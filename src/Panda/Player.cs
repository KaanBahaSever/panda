using System.Text.Json.Serialization;
using TSLib;
using TSLib.Audio;
using TSLib.Helper;

namespace Panda;

[JsonConverter(typeof(JsonStringEnumConverter<LoopMode>))]
public enum LoopMode { Off, One, All }

/// <param name="StreamTitle">What an internet radio station says it plays right now, when it says.</param>
public sealed record NowPlaying(Track Track, double Position, bool Paused, string? StreamTitle = null);

public sealed record PlayerState(
	NowPlaying? Current,
	IReadOnlyList<Track> Queue,
	IReadOnlyList<PlayedTrack> History,
	int Volume,
	LoopMode Loop,
	bool Autoplay);

/// <summary>
/// What a bot was playing when Panda stopped (an update or a restart), so it picks up where it left off.
/// </summary>
public sealed record PlayerSession(
	Track? Current, double Position, bool Paused, DateTimeOffset StartedAt, double Heard,
	IReadOnlyList<Track> Queue, LoopMode Loop);

public sealed class QueueFullException : Exception;

/// <summary>The person already has as many songs waiting as the bot allows.</summary>
public sealed class UserLimitException(int max) : Exception
{
	public int Max => max;
}

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
	readonly SponsorBlock sponsorBlock;
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
	// Tries of the current song so far (0 = the first): YouTube refuses a stream now and then.
	int attempt;
	bool paused;
	int volume;
	// For the play history: when the current song started and how much of it was heard
	// by sources that are already gone (seeking and loop-one replace the source).
	DateTimeOffset startedAt;
	double heardBefore;
	// Autoplay: a pick is being looked for; anything that makes a running search outdated bumps the
	// generation; a song without a Mix isn't asked again; picks that fail to play in a row.
	bool autoBusy;
	int autoGen;
	string? autoNoMix;
	int autoFailures;
	// The bot was removed: work that finishes late (an autoplay pick, a song still being looked up)
	// must not start playback, nor touch the library, on a player whose audio thread is gone.
	bool disposed;
	// Restored after a restart: the song waits paused until the bot is connected again.
	bool resumeOnConnect;
	// The current song was saved for after the restart; it is not a finished play yet.
	bool sessionSaved;
	// What the radio station playing says it plays.
	string? streamTitle;
	// Goes up when the queue is thrown away (!stop, !clear): songs of a list still being looked up stop coming.
	int epoch;

	public LoopMode Loop { get; private set; }

	/// <summary>Anything visible changed (queue, song, pause, volume...).</summary>
	public event Action? Changed;
	public event Action<Track>? TrackStarted;
	public event Action<Track, string>? TrackFailed;
	/// <summary>The radio station playing names another song.</summary>
	public event Action<Track, string>? StreamTitleChanged;

	public Player(BotConfig settings, PandaConfig config, YouTube youtube, Library library, SponsorBlock sponsorBlock, PlayHistory history, ILogger log)
	{
		this.settings = settings;
		this.config = config;
		this.youtube = youtube;
		this.library = library;
		this.sponsorBlock = sponsorBlock;
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
	public void AttachOutput(IAudioPassiveConsumer? output)
	{
		target.OutStream = output;
		bool resume;
		lock (gate)
		{
			resume = output != null && resumeOnConnect;
			resumeOnConnect = false;
		}
		if (resume) Resume();
	}

	public int Volume => volume;
	/// <summary>The bot was removed (or Panda is stopping): nothing more is added.</summary>
	public bool Disposed { get { lock (gate) return disposed; } }
	public Track? Current { get { lock (gate) return current; } }
	public string? StreamTitle { get { lock (gate) return streamTitle; } }
	/// <summary>See <see cref="epoch"/>: a list being added stops when this changed.</summary>
	public int Epoch { get { lock (gate) return epoch; } }
	public bool IsPaused { get { lock (gate) return paused; } }

	public PlayerState GetState()
	{
		lock (gate)
		{
			NowPlaying? now = current is null ? null : new(current, PositionLocked(), paused, streamTitle);
			return new PlayerState(now, queue.ToArray(), history.Recent(), volume, Loop, settings.Bot.Autoplay);
		}
	}

	double PositionLocked() =>
		source is null ? startOffset : startOffset + Interlocked.Read(ref source.BytesRead) / (double)BytesPerSecond;

	/// <summary>Adds a track; starts it right away when nothing is playing. Returns its queue position (0 = playing now).</summary>
	/// <param name="exempt">Not held to the songs-per-person limit (a panel admin).</param>
	public int Enqueue(Track track, bool exempt = false)
	{
		int position;
		lock (gate)
		{
			if (disposed) return 0;
			if (queue.Count >= settings.Bot.MaxQueueLength)
				throw new QueueFullException();
			if (current != null && !exempt && WaitingLocked(track.RequesterId) >= PerUser)
				throw new UserLimitException(PerUser);
			DropAutoplayPicksLocked();
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
		if (position <= 2) sponsorBlock.Prefetch(track);
		RaiseChanged();
		return position;
	}

	/// <summary>Adds several tracks (a playlist) until the queue is full. Returns how many were added;
	/// <paramref name="limited"/> tells that the songs-per-person limit stopped it.</summary>
	public int EnqueueMany(IEnumerable<Track> tracks, bool exempt, out bool limited)
	{
		var added = 0;
		limited = false;
		lock (gate)
		{
			if (disposed) return 0;
			DropAutoplayPicksLocked();
			foreach (var t in tracks)
			{
				if (youtube.TooLong(t)) continue;   // a playlist song over the length limit
				if (current is null) StartLocked(t, 0);
				else if (queue.Count >= settings.Bot.MaxQueueLength) break;
				else if (!exempt && WaitingLocked(t.RequesterId) >= PerUser) { limited = true; break; }
				else queue.Add(t);
				added++;
			}
		}
		if (added > 0) RaiseChanged();
		return added;
	}

	int PerUser => settings.Bot.MaxSongsPerUser > 0 ? settings.Bot.MaxSongsPerUser : int.MaxValue;

	// Songs this person has waiting (the one playing doesn't count). Unknown requesters aren't limited.
	int WaitingLocked(string? requesterId) =>
		requesterId is null ? 0 : queue.Count(t => t.RequesterId == requesterId);

	/// <summary>Puts a track at the front of the queue and skips to it.</summary>
	/// <param name="exempt">Not held to the songs-per-person limit (a panel admin).</param>
	public void PlayNow(Track track, bool exempt = false)
	{
		lock (gate)
		{
			if (disposed) return;
			if (current != null && !exempt && WaitingLocked(track.RequesterId) >= PerUser)
				throw new UserLimitException(PerUser);
			DropAutoplayPicksLocked();
			queue.Insert(0, track);
			AdvanceLocked(finished: false);
			// Loop "all" put the song that was playing back at the end; that may not pass the limit.
			while (queue.Count > settings.Bot.MaxQueueLength) queue.RemoveAt(queue.Count - 1);
		}
		RaiseChanged();
	}

	/// <param name="expected">When given, only skips if this is still the song playing.</param>
	public bool Skip(Track? expected = null)
	{
		lock (gate)
		{
			if (current is null || (expected != null && !ReferenceEquals(current, expected))) return false;
			AdvanceLocked(finished: false);
		}
		RaiseChanged();
		return true;
	}

	/// <summary>
	/// Plays the song before this one again; the current song goes back to the front of the queue.
	/// Returns the song, or null when nothing was played before.
	/// </summary>
	public Track? Back()
	{
		Track prev;
		lock (gate)
		{
			// The song playing may itself be the last one (played again by !back): look past it.
			if (disposed || history.Recent().FirstOrDefault(p => p.Id != current?.Id) is not { } last) return null;
			prev = last.ToTrack();
			DropAutoplayPicksLocked();
			// The interrupted song isn't a finished play: it comes again right after.
			if (current != null)
			{
				queue.Insert(0, current);
				if (queue.Count > settings.Bot.MaxQueueLength) queue.RemoveAt(queue.Count - 1);
			}
			current = null;
			StartLocked(prev, 0);
		}
		RaiseChanged();
		return prev;
	}

	/// <summary>Jumps to the song at <paramref name="index"/> of the queue, leaving out the ones before it
	/// (with loop "all" they go to the end). Returns the song, or null for a wrong index.</summary>
	public Track? SkipTo(int index, string? expectedId = null)
	{
		Track to;
		lock (gate)
		{
			if (disposed || index < 0 || index >= queue.Count) return null;
			if (expectedId != null && queue[index].Id != expectedId) return null;
			to = queue[index];
			var passed = queue.GetRange(0, index);
			queue.RemoveRange(0, index);
			AdvanceLocked(finished: false);
			if (Loop == LoopMode.All) queue.AddRange(passed);
		}
		RaiseChanged();
		return to;
	}

	public void Stop()
	{
		lock (gate)
		{
			epoch++;
			autoGen++; // a pick on its way must not start playing again
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
			resumeOnConnect = false;
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
			epoch++;
			autoGen++;
			queue.Clear();
		}
		RaiseChanged();
	}

	// ---- autoplay: when the queue runs out, songs from the YouTube Mix of the last one ----

	public bool Autoplay => settings.Bot.Autoplay;

	public void SetAutoplay(bool on)
	{
		Track? seed = null;
		lock (gate)
		{
			settings.Bot.Autoplay = on;
			autoGen++;
			autoNoMix = null;
			autoFailures = 0;
			if (!on) queue.RemoveAll(t => t.Autoplay);
			// Turned on while idle: start from the last song played.
			else if (current is null && queue.Count == 0) seed = LastYouTubeLocked();
		}
		config.Save();
		if (seed != null) FetchAutoplay(seed);
		RaiseChanged();
	}

	// A song someone asked for goes first; autoplay picks again after it.
	void DropAutoplayPicksLocked()
	{
		if (queue.RemoveAll(t => t.Autoplay) > 0 || autoBusy) autoGen++;
	}

	// The last song is playing and nothing comes after it: look for the next one now, so it can be
	// downloaded ahead of time like any queued song. Only YouTube has Mixes: after a song from elsewhere
	// it goes on from the last YouTube song; a radio station never ends, so nothing comes after it.
	void CheckAutoplay()
	{
		Track seed;
		lock (gate)
		{
			if (disposed || !settings.Bot.Autoplay || autoBusy || current is null || queue.Count > 0 || current.IsLive) return;
			if ((current.IsYouTube ? current : LastYouTubeLocked()) is not { } s || s.Id == autoNoMix) return;
			seed = s;
		}
		FetchAutoplay(seed);
	}

	Track? LastYouTubeLocked() =>
		history.Recent().FirstOrDefault(p => p.Source is null && !p.IsLive)?.ToTrack();

	void FetchAutoplay(Track seed)
	{
		int gen;
		lock (gate)
		{
			if (autoBusy || disposed) return;
			autoBusy = true;
			gen = autoGen;
		}
		_ = Task.Run(async () =>
		{
			Track? pick = null;
			try { pick = await PickAutoplayAsync(seed); }
			catch (Exception ex) { log.LogWarning("Autoplay: no Mix for {Title}: {Error}", seed.Title, ex.Message); }
			var added = false;
			lock (gate)
			{
				autoBusy = false;
				if (gen != autoGen || !settings.Bot.Autoplay) { } // outdated: looked at again below
				else if (pick is null) autoNoMix = seed.Id; // stays quiet after this song
				else if (queue.Count == 0)
				{
					log.LogInformation("Autoplay: {Title} comes next (like {Seed})", pick.Title, seed.Title);
					if (current is null) StartLocked(pick, 0);
					else queue.Add(pick);
					added = true;
				}
			}
			if (added) RaiseChanged();
			else if (gen != autoGen) CheckAutoplay();
		});
	}

	/// <summary>The first song of the seed's Mix that wasn't played lately (and, when the settings say so,
	/// isn't by the same artist), or null.</summary>
	async Task<Track?> PickAutoplayAsync(Track seed)
	{
		HashSet<string> skip;
		lock (gate)
		{
			skip = history.Recent().Take(Math.Clamp(config.YouTube.AutoplayNoRepeat, 0, PlayHistory.RecentSize))
				.Select(p => p.Id).Concat(queue.Select(t => t.Id)).ToHashSet();
			if (current != null) skip.Add(current.Id);
		}
		skip.Add(seed.Id);
		var sameArtist = config.YouTube.AutoplaySameArtist;
		var artist = Artist(seed.Channel);
		var max = config.YouTube.MaxDurationMinutes * 60;
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		foreach (var t in await youtube.MixAsync(seed.Id, 50, cts.Token))
		{
			if (skip.Contains(t.Id) || (max > 0 && t.Duration > max)) continue;
			if (!sameArtist && artist.Length > 0 && Artist(t.Channel) == artist) continue;
			var track = t;
			if (track.IsLive) // the Mix didn't say how long it is: ask
			{
				try { track = await youtube.ResolveAsync(t.Url, "", cts.Token); }
				catch (YouTubeException) { continue; }
				if (track.IsLive) continue; // no live streams in autoplay
			}
			return track with { RequestedBy = Strings.Get(settings.Bot.Language, "autoplay-name"), Autoplay = true };
		}
		return null;
	}

	// "Artist - Topic", "ArtistVEVO" and "Artist" are one artist.
	static string Artist(string channel) =>
		Library.Normalize(System.Text.RegularExpressions.Regex.Replace(channel, @"\s*-\s*Topic$|VEVO$|\s+Official$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

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
	// "retry" > 0 is another try after YouTube refused the song or broke off: as quiet as seeking.
	void StartLocked(Track track, int startSeconds, bool seeking = false, bool repeat = false, int retry = 0)
	{
		if (disposed) return;
		resumeOnConnect = false;
		StopSourceLocked();
		var gen = ++generation;
		current = track;
		startOffset = startSeconds;
		paused = false;
		attempt = retry;
		if (!seeking && !repeat && retry == 0)
		{
			startedAt = DateTimeOffset.UtcNow;
			heardBefore = 0;
		}

		if (retry == 0 && !seeking) streamTitle = null;
		// SponsorBlock: a song that starts with a part to skip starts after it.
		var segments = sponsorBlock.Known(track);
		if (segments?.FirstOrDefault(s => s.Start <= startSeconds + 0.5 && s.End > startSeconds) is { } first && first.End < track.Duration - 2)
		{
			log.LogInformation("Skipping {From}–{To} of {Title} (SponsorBlock: {Category})",
				Strings.FormatTime(first.Start), Strings.FormatTime(first.End), track.Title, first.Category);
			startOffset = startSeconds = (int)Math.Ceiling(first.End);
		}
		else if (segments is null && sponsorBlock.Enabled && track.IsYouTube && !track.IsLive)
			_ = Task.Run(async () => { await sponsorBlock.GetAsync(track); SegmentsArrived(gen, track); });

		AudioStream audio;
		// Only YouTube songs are saved (their id names the file).
		var file = track.IsYouTube ? library.GetFile(track.Id) : null;
		var gain = file is null ? null : library.GainFor(track.Id);
		string? tee = null;
		try
		{
			if (file != null)
			{
				audio = youtube.OpenFile(file, startSeconds, gain);
			}
			else if (track.Source == Media.Radio)
			{
				audio = youtube.OpenRadio(track, title => OnStreamTitle(gen, track, title));
			}
			else if (track.Source == Media.Direct)
			{
				audio = youtube.OpenUrl(track, startSeconds);
			}
			else
			{
				// Not on disk yet: stream it (from YouTube: and save it on the way).
				tee = seeking || !track.IsYouTube ? null : library.BeginTee(track);
				audio = youtube.OpenAudio(track, startSeconds, tee,
					tee is null ? null : complete => library.EndTee(track, tee, complete), retry);
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

		source = new TrackSource(audio, gen, file != null, OnSourceEnded, OnSkipPoint);
		SetSkipPointLocked(segments);
		// The next songs' parts to skip are known before they start (not a whole playlist's at once).
		foreach (var next in queue.Take(2)) sponsorBlock.Prefetch(next);
		timePipe.InStream = source;
		timePipe.Paused = false;
		if (!seeking && retry == 0)
		{
			if (track.IsYouTube) library.MarkPlayed(settings.Id, track);
			_ = Task.Run(() => TrackStarted?.Invoke(track));
		}
		if (retry > 0)
			log.LogInformation("Trying {Title} ({Id}) again{At}, try {Try} of {Tries}", track.Title, track.Id,
				startSeconds > 0 ? $" from {Strings.FormatTime(startSeconds)}" : "", retry + 1, MaxAttempts);
		else
			log.LogInformation("Playing {Title} ({Id}) for {User}{From}{Gain}", track.Title, track.Id, track.RequestedBy,
				file != null ? " from the library" : "", gain is { } g ? $", loudness {g:+0.0;-0.0} dB" : "");
	}

	// ---- SponsorBlock: jumping over the parts that aren't music ----

	// The segments came after the song had started: the next part to skip is set now.
	void SegmentsArrived(int gen, Track track)
	{
		lock (gate)
		{
			if (gen != generation || current != track || source is null) return;
			var segments = sponsorBlock.Known(track);
			// Still in a part at the start (the first seconds of the song): jump now.
			var at = PositionLocked();
			if (segments?.FirstOrDefault(s => s.Start <= at + 0.5 && s.End > at) is { } now)
			{
				SkipLocked(now);
				return;
			}
			SetSkipPointLocked(segments);
		}
		RaiseChanged();
	}

	// The audio thread tells when the song reaches the next part to skip.
	void SetSkipPointLocked(Segment[]? segments)
	{
		if (source is null || segments is null || current is null) return;
		var at = Math.Max(startOffset, PositionLocked());
		var next = segments.FirstOrDefault(s => s.Start > at + 0.5);
		source.Arm(next is null ? long.MaxValue : (long)((next.Start - startOffset) * BytesPerSecond));
	}

	void OnSkipPoint(TrackSource reached)
	{
		lock (gate)
		{
			if (reached.Generation != generation || current is null) return;
			var at = PositionLocked();
			var segments = sponsorBlock.Known(current);
			// Nothing to skip here (a kind was turned off meanwhile): wait for the next part.
			if (segments?.FirstOrDefault(s => s.Start <= at + 1 && s.End > at) is not { } part)
			{
				SetSkipPointLocked(segments);
				return;
			}
			SkipLocked(part);
		}
		RaiseChanged();
	}

	// To the end of the part; a part that runs to the end of the song ends it (as if it had played out).
	void SkipLocked(Segment part)
	{
		var song = current!;
		log.LogInformation("Skipping {From}–{To} of {Title} (SponsorBlock: {Category})",
			Strings.FormatTime(part.Start), Strings.FormatTime(part.End), song.Title, part.Category);
		var wasPaused = paused;
		if (part.End >= song.Duration - 2) AdvanceLocked(finished: true);
		else StartLocked(song, (int)Math.Ceiling(part.End), seeking: true);
		if (wasPaused && current == song) { paused = true; timePipe.Paused = true; }
	}

	void OnStreamTitle(int gen, Track track, string title)
	{
		lock (gate)
		{
			if (gen != generation || current != track || streamTitle == title) return;
			streamTitle = title;
		}
		log.LogInformation("{Station} plays {Title}", track.Title, title);
		try { StreamTitleChanged?.Invoke(track, title); }
		catch (Exception ex) { log.LogError(ex, "StreamTitleChanged handler failed"); }
		RaiseChanged();
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
		history.Add(new PlayedTrack(t.Id, t.Title, t.Channel, t.Duration, t.RequestedBy, startedAt, heard) { Source = t.Source, Link = t.Link, Art = t.Art });
	}

	int MaxAttempts => Math.Clamp(config.YouTube.PlayAttempts, 1, 10);

	void OnSourceEnded(TrackSource ended)
	{
		lock (gate)
		{
			if (ended.Generation != generation || current is null)
				return;
			// Less than a second of audio means yt-dlp/ffmpeg failed rather than the song ending,
			// unless it was started (seeked) right before its end.
			var remaining = current.IsLive ? int.MaxValue : current.Duration - startOffset;
			var played = Interlocked.Read(ref ended.BytesRead) / (double)BytesPerSecond;
			var noAudio = played < 1 && remaining > 3;
			// yt-dlp stopped with an error halfway through: YouTube broke the stream off.
			var cutOff = !noAudio && !ended.FromFile && ended.Errors.Contains("ERROR") && remaining - played > 5;
			var reason = YouTube.FriendlyError(ended.Errors);
			// A station that played for a while and then dropped gets all its tries again.
			if (cutOff && current.IsLive && played > 60) attempt = 0;
			if ((noAudio || cutOff) && attempt + 1 < MaxAttempts && (ended.FromFile || YouTube.IsRetryable(reason)))
			{
				log.LogWarning("{Title} ({Id}) {What}: {Reason}", current.Title, current.Id,
					cutOff ? $"broke off at {Strings.FormatTime(startOffset + played)}" : "did not start", ended.Errors.Trim());
				// A saved file that can't be played is broken; drop it, the next try downloads it again.
				if (ended.FromFile) library.Delete(current.Id);
				// A live stream goes on from "now": seeking a pipe decodes everything up to that point.
				RetryLocked(current.IsLive ? 0 : cutOff ? startOffset + (int)played : startOffset);
			}
			else if (noAudio && heardBefore >= 1)
			{
				// It broke off earlier and the tries ran out: it was played, partly; no error message.
				log.LogWarning("Gave up on {Title} ({Id}) after it broke off: {Reason}", current.Title, current.Id, ended.Errors.Trim());
				AddHistoryLocked(current);
				AdvanceAfterFailureLocked();
			}
			else if (noAudio)
			{
				var failed = current;
				log.LogWarning("Could not play {Id}: {Reason}", failed.Id, ended.Errors.Trim());
				if (ended.FromFile) library.Delete(failed.Id);
				AdvanceAfterFailureLocked();
				_ = Task.Run(() => TrackFailed?.Invoke(failed, reason));
			}
			else
			{
				autoFailures = 0;
				AdvanceLocked(finished: true);
			}
		}
		RaiseChanged();
	}

	// Starts the current song again after a second (YouTube often takes the next request), unless
	// something else happened meanwhile (a skip, a stop, another song).
	void RetryLocked(int at)
	{
		var track = current!;
		var next = attempt + 1;
		StopSourceLocked();
		startOffset = at;
		var gen = generation;
		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(1));
			lock (gate)
			{
				if (disposed || sessionSaved || gen != generation || current != track) return;
				var wasPaused = paused;
				try { StartLocked(track, at, seeking: at > 0, retry: next); }
				catch (Exception ex)
				{
					log.LogError(ex, "Could not try {Title} again", track.Title);
					AdvanceAfterFailureLocked();
				}
				// Paused during the wait: the new try waits too.
				if (wasPaused && current == track) { paused = true; timePipe.Paused = true; }
			}
			RaiseChanged();
		});
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
	// When it was autoplay's pick and nothing else comes, autoplay tries again (a few times).
	void AdvanceAfterFailureLocked()
	{
		var failed = current;
		var loop = Loop;
		current = null;
		Loop = LoopMode.Off;
		try { AdvanceLocked(finished: false); }
		finally { Loop = loop; }
		if (failed is { Autoplay: true } && current is null && settings.Bot.Autoplay && ++autoFailures <= 3)
			_ = Task.Run(() => FetchAutoplay(failed));
	}

	// ---- surviving a restart ----

	/// <summary>
	/// The queue and the song with its position, for <see cref="Restore"/> after Panda starts again.
	/// Only called when Panda stops: playback halts here. The song doesn't go into the history now;
	/// it does when it really ends.
	/// </summary>
	public PlayerSession SaveSession()
	{
		lock (gate)
		{
			sessionSaved = current != null;
			// Panda is stopping: hold the song here so it can't end (and move on) after being saved.
			timePipe.Paused = true;
			return new PlayerSession(current, PositionLocked(), paused, startedAt,
				heardBefore + SourceSecondsLocked(), queue.ToArray(), Loop);
		}
	}

	/// <summary>Puts back what <see cref="SaveSession"/> saved; playback continues once the bot is connected.</summary>
	public void Restore(PlayerSession s)
	{
		lock (gate)
		{
			if (disposed) return;
			queue.AddRange(s.Queue.Take(settings.Bot.MaxQueueLength));
			Loop = s.Loop;
			if (s.Current is { } t)
			{
				startedAt = s.StartedAt;
				heardBefore = s.Heard;
				var at = t.IsLive ? 0 : Math.Clamp((int)s.Position, 0, Math.Max(0, t.Duration - 1));
				StartLocked(t, at, seeking: true);
				if (current == t)
				{
					if (t.IsYouTube) library.Resumed(settings.Id, t);
					paused = true;
					timePipe.Paused = true;
					resumeOnConnect = !s.Paused;
				}
			}
		}
		RaiseChanged();
	}

	void RaiseChanged()
	{
		CheckAutoplay();
		Track[] upcoming;
		bool idle;
		lock (gate)
		{
			// A removed bot no longer counts for the library (BotManager has told it to forget it).
			if (disposed) return;
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
			if (disposed) return;
			// Shutting down mid-song still counts as a play, unless it goes on after a restart.
			if (current != null && !sessionSaved) AddHistoryLocked(current);
			current = null;
			StopSourceLocked();
			queue.Clear();
			disposed = true;
			autoGen++;
		}
		timePipe.Dispose();
	}

	/// <summary>Feeds ffmpeg's PCM into the timer pipe and notices when the song is over.</summary>
	sealed class TrackSource(AudioStream audio, int generation, bool fromFile, Action<TrackSource> ended, Action<TrackSource> skipPoint) : IAudioPassiveProducer
	{
		public bool FromFile => fromFile;
		public long BytesRead;
		public int Generation => generation;
		public string Errors => audio.Errors;
		/// <summary>When this much audio was read, the song reached a part to skip (SponsorBlock).</summary>
		long SkipAt = long.MaxValue;
		int done, skipped;

		public void Arm(long at)
		{
			Volatile.Write(ref SkipAt, at);
			Interlocked.Exchange(ref skipped, 0);
		}

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
			if (read > 0 && Interlocked.Add(ref BytesRead, read) >= Volatile.Read(ref SkipAt) && Interlocked.Exchange(ref skipped, 1) == 0)
				ThreadPool.QueueUserWorkItem(_ => skipPoint(this));
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
