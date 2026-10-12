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
	IReadOnlyList<PlayedTrack> History,
	int Volume,
	LoopMode Loop,
	bool Radio);

public sealed class QueueFullException : Exception;

/// <summary>
/// Queue + audio pipeline: TrackSource (PCM) → timer → volume → Opus encoder → voice target → TeamSpeak.
/// </summary>
public sealed class Player : IDisposable
{
	const int BytesPerSecond = 48000 * 2 * 2;

	readonly BotConfig settings;
	readonly PandaConfig config;
	readonly YouTube youtube;
	readonly Library library;
	readonly PlayHistory history;
	readonly ILogger log;

	readonly PreciseTimedPipe timePipe;
	readonly VolumePipe volumePipe;
	readonly OpusEncoderPipe encoder;
	readonly StaticMetaPipe target;

	readonly object gate = new();
	readonly List<Track> queue = [];
	Track? current;
	TrackSource? source;
	int generation;
	int startOffset;
	bool paused;
	int volume;
	// For the play history: when the current song started and how much of it was heard
	// by sources that are already gone (seeking and loop-one replace the source).
	DateTimeOffset startedAt;
	double heardBefore;
	// Radio: a pick is being looked for; anything that makes a running search outdated bumps the
	// generation; a song without a Mix isn't asked again; picks that fail to play in a row.
	bool radioBusy;
	int radioGen;
	string? radioNoMix;
	int radioFailures;

	public LoopMode Loop { get; private set; }

	/// <summary>Anything visible changed (queue, song, pause, volume...).</summary>
	public event Action? Changed;
	public event Action<Track>? TrackStarted;
	public event Action<Track, string>? TrackFailed;

	public Player(BotConfig settings, PandaConfig config, YouTube youtube, Library library, PlayHistory history, ILogger log)
	{
		this.settings = settings;
		this.config = config;
		this.youtube = youtube;
		this.library = library;
		this.history = history;
		this.log = log;

		encoder = new OpusEncoderPipe(bitrate: 96_000);
		timePipe = new PreciseTimedPipe(encoder, Id.Null) { ReadBufferSize = OpusEncoderPipe.PacketSize, Paused = true };
		volumePipe = new VolumePipe();
		target = new StaticMetaPipe();
		target.SetVoice();
		timePipe.Chain(volumePipe).Chain(encoder).Chain(target);
		SetVolume(settings.Bot.Volume, save: false);
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
			return new PlayerState(now, queue.ToArray(), history.Recent(), volume, Loop, settings.Bot.Radio);
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
			if (queue.Count >= settings.Bot.MaxQueueLength)
				throw new QueueFullException();
			DropRadioPicksLocked();
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

	/// <summary>Adds several tracks (a playlist) until the queue is full. Returns how many were added.</summary>
	public int EnqueueMany(IEnumerable<Track> tracks)
	{
		var added = 0;
		lock (gate)
		{
			DropRadioPicksLocked();
			foreach (var t in tracks)
			{
				if (current is null) StartLocked(t, 0);
				else if (queue.Count < settings.Bot.MaxQueueLength) queue.Add(t);
				else break;
				added++;
			}
		}
		if (added > 0) RaiseChanged();
		return added;
	}

	/// <summary>Puts a track at the front of the queue and skips to it.</summary>
	public void PlayNow(Track track)
	{
		lock (gate)
		{
			DropRadioPicksLocked();
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
			radioGen++; // a pick on its way must not start playing again
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
			settings.Bot.Volume = value;
			config.Save();
		}
		RaiseChanged();
	}

	public void SetLoop(LoopMode mode)
	{
		lock (gate) Loop = mode;
		RaiseChanged();
	}

	/// <param name="expectedId">When given, only removes the song if it is still at that position
	/// (the queue may have moved on since the panel showed it).</param>
	public Track? Remove(int index, string? expectedId = null)
	{
		Track removed;
		lock (gate)
		{
			if (index < 0 || index >= queue.Count) return null;
			if (expectedId != null && queue[index].Id != expectedId) return null;
			removed = queue[index];
			queue.RemoveAt(index);
		}
		RaiseChanged();
		return removed;
	}

	public bool Move(int from, int to, string? expectedId = null)
	{
		lock (gate)
		{
			if (from < 0 || from >= queue.Count || to < 0 || to >= queue.Count) return false;
			if (expectedId != null && queue[from].Id != expectedId) return false;
			var t = queue[from];
			queue.RemoveAt(from);
			queue.Insert(to, t);
		}
		RaiseChanged();
		return true;
	}

	public void ClearQueue()
	{
		lock (gate)
		{
			radioGen++;
			queue.Clear();
		}
		RaiseChanged();
	}

	// ---- radio: when the queue runs out, songs from the Mix of the last one ----

	public bool Radio => settings.Bot.Radio;

	public void SetRadio(bool on)
	{
		Track? seed = null;
		lock (gate)
		{
			settings.Bot.Radio = on;
			radioGen++;
			radioNoMix = null;
			radioFailures = 0;
			if (!on) queue.RemoveAll(t => t.Radio);
			// Turned on while idle: start from the last song played.
			else if (current is null && queue.Count == 0) seed = history.Recent().FirstOrDefault()?.ToTrack();
		}
		config.Save();
		if (seed != null) FetchRadio(seed);
		RaiseChanged();
	}

	// A song someone asked for goes first; the radio picks again after it.
	void DropRadioPicksLocked()
	{
		if (queue.RemoveAll(t => t.Radio) > 0 || radioBusy) radioGen++;
	}

	// The last song is playing and nothing comes after it: look for the next one now, so it can be
	// downloaded ahead of time like any queued song.
	void CheckRadio()
	{
		Track seed;
		lock (gate)
		{
			if (!settings.Bot.Radio || radioBusy || current is null || queue.Count > 0 || current.Id == radioNoMix) return;
			seed = current;
		}
		FetchRadio(seed);
	}

	void FetchRadio(Track seed)
	{
		int gen;
		lock (gate)
		{
			if (radioBusy) return;
			radioBusy = true;
			gen = radioGen;
		}
		_ = Task.Run(async () =>
		{
			Track? pick = null;
			try { pick = await PickRadioAsync(seed); }
			catch (Exception ex) { log.LogWarning("Radio: no Mix for {Title}: {Error}", seed.Title, ex.Message); }
			var added = false;
			lock (gate)
			{
				radioBusy = false;
				if (gen != radioGen || !settings.Bot.Radio) { } // outdated: looked at again below
				else if (pick is null) radioNoMix = seed.Id; // stays quiet after this song
				else if (queue.Count == 0)
				{
					log.LogInformation("Radio: {Title} comes next (like {Seed})", pick.Title, seed.Title);
					if (current is null) StartLocked(pick, 0);
					else queue.Add(pick);
					added = true;
				}
			}
			if (added) RaiseChanged();
			else if (gen != radioGen) CheckRadio();
		});
	}

	/// <summary>The first song of the seed's Mix that wasn't played lately, or null.</summary>
	async Task<Track?> PickRadioAsync(Track seed)
	{
		HashSet<string> skip;
		lock (gate)
		{
			skip = history.Recent().Select(p => p.Id).Concat(queue.Select(t => t.Id)).ToHashSet();
			if (current != null) skip.Add(current.Id);
		}
		skip.Add(seed.Id);
		var max = config.YouTube.MaxDurationMinutes * 60;
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		foreach (var t in await youtube.MixAsync(seed.Id, 50, cts.Token))
		{
			if (skip.Contains(t.Id) || (max > 0 && t.Duration > max)) continue;
			var track = t;
			if (track.IsLive) // the Mix didn't say how long it is: ask
			{
				try { track = await youtube.ResolveAsync(t.Url, "", cts.Token); }
				catch (YouTubeException) { continue; }
				if (track.IsLive) continue; // no live streams on the radio
			}
			return track with { RequestedBy = Strings.Get(settings.Bot.Language, "radio-name"), Radio = true };
		}
		return null;
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
		if (finished && Loop == LoopMode.One && prev != null)
		{
			// One history entry for the whole loop, not one per repeat.
			StartLocked(prev, 0, repeat: true);
			return;
		}

		if (prev != null)
		{
			AddHistoryLocked(prev);
			if (Loop == LoopMode.All) queue.Add(prev);
		}
		Track? next = null;
		if (queue.Count > 0)
		{
			next = queue[0];
			queue.RemoveAt(0);
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
	// "repeat" is loop-one starting it over: announced, but the same history entry.
	void StartLocked(Track track, int startSeconds, bool seeking = false, bool repeat = false)
	{
		StopSourceLocked();
		var gen = ++generation;
		current = track;
		startOffset = startSeconds;
		paused = false;
		if (!seeking && !repeat)
		{
			startedAt = DateTimeOffset.UtcNow;
			heardBefore = 0;
		}

		AudioStream audio;
		var file = library.GetFile(track.Id);
		var gain = file is null ? null : library.GainFor(track.Id);
		string? tee = null;
		try
		{
			if (file != null)
			{
				audio = youtube.OpenFile(file, startSeconds, gain);
			}
			else
			{
				// Not on disk yet: stream it from YouTube and save it on the way.
				tee = seeking ? null : library.BeginTee(track);
				audio = youtube.OpenAudio(track, startSeconds, tee,
					tee is null ? null : complete => library.EndTee(track, tee, complete));
			}
		}
		catch (YouTubeException ex)
		{
			// The library holds the song as "being saved" until EndTee; release it.
			if (tee != null) library.EndTee(track, tee, complete: false);
			// Report and move on without blocking the caller.
			_ = Task.Run(() => OnFailed(gen, track, ex.Message));
			return;
		}

		source = new TrackSource(audio, gen, file != null, OnSourceEnded);
		timePipe.InStream = source;
		timePipe.Paused = false;
		if (!seeking)
		{
			library.MarkPlayed(settings.Id, track);
			_ = Task.Run(() => TrackStarted?.Invoke(track));
		}
		log.LogInformation("Playing {Title} ({Id}) for {User}{From}{Gain}", track.Title, track.Id, track.RequestedBy,
			file != null ? " from the library" : "", gain is { } g ? $", loudness {g:+0.0;-0.0} dB" : "");
	}

	void StopSourceLocked()
	{
		timePipe.Paused = true;
		timePipe.InStream = null;
		heardBefore += SourceSecondsLocked();
		source?.Dispose();
		source = null;
	}

	double SourceSecondsLocked() =>
		source is null ? 0 : Interlocked.Read(ref source.BytesRead) / (double)BytesPerSecond;

	void AddHistoryLocked(Track t)
	{
		var heard = (int)Math.Round(heardBefore + SourceSecondsLocked());
		history.Add(new PlayedTrack(t.Id, t.Title, t.Channel, t.Duration, t.RequestedBy, startedAt, heard));
	}

	void OnSourceEnded(TrackSource ended)
	{
		lock (gate)
		{
			if (ended.Generation != generation || current is null)
				return;
			// Less than a second of audio means yt-dlp/ffmpeg failed rather than the song ending,
			// unless it was started (seeked) right before its end.
			var remaining = current.IsLive ? int.MaxValue : current.Duration - startOffset;
			if (Interlocked.Read(ref ended.BytesRead) < BytesPerSecond && remaining > 3)
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
				radioFailures = 0;
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
	// When it was the radio's pick and nothing else comes, the radio tries again (a few times).
	void AdvanceAfterFailureLocked()
	{
		var failed = current;
		var loop = Loop;
		current = null;
		Loop = LoopMode.Off;
		try { AdvanceLocked(finished: false); }
		finally { Loop = loop; }
		if (failed is { Radio: true } && current is null && settings.Bot.Radio && ++radioFailures <= 3)
			_ = Task.Run(() => FetchRadio(failed));
	}

	void RaiseChanged()
	{
		CheckRadio();
		Track[] upcoming;
		bool idle;
		lock (gate)
		{
			upcoming = queue.ToArray();
			idle = current is null;
		}
		if (idle) library.ClearNowPlaying(settings.Id);
		library.Want(settings.Id, upcoming);
		try { Changed?.Invoke(); }
		catch (Exception ex) { log.LogError(ex, "Changed handler failed"); }
	}

	public void Dispose()
	{
		lock (gate)
		{
			// Shutting down mid-song still counts as a play.
			if (current != null) AddHistoryLocked(current);
			current = null;
			StopSourceLocked();
		}
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
