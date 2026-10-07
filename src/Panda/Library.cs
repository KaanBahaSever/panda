using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Panda;

public sealed record LibraryItem(
	string Id, string Title, string Channel, int Duration, long Size,
	int Plays, DateTimeOffset? LastPlayed, DateTimeOffset Added)
{
	public string Url => $"https://www.youtube.com/watch?v={Id}";
	public string Thumbnail => $"https://i.ytimg.com/vi/{Id}/hqdefault.jpg";
}

public sealed record LibraryInfo(bool Enabled, long UsedBytes, long LimitBytes, int Count, IReadOnlyList<LibraryItem> Items);

/// <summary>
/// Songs saved on disk. Every requested song is downloaded in the background (the next ones in the
/// queue first), so songs start instantly and favourites never hit YouTube again. When the folder
/// grows over the limit, the least played songs are deleted first.
/// With saving turned off it still downloads the next song ahead of time and deletes it after use.
/// </summary>
public sealed class Library : IDisposable
{
	const int PrefetchWhenDisabled = 2;

	readonly PandaConfig config;
	readonly YouTube youtube;
	readonly ILogger<Library> log;
	readonly string dataDir;

	readonly object gate = new();
	Dictionary<string, Song> songs = new();
	Dictionary<string, Stat> stats = new();
	List<Track> wanted = [];
	string? nowPlaying;
	readonly HashSet<string> busy = []; // ids being downloaded right now
	readonly SemaphoreSlim wake = new(0);
	readonly CancellationTokenSource stop = new();
	CancellationTokenSource? currentDownload;
	string? currentDownloadId;
	Timer? saveTimer;

	public event Action? Changed;

	public Library(PandaConfig config, YouTube youtube, DataPaths paths, ILogger<Library> log)
	{
		this.config = config;
		this.youtube = youtube;
		this.log = log;
		dataDir = paths.Data;
		Directory.CreateDirectory(Dir);
		Directory.CreateDirectory(TempDir);
		Load();
		_ = Task.Run(DownloadLoop);
	}

	public bool Enabled => config.Library.Enabled;
	string Dir => config.Library.Path.Length > 0 ? config.Library.Path : Path.Combine(dataDir, "library");
	string TempDir => Path.Combine(Dir, ".tmp");
	string IndexPath => Path.Combine(Dir, "library.json");
	string SongPath(string id) => Path.Combine(Dir, id + ".audio");
	string PrefetchPath(string id) => Path.Combine(TempDir, id + ".audio");
	long LimitBytes => config.Library.MaxSizeMb <= 0 ? long.MaxValue : config.Library.MaxSizeMb * 1024L * 1024;

	// ---- lookups ----

	/// <summary>A local file for this song (saved or prefetched), or null.</summary>
	public string? GetFile(string id)
	{
		lock (gate)
		{
			if (songs.ContainsKey(id) && File.Exists(SongPath(id))) return SongPath(id);
		}
		var tmp = PrefetchPath(id);
		return File.Exists(tmp) ? tmp : null;
	}

	public Track? Find(string id, string requestedBy)
	{
		lock (gate)
			return songs.TryGetValue(id, out var s) ? s.ToTrack(requestedBy) : null;
	}

	/// <summary>Best saved song whose title/channel contains every word of the query.</summary>
	public Track? Search(string query, string requestedBy)
	{
		var words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0 || string.Concat(words).Length < 3) return null;
		lock (gate)
		{
			return songs.Values
				.Where(s => { var text = Normalize(s.Title + " " + s.Channel); return words.All(text.Contains); })
				.OrderByDescending(s => PlaysOf(s.Id))
				.FirstOrDefault()?.ToTrack(requestedBy);
		}
	}

	/// <summary>Like YouTube.ResolveAsync, but answers from the library when it can (no YouTube request).</summary>
	public async Task<Track> ResolveAsync(string input, string requestedBy, CancellationToken ct = default)
	{
		var clean = YouTube.CleanInput(input);
		var id = YouTube.ParseVideoId(clean);
		if (id != null && Find(id, requestedBy) is { } saved) return saved;
		if (id == null && !YouTube.LooksLikeUrl(clean) && Search(clean, requestedBy) is { } found) return found;
		return await youtube.ResolveAsync(clean, requestedBy, ct);
	}

	public LibraryInfo GetInfo(string? query = null, int max = 500)
	{
		lock (gate)
		{
			IEnumerable<Song> list = songs.Values;
			if (!string.IsNullOrWhiteSpace(query))
			{
				var words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
				list = list.Where(s => { var t = Normalize(s.Title + " " + s.Channel); return words.All(t.Contains); });
			}
			var items = list
				.OrderByDescending(s => PlaysOf(s.Id))
				.ThenByDescending(s => stats.GetValueOrDefault(s.Id)?.LastPlayed ?? s.Added)
				.Take(max)
				.Select(ToItem)
				.ToList();
			return new LibraryInfo(Enabled, UsedLocked(), config.Library.MaxSizeMb <= 0 ? 0 : LimitBytes, songs.Count, items);
		}
	}

	LibraryItem ToItem(Song s)
	{
		var st = stats.GetValueOrDefault(s.Id);
		return new(s.Id, s.Title, s.Channel, s.Duration, s.Size, st?.Plays ?? 0, st?.LastPlayed, s.Added);
	}

	int PlaysOf(string id) => stats.GetValueOrDefault(id)?.Plays ?? 0;
	long UsedLocked() => songs.Values.Sum(s => s.Size);

	// ---- play statistics ----

	public void MarkPlayed(Track t)
	{
		lock (gate)
		{
			nowPlaying = t.Id;
			if (!stats.TryGetValue(t.Id, out var st)) stats[t.Id] = st = new Stat();
			st.Plays++;
			st.LastPlayed = DateTimeOffset.UtcNow;
		}
		SaveSoon();
	}

	public void ClearNowPlaying()
	{
		lock (gate) nowPlaying = null;
		Signal();
	}

	public bool Delete(string id)
	{
		lock (gate)
		{
			if (!songs.Remove(id)) return false;
			TryDelete(SongPath(id));
		}
		SaveSoon();
		Changed?.Invoke();
		return true;
	}

	/// <summary>Called after the settings changed: applies a new limit or turns saving on/off.</summary>
	public void SettingsChanged()
	{
		lock (gate) EvictLocked();
		SaveSoon();
		Signal();
		Changed?.Invoke();
	}

	// ---- downloads ----

	/// <summary>The queue changed: download what will be played (all of it when saving is on).</summary>
	public void Want(IReadOnlyList<Track> queue)
	{
		lock (gate)
		{
			var list = queue.Where(t => !t.IsLive);
			wanted = (Enabled ? list : list.Take(PrefetchWhenDisabled)).ToList();
			// A prefetch nobody needs any more is cancelled when songs aren't being saved.
			if (!Enabled && currentDownloadId != null && wanted.All(t => t.Id != currentDownloadId))
				currentDownload?.Cancel();
		}
		Signal();
	}

	void Signal() { if (wake.CurrentCount == 0) wake.Release(); }

	async Task DownloadLoop()
	{
		var ct = stop.Token;
		while (!ct.IsCancellationRequested)
		{
			try { await wake.WaitAsync(TimeSpan.FromSeconds(30), ct); }
			catch (OperationCanceledException) { return; }

			CleanupPrefetched();
			while (!ct.IsCancellationRequested && NextToDownload() is { } track)
				await DownloadAsync(track, ct);
		}
	}

	Track? NextToDownload()
	{
		lock (gate)
		{
			var max = config.YouTube.MaxDurationMinutes;
			return wanted.FirstOrDefault(t =>
				!busy.Contains(t.Id)
				&& !(max > 0 && t.Duration > max * 60)
				&& !songs.ContainsKey(t.Id)
				&& !File.Exists(PrefetchPath(t.Id)));
		}
	}

	async Task DownloadAsync(Track track, CancellationToken stopToken)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
		lock (gate)
		{
			busy.Add(track.Id);
			currentDownload = cts;
			currentDownloadId = track.Id;
		}
		// Not ".part": yt-dlp treats that suffix specially and drops it.
		var part = Path.Combine(TempDir, track.Id + ".dl");
		try
		{
			var ok = await youtube.DownloadAsync(track, part, cts.Token);
			if (ok && File.Exists(part))
				Store(track, part);
			else
				TryDelete(part);
		}
		catch (OperationCanceledException) { TryDelete(part); }
		catch (Exception ex)
		{
			log.LogWarning("Download of {Id} failed: {Error}", track.Id, ex.Message);
			TryDelete(part);
		}
		finally
		{
			lock (gate)
			{
				busy.Remove(track.Id);
				currentDownload = null;
				currentDownloadId = null;
			}
		}
	}

	/// <summary>
	/// While a song is streamed straight from YouTube, the bytes are also written here so it ends up
	/// in the library without a second download. Returns null when that isn't wanted.
	/// </summary>
	public string? BeginTee(Track track)
	{
		if (track.IsLive || !Enabled) return null;
		lock (gate)
		{
			if (songs.ContainsKey(track.Id) || !busy.Add(track.Id)) return null;
		}
		return Path.Combine(TempDir, track.Id + ".tee");
	}

	public void EndTee(Track track, string path, bool complete)
	{
		try
		{
			if (complete && File.Exists(path) && new FileInfo(path).Length > 0) Store(track, path);
			else TryDelete(path);
		}
		finally
		{
			lock (gate) busy.Remove(track.Id);
			Signal();
		}
	}

	void Store(Track track, string file)
	{
		var size = new FileInfo(file).Length;
		lock (gate)
		{
			if (Enabled)
			{
				File.Move(file, SongPath(track.Id), overwrite: true);
				songs[track.Id] = new Song
				{
					Id = track.Id, Title = track.Title, Channel = track.Channel,
					Duration = track.Duration, Size = size, Added = DateTimeOffset.UtcNow,
				};
				EvictLocked();
				log.LogInformation("Saved {Title} ({Size:0.0} MB)", track.Title, size / 1048576.0);
			}
			else
			{
				File.Move(file, PrefetchPath(track.Id), overwrite: true);
				log.LogInformation("Ready ahead of time: {Title}", track.Title);
			}
		}
		SaveSoon();
		Changed?.Invoke();
	}

	// Deletes the least played songs (oldest first among equals) until the folder fits the limit.
	// Songs that are playing or waiting in the queue are kept.
	void EvictLocked()
	{
		var limit = LimitBytes;
		var used = UsedLocked();
		if (used <= limit) return;
		var keep = wanted.Select(t => t.Id).ToHashSet();
		if (nowPlaying != null) keep.Add(nowPlaying);
		var victims = songs.Values
			.Where(s => !keep.Contains(s.Id))
			.OrderBy(s => PlaysOf(s.Id))
			.ThenBy(s => stats.GetValueOrDefault(s.Id)?.LastPlayed ?? s.Added)
			.ToList();
		foreach (var v in victims)
		{
			if (used <= limit) break;
			songs.Remove(v.Id);
			TryDelete(SongPath(v.Id));
			used -= v.Size;
			log.LogInformation("Library full: removed {Title} ({Plays} plays)", v.Title, PlaysOf(v.Id));
		}
	}

	// Prefetched files (saving off) are only kept while they're still coming up or playing.
	void CleanupPrefetched()
	{
		HashSet<string> keep;
		lock (gate)
		{
			keep = wanted.Select(t => t.Id).ToHashSet();
			if (nowPlaying != null) keep.Add(nowPlaying);
			keep.UnionWith(busy);
		}
		foreach (var f in Directory.EnumerateFiles(TempDir, "*.audio"))
			if (!keep.Contains(Path.GetFileNameWithoutExtension(f)))
				TryDelete(f);
	}

	// ---- persistence ----

	void Load()
	{
		foreach (var f in Directory.EnumerateFiles(TempDir)) TryDelete(f);
		if (File.Exists(IndexPath))
		{
			try
			{
				var idx = JsonSerializer.Deserialize<IndexFile>(File.ReadAllText(IndexPath), JsonOpts);
				if (idx != null)
				{
					songs = idx.Songs.ToDictionary(s => s.Id);
					stats = idx.Stats;
				}
			}
			catch (Exception ex)
			{
				log.LogWarning("Could not read {Path}: {Error}", IndexPath, ex.Message);
			}
		}
		// Forget entries whose file is gone and files nobody knows about.
		foreach (var id in songs.Keys.Where(id => !File.Exists(SongPath(id))).ToList())
			songs.Remove(id);
		foreach (var f in Directory.EnumerateFiles(Dir, "*.audio"))
			if (!songs.ContainsKey(Path.GetFileNameWithoutExtension(f)))
				TryDelete(f);
	}

	void SaveSoon()
	{
		lock (gate)
		{
			saveTimer ??= new Timer(_ => Save(), null, Timeout.Infinite, Timeout.Infinite);
			saveTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
		}
	}

	void Save()
	{
		try
		{
			string json;
			lock (gate)
				json = JsonSerializer.Serialize(new IndexFile { Songs = songs.Values.ToList(), Stats = stats }, JsonOpts);
			var tmp = IndexPath + ".tmp";
			File.WriteAllText(tmp, json);
			File.Move(tmp, IndexPath, overwrite: true);
		}
		catch (Exception ex)
		{
			log.LogWarning("Could not save the library index: {Error}", ex.Message);
		}
	}

	static void TryDelete(string path)
	{
		try { File.Delete(path); } catch { }
	}

	// The app runs in invariant globalization mode, where string.Normalize can't strip accents,
	// so the common accented letters are folded by hand.
	const string Accented = "şçğüöıiâäàáãåêëèéîïìíôòóõûùúñßøæœ";
	const string Plain    = "scguoiiaaaaaaeeeeiiiioooouuunsoao";

	/// <summary>Lower case without accents, so "simarik" finds "Şımarık".</summary>
	public static string Normalize(string s)
	{
		var lower = s.ToLowerInvariant();
		var sb = new StringBuilder(lower.Length);
		foreach (var c in lower)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
			var i = Accented.IndexOf(c);
			if (i >= 0) sb.Append(Plain[i]);
			else sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
		}
		return sb.ToString();
	}

	public void Dispose()
	{
		stop.Cancel();
		saveTimer?.Dispose();
		Save();
	}

	static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	sealed class IndexFile
	{
		public List<Song> Songs { get; set; } = [];
		public Dictionary<string, Stat> Stats { get; set; } = new();
	}

	sealed class Song
	{
		public string Id { get; set; } = "";
		public string Title { get; set; } = "";
		public string Channel { get; set; } = "";
		public int Duration { get; set; }
		public long Size { get; set; }
		public DateTimeOffset Added { get; set; }
		public Track ToTrack(string requestedBy) => new(Id, Title, Channel, Duration, requestedBy);
	}

	sealed class Stat
	{
		public int Plays { get; set; }
		public DateTimeOffset? LastPlayed { get; set; }
	}
}
