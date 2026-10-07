using System.Text.Json.Serialization;
using TSLib;
using TSLib.Audio;
using TSLib.Helper;

namespace Panda;

[JsonConverter(typeof(JsonStringEnumConverter<LoopMode>))]
public enum LoopMode { Off, One, All }

public sealed record NowPlaying(Track Track, double Position, bool Paused);

public sealed record PlayerState(
	NowPlaying? Current,
	IReadOnlyList<Track> Queue,
	IReadOnlyList<Track> History,
	int Volume,
	LoopMode Loop);

public sealed class QueueFullException : Exception;

/// <summary>
/// Queue + audio pipeline: TrackSource (PCM) → timer → volume → Opus encoder → voice target → TeamSpeak.
/// </summary>
public sealed class Player : IDisposable
{
	const int BytesPerSecond = 48000 * 2 * 2;
	const int HistorySize = 30;

	readonly YouTube youtube;
	readonly Library library;
	readonly PandaConfig config;
	readonly ILogger<Player> log;

	readonly PreciseTimedPipe timePipe;
	readonly VolumePipe volumePipe;
	readonly OpusEncoderPipe encoder;
	readonly StaticMetaPipe target;

	readonly object gate = new();
	readonly List<Track> queue = [];
	readonly List<Track> history = [];
	Track? current;
	TrackSource? source;
	int generation;
	int startOffset;
	bool paused;
	int volume;

	public LoopMode Loop { get; private set; }

	/// <summary>Anything visible changed (queue, song, pause, volume...).</summary>
	public event Action? Changed;
	public event Action<Track>? TrackStarted;
	public event Action<Track, string>? TrackFailed;

	public Player(YouTube youtube, Library library, PandaConfig config, ILogger<Player> log)
	{
		this.youtube = youtube;
		this.library = library;
		this.config = config;
		this.log = log;

		encoder = new OpusEncoderPipe(bitrate: 96_000);
		timePipe = new PreciseTimedPipe(encoder, Id.Null) { ReadBufferSize = OpusEncoderPipe.PacketSize, Paused = true };
		volumePipe = new VolumePipe();
		target = new StaticMetaPipe();
		target.SetVoice();
		timePipe.Chain(volumePipe).Chain(encoder).Chain(target);
		SetVolume(config.Bot.Volume, save: false);
	}

	/// <summary>Where encoded audio goes (the connected TeamSpeak client), or null while offline.</summary>
	public void AttachOutput(IAudioPassiveConsumer? output) => target.OutStream = output;

	public int Volume => volume;
	public Track? Current { get { lock (gate) return current; } }
	public bool IsPaused { get { lock (gate) return paused; } }

	public PlayerState GetState()
	{
		lock (gate)
		{
			NowPlaying? now = current is null ? null : new(current, PositionLocked(), paused);
			return new PlayerState(now, queue.ToArray(), history.ToArray(), volume, Loop);
		}
	}

	double PositionLocked() =>
		source is null ? startOffset : startOffset + Interlocked.Read(ref source.BytesRead) / (double)BytesPerSecond;

	/// <summary>Adds a track; starts it right away when nothing is playing. Returns its queue position (0 = playing now).</summary>
	public int Enqueue(Track track)
	{
		int position;
		lock (gate)
		{
			if (queue.Count >= config.Bot.MaxQueueLength)
				throw new QueueFullException();
			if (current is null)
			{
				StartLocked(track, 0);
				position = 0;
			}
			else
			{
				queue.Add(track);
				position = queue.Count;
			}
		}
		RaiseChanged();
		return position;
	}

	/// <summary>Puts a track at the front of the queue and skips to it.</summary>
	public void PlayNow(Track track)
	{
		lock (gate)
		{
			queue.Insert(0, track);
			AdvanceLocked(finished: false);
		}
		RaiseChanged();
	}

	public bool Skip()
	{
		lock (gate)
		{
			if (current is null) return false;
			AdvanceLocked(finished: false);
		}
		RaiseChanged();
		return true;
	}

	public void Stop()
	{
		lock (gate)
		{
			queue.Clear();
			if (current != null) AddHistoryLocked(current);
			StopSourceLocked();
			current = null;
		}
		RaiseChanged();
	}

	public bool Pause() => SetPaused(true);
	public bool Resume() => SetPaused(false);

	bool SetPaused(bool value)
	{
		lock (gate)
		{
			if (current is null || paused == value) return false;
			paused = value;
			timePipe.Paused = value;
		}
		RaiseChanged();
		return true;
	}

	public bool Seek(int seconds)
	{
		lock (gate)
		{
			if (current is null || current.IsLive) return false;
			seconds = Math.Clamp(seconds, 0, Math.Max(0, current.Duration - 1));
			StartLocked(current, seconds, seeking: true);
		}
		RaiseChanged();
		return true;
	}

	public void SetVolume(int value, bool save = true)
	{
		value = Math.Clamp(value, 0, 100);
		volume = value;
		// Perceived loudness is roughly logarithmic, so a squared curve feels linear on a slider.
		volumePipe.Volume = (float)Math.Pow(value / 100.0, 2);
		if (save)
		{
			config.Bot.Volume = value;
			config.Save();
		}
		RaiseChanged();
	}

	public void SetLoop(LoopMode mode)
	{
		lock (gate) Loop = mode;
		RaiseChanged();
	}

	public Track? Remove(int index)
	{
		Track removed;
		lock (gate)
		{
			if (index < 0 || index >= queue.Count) return null;
			removed = queue[index];
			queue.RemoveAt(index);
		}
		RaiseChanged();
		return removed;
	}

	public bool Move(int from, int to)
	{
		lock (gate)
		{
			if (from < 0 || from >= queue.Count || to < 0 || to >= queue.Count) return false;
			var t = queue[from];
			queue.RemoveAt(from);
			queue.Insert(to, t);
		}
		RaiseChanged();
		return true;
	}

	public void ClearQueue()
	{
		lock (gate) queue.Clear();
		RaiseChanged();
	}

	public void Shuffle()
	{
		lock (gate)
		{
			for (int i = queue.Count - 1; i > 0; i--)
			{
				int j = Random.Shared.Next(i + 1);
				(queue[i], queue[j]) = (queue[j], queue[i]);
			}
		}
		RaiseChanged();
	}

	// Moves on to the next song. "finished" means the current one ended by itself,
	// which is the only case where loop-one repeats it.
	void AdvanceLocked(bool finished)
	{
		var prev = current;
		Track? next = null;
		if (finished && Loop == LoopMode.One && prev != null)
		{
			next = prev;
		}
		else
		{
			if (prev != null)
			{
				AddHistoryLocked(prev);
				if (Loop == LoopMode.All) queue.Add(prev);
			}
			if (queue.Count > 0)
			{
				next = queue[0];
				queue.RemoveAt(0);
			}
		}

		if (next is null)
		{
			StopSourceLocked();
			current = null;
		}
		else
		{
			StartLocked(next, 0);
		}
	}

	// "seeking" restarts the same song elsewhere: no announcement, no extra play count.
	void StartLocked(Track track, int startSeconds, bool seeking = false)
	{
		StopSourceLocked();
		var gen = ++generation;
		current = track;
		startOffset = startSeconds;
		paused = false;

		AudioStream audio;
		var file = library.GetFile(track.Id);
		try
		{
			if (file != null)
			{
				audio = youtube.OpenFile(file, startSeconds);
			}
			else
			{
				// Not on disk yet: stream it from YouTube and save it on the way.
				var tee = seeking ? null : library.BeginTee(track);
				audio = youtube.OpenAudio(track, startSeconds, tee,
					tee is null ? null : complete => library.EndTee(track, tee, complete));
			}
		}
		catch (YouTubeException ex)
		{
			// Report and move on without blocking the caller.
			_ = Task.Run(() => OnFailed(gen, track, ex.Message));
			return;
		}

		source = new TrackSource(audio, gen, file != null, OnSourceEnded);
		timePipe.InStream = source;
		timePipe.Paused = false;
		if (!seeking)
		{
			library.MarkPlayed(track);
			_ = Task.Run(() => TrackStarted?.Invoke(track));
		}
		log.LogInformation("Playing {Title} ({Id}) for {User}{From}", track.Title, track.Id, track.RequestedBy, file != null ? " from the library" : "");
	}

	void StopSourceLocked()
	{
		timePipe.Paused = true;
		timePipe.InStream = null;
		source?.Dispose();
		source = null;
	}

	void AddHistoryLocked(Track t)
	{
		history.Insert(0, t);
		if (history.Count > HistorySize) history.RemoveAt(history.Count - 1);
	}

	void OnSourceEnded(TrackSource ended)
	{
		lock (gate)
		{
			if (ended.Generation != generation || current is null)
				return;
			// Less than a second of audio means yt-dlp/ffmpeg failed rather than the song ending.
			if (Interlocked.Read(ref ended.BytesRead) < BytesPerSecond && !current.IsLive)
			{
				var reason = YouTube.FriendlyError(ended.Errors);
				var failed = current;
				log.LogWarning("Could not play {Id}: {Reason}", failed.Id, ended.Errors.Trim());
				// A saved file that can't be played is broken; drop it so it's downloaded again next time.
				if (ended.FromFile) library.Delete(failed.Id);
				AdvanceAfterFailureLocked();
				_ = Task.Run(() => TrackFailed?.Invoke(failed, reason));
			}
			else
			{
				AdvanceLocked(finished: true);
			}
		}
		RaiseChanged();
	}

	void OnFailed(int gen, Track track, string reason)
	{
		lock (gate)
		{
			if (gen != generation) return;
			AdvanceAfterFailureLocked();
		}
		TrackFailed?.Invoke(track, reason);
		RaiseChanged();
	}

	// A broken song is dropped: never repeated by loop-one, never re-queued by loop-all.
	void AdvanceAfterFailureLocked()
	{
		var loop = Loop;
		current = null;
		Loop = LoopMode.Off;
		try { AdvanceLocked(finished: false); }
		finally { Loop = loop; }
	}

	void RaiseChanged()
	{
		Track[] upcoming;
		bool idle;
		lock (gate)
		{
			upcoming = queue.ToArray();
			idle = current is null;
		}
		if (idle) library.ClearNowPlaying();
		library.Want(upcoming);
		try { Changed?.Invoke(); }
		catch (Exception ex) { log.LogError(ex, "Changed handler failed"); }
	}

	public void Dispose()
	{
		lock (gate) StopSourceLocked();
		timePipe.Dispose();
	}

	/// <summary>Feeds ffmpeg's PCM into the timer pipe and notices when the song is over.</summary>
	sealed class TrackSource(AudioStream audio, int generation, bool fromFile, Action<TrackSource> ended) : IAudioPassiveProducer
	{
		public bool FromFile => fromFile;
		public long BytesRead;
		public int Generation => generation;
		public string Errors => audio.Errors;
		int done;

		public int Read(byte[] buffer, int offset, int length, out Meta? meta)
		{
			meta = null;
			if (Volatile.Read(ref done) != 0) return 0;
			int read;
			try
			{
				// Fill the whole buffer so 16-bit samples are never split between reads.
				read = audio.Pcm.ReadAtLeast(buffer.AsSpan(offset, length), length, throwOnEndOfStream: false);
			}
			catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
			{
				read = 0;
			}
			if (read > 0)
				Interlocked.Add(ref BytesRead, read);
			if (read < length && Interlocked.Exchange(ref done, 1) == 0)
				ThreadPool.QueueUserWorkItem(_ => ended(this));
			return read;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref done, 1);
			audio.Dispose();
		}
	}
}
