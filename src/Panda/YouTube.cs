using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Panda;

public sealed record Track(
	string Id,
	string Title,
	string Channel,
	/// <summary>Seconds; 0 for live streams.</summary>
	int Duration,
	string RequestedBy)
{
	/// <summary>Where it plays from: null for YouTube (the id is the video's), <see cref="Media.Link"/> for
	/// another site (through yt-dlp), <see cref="Media.Direct"/> for an audio file or an HLS stream (ffmpeg
	/// reads it), <see cref="Media.Radio"/> for an internet radio stream (Icecast/Shoutcast).</summary>
	public string? Source { get; init; }
	/// <summary>The page, file or stream of a song that isn't on YouTube.</summary>
	public string? Link { get; init; }
	/// <summary>Cover picture of a song that isn't on YouTube (YouTube's comes from the id).</summary>
	public string? Art { get; init; }
	public string Url => Media.Url(Id, Source, Link);
	public string? Thumbnail => Media.Thumbnail(Id, Source, Art);
	public bool IsLive => Duration <= 0;
	public bool IsYouTube => Source is null;
	/// <summary>Picked by autoplay rather than asked for by someone.</summary>
	public bool Autoplay { get; init; }
	/// <summary>Up to 0.5 autoplay was called radio; a session saved by 0.5 still says so.</summary>
	[JsonPropertyName("radio"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool OldRadio { get => false; init { if (value) Autoplay = true; } }
	/// <summary>Who asked for it, for the songs-per-person limit: a TeamSpeak unique id or "panel:name".</summary>
	public string? RequesterId { get; init; }
}

/// <summary>Links and pictures of songs from anywhere; YouTube's come from the video id.</summary>
public static class Media
{
	public const string Link = "link";
	public const string Direct = "direct";
	public const string Radio = "radio";

	public static string Url(string id, string? source, string? link) =>
		source is null ? $"https://www.youtube.com/watch?v={id}" : link ?? "";

	public static string? Thumbnail(string id, string? source, string? art) =>
		source is null ? $"https://i.ytimg.com/vi/{id}/hqdefault.jpg" : art;

	/// <summary>A YouTube video id; anything else (other sites, radio) has a "kind:..." id.</summary>
	public static bool IsVideoId(string id) =>
		id.Length == 11 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

	public static bool IsWebLink(string? s) =>
		s is { Length: > 0 and <= 2000 } && Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https";
}

public sealed class YouTubeException(string message) : Exception(message);

/// <summary>Everything YouTube goes through yt-dlp: search, metadata and the audio stream.</summary>
public sealed partial class YouTube(PandaConfig config, ILogger<YouTube> log)
{
	// watch?v=, youtu.be/, shorts/, embed/, music.youtube.com, m.youtube.com; extra params like
	// &list= or &si= are ignored so a playlist link only plays its main song.
	[GeneratedRegex(@"(?:https?://)?(?:www\.|m\.|music\.)?(?:youtube\.com/(?:watch\?(?:\S*?&)?v=|shorts/|embed/|live/)|youtu\.be/)([\w-]{11})")]
	private static partial Regex VideoLinkRegex();

	[GeneratedRegex(@"youtube\.com/playlist\?|[?&]list=")]
	private static partial Regex PlaylistRegex();

	/// <summary>TeamSpeak wraps links in [URL]...[/URL]; strip any BBCode.</summary>
	[GeneratedRegex(@"\[/?[a-zA-Z]+(?:=[^\]]*)?\]")]
	private static partial Regex BbCodeRegex();

	public static string CleanInput(string input) => BbCodeRegex().Replace(input, "").Trim();

	public static bool LooksLikeUrl(string input) =>
		input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
		|| input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
		|| input.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
		|| input.StartsWith("youtu", StringComparison.OrdinalIgnoreCase)
		|| input.StartsWith("music.youtube", StringComparison.OrdinalIgnoreCase)
		|| BareLinkRegex().IsMatch(input);

	// "soundcloud.com/artist/song", "open.spotify.com/track/...": a host and a path, no spaces.
	[GeneratedRegex(@"^(?:[a-z0-9-]+\.)+[a-z]{2,}/\S*$", RegexOptions.IgnoreCase)]
	private static partial Regex BareLinkRegex();

	[GeneratedRegex(@"^(?:https?://)?(?:www\.|m\.|music\.)?(?:youtube\.com|youtu\.be)/", RegexOptions.IgnoreCase)]
	private static partial Regex YouTubeHostRegex();

	public static bool IsYouTubeLink(string input) => YouTubeHostRegex().IsMatch(input);

	public static string? ParseVideoId(string input)
	{
		var m = VideoLinkRegex().Match(input);
		return m.Success ? m.Groups[1].Value : null;
	}

	/// <summary>Turns a link or search words into a playable track.</summary>
	public async Task<Track> ResolveAsync(string input, string requestedBy, CancellationToken ct = default)
	{
		input = CleanInput(input);
		if (input.Length == 0)
			throw new YouTubeException("empty");

		Track track;
		var id = ParseVideoId(input);
		if (id != null)
		{
			var json = await RunJsonAsync(["--no-playlist", "--skip-download", $"https://www.youtube.com/watch?v={id}"], ct);
			track = FromJson(json, requestedBy);
		}
		else if (YouTubeHostRegex().IsMatch(input))
		{
			// A playlist or album link without a song in it: take only its first song,
			// opening the whole list would be slow and costly.
			var url = input.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? input : "https://" + input;
			var json = await RunJsonAsync(["--flat-playlist", "--playlist-items", "1", url], ct);
			var first = json.TryGetProperty("entries", out var entries)
				? entries.EnumerateArray().FirstOrDefault()
				: json;
			if (first.ValueKind != JsonValueKind.Object || !first.TryGetProperty("id", out var fid) || fid.GetString() is not { Length: 11 } firstId)
				throw new YouTubeException(PlaylistRegex().IsMatch(input) ? "playlist-empty" : "not-found");
			track = FromJson(first, requestedBy);
			if (track.Title.Length == 0)
				track = FromJson(await RunJsonAsync(["--no-playlist", "--skip-download", $"https://www.youtube.com/watch?v={firstId}"], ct), requestedBy);
		}
		else if (LooksLikeUrl(input))
		{
			throw new YouTubeException("not-youtube");
		}
		else
		{
			var results = await SearchAsync(input, 1, requestedBy, ct);
			track = results.FirstOrDefault() ?? throw new YouTubeException("not-found");
		}

		CheckLength(track);
		return track;
	}

	/// <summary>Turns down songs over the "max song length" setting, wherever they come from.</summary>
	public void CheckLength(Track track)
	{
		if (TooLong(track)) throw new YouTubeException("too-long");
	}

	public bool TooLong(Track track)
	{
		var max = config.YouTube.MaxDurationMinutes;
		return max > 0 && track.Duration > max * 60;
	}

	public async Task<List<Track>> SearchAsync(string query, int count, string requestedBy, CancellationToken ct = default)
	{
		query = CleanInput(query);
		if (query.Length == 0)
			return [];
		// --flat-playlist only reads the search page, which is much faster than resolving every video.
		var json = await RunJsonAsync(["--flat-playlist", $"ytsearch{Asked(count)}:{query}"], ct);
		return Filtered(VideoEntries(json, requestedBy), count);
	}

	/// <summary>YouTube Music's "Songs": the official audio rather than a video clip.</summary>
	public async Task<List<Track>> MusicSearchAsync(string query, int count, string requestedBy, CancellationToken ct = default)
	{
		query = CleanInput(query);
		if (query.Length == 0)
			return [];
		var json = await RunJsonAsync(["--flat-playlist", "--playlist-end", Asked(count).ToString(),
			$"https://music.youtube.com/search?q={Uri.EscapeDataString(query)}#songs"], ct);
		// The list only names the songs (no length, no artist): each one is asked for. One song: the first
		// that passes the filters; a list: up to 8, a few at a time.
		var entries = VideoEntries(json, requestedBy);
		async Task<Track?> Full(Track t)
		{
			try { return FromJson(await RunJsonAsync(["--no-playlist", "--skip-download", t.Url], ct), requestedBy); }
			catch (YouTubeException ex)
			{
				log.LogInformation("YouTube Music: could not look up {Id}: {Error}", t.Id, ex.Message);
				return null;
			}
		}
		if (count <= 1)
		{
			foreach (var e in entries)
				if (await Full(e) is { } t && Filtered([t], 1).Count == 1) return [t];
			return [];
		}
		using var gate = new SemaphoreSlim(4);
		var full = await Task.WhenAll(entries.Take(8).Select(async t =>
		{
			await gate.WaitAsync(ct);
			try { return await Full(t); }
			finally { gate.Release(); }
		}));
		return Filtered(full.OfType<Track>(), count);
	}

	/// <summary>A search on another site yt-dlp can search ("scsearch" = SoundCloud).</summary>
	public async Task<List<Track>> SiteSearchAsync(string prefix, string query, int count, string requestedBy, CancellationToken ct = default)
	{
		query = CleanInput(query);
		if (query.Length == 0)
			return [];
		var json = await RunJsonAsync(["--flat-playlist", $"{prefix}{Asked(count)}:{query}"], ct, youtube: false);
		var list = new List<Track>();
		if (json.TryGetProperty("entries", out var entries))
			foreach (var e in entries.EnumerateArray())
				if (FromSiteJson(e, requestedBy, null) is { } t) list.Add(t);
		return Filtered(list, count);
	}

	/// <summary>A link to another site yt-dlp knows (SoundCloud, Bandcamp, Mixcloud, Vimeo, Twitch...):
	/// the song, or the first one of an album or set.</summary>
	public async Task<Track> ResolveSiteAsync(string url, string requestedBy, CancellationToken ct = default)
	{
		var json = await RunJsonAsync(["--no-playlist", "--playlist-items", "1", "--skip-download", url], ct, youtube: false);
		var first = json.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().FirstOrDefault() : json;
		if (first.ValueKind != JsonValueKind.Object || FromSiteJson(first, requestedBy, url) is not { } track)
			throw new YouTubeException("not-found");
		// A list only named its songs: ask for the song itself (length, picture).
		if (track.Title.Length == 0 || (track.Duration == 0 && !IsLiveJson(first)))
			if (FromSiteJson(await RunJsonAsync(["--no-playlist", "--skip-download", track.Url], ct, youtube: track.IsYouTube), requestedBy, track.Url) is { } full)
				track = full;
		CheckLength(track);
		return track;
	}

	// Filters drop some results, so a few more are asked for.
	int Asked(int count) =>
		Math.Clamp(config.YouTube.HideLive || config.YouTube.HideShort ? count + 5 : count, 1, 20);

	List<Track> Filtered(IEnumerable<Track> list, int count) => list
		.Where(t => !(config.YouTube.HideLive && t.IsLive) && !(config.YouTube.HideShort && t.Duration is > 0 and < 60))
		.Take(Math.Max(1, count)).ToList();

	static List<Track> VideoEntries(JsonElement json, string requestedBy)
	{
		var list = new List<Track>();
		if (json.TryGetProperty("entries", out var entries))
			foreach (var e in entries.EnumerateArray())
				if (e.TryGetProperty("id", out var idEl) && idEl.GetString() is { Length: 11 })
					list.Add(FromJson(e, requestedBy));
		return list;
	}

	static bool IsLiveJson(JsonElement j) =>
		(j.TryGetProperty("is_live", out var l) && l.ValueKind == JsonValueKind.True)
		|| (j.TryGetProperty("live_status", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() == "is_live");

	/// <summary>A song from yt-dlp's answer about another site; a YouTube video stays a YouTube song.</summary>
	static Track? FromSiteJson(JsonElement j, string requestedBy, string? fallbackUrl)
	{
		string Str(string name) => j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
		var site = Str("extractor_key") is { Length: > 0 } k ? k : Str("ie_key");
		var id = Str("id");
		if (site.Equals("Youtube", StringComparison.OrdinalIgnoreCase) && Media.IsVideoId(id))
			return FromJson(j, requestedBy);
		var link = new[] { Str("webpage_url"), Str("original_url"), Str("url"), fallbackUrl }.FirstOrDefault(Media.IsWebLink);
		if (link is null) return null;
		var duration = j.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? (int)d.GetDouble() : 0;
		if (IsLiveJson(j)) duration = 0;
		var channel = new[] { Str("artist"), Str("uploader"), Str("channel"), Str("creator") }.FirstOrDefault(s => s.Length > 0) ?? "";
		var art = Str("thumbnail");
		if (!Media.IsWebLink(art) && j.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
			art = thumbs.EnumerateArray().Select(t => t.TryGetProperty("url", out var u) ? u.GetString() : null).LastOrDefault(Media.IsWebLink) ?? "";
		var key = (site.Length > 0 ? site.ToLowerInvariant() : "link") + ":" + (id.Length > 0 ? id : Hash(link));
		return new Track(key.Length > 100 ? key[..100] : key, Str("title"), channel, duration, requestedBy)
		{
			Source = Media.Link,
			Link = link,
			Art = Media.IsWebLink(art) ? art : null,
		};
	}

	internal static string Hash(string s) =>
		Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16].ToLowerInvariant();

	[GeneratedRegex(@"Duration:\s+(\d+):(\d\d):(\d\d)")]
	private static partial Regex FfDurationRegex();

	[GeneratedRegex(@"^\s+(title|artist)\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
	private static partial Regex FfTagRegex();

	/// <summary>An audio file on the web, as ffmpeg sees it: its length and its title/artist tags.</summary>
	public async Task<Track> ProbeFileAsync(string url, string requestedBy, CancellationToken ct = default)
	{
		var psi = new ProcessStartInfo(config.YouTube.FfmpegPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in (string[])["-hide_banner", "-nostdin", .. LinkInput(), "-i", url])
			psi.ArgumentList.Add(a);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(30));
		using var proc = StartProcess(psi);
		using var reg = timeout.Token.Register(() => Kill(proc));
		var stderr = proc.StandardError.ReadToEndAsync(CancellationToken.None);
		_ = proc.StandardOutput.ReadToEndAsync(CancellationToken.None);
		await proc.WaitForExitAsync(CancellationToken.None);
		if (timeout.IsCancellationRequested) throw new YouTubeException("timeout");
		var text = await stderr;
		// ffmpeg says "at least one output file must be specified": it only read the file's head.
		if (!text.Contains("Stream #")) throw new YouTubeException("link-unplayable");
		var m = FfDurationRegex().Match(text);
		var duration = m.Success ? int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value) : 0;
		var tags = FfTagRegex().Matches(text).GroupBy(x => x.Groups[1].Value.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Groups[2].Value.Trim());
		var name = Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath));
		var track = new Track("direct:" + Hash(url), tags.GetValueOrDefault("title") is { Length: > 0 } t ? t : name.Length > 0 ? name : new Uri(url).Host,
			tags.GetValueOrDefault("artist") ?? new Uri(url).Host, duration, requestedBy)
		{
			Source = Media.Direct,
			Link = url,
		};
		CheckLength(track);
		return track;
	}

	/// <summary>
	/// Songs like this one: YouTube's automatic "Mix" playlist for the video (it starts with the video
	/// itself). Empty when YouTube has no Mix for it, e.g. for very new or non-music videos.
	/// </summary>
	public async Task<List<Track>> MixAsync(string videoId, int max, CancellationToken ct = default)
	{
		var json = await RunJsonAsync(["--flat-playlist", "--playlist-end", Math.Clamp(max, 1, 100).ToString(),
			$"https://www.youtube.com/watch?v={videoId}&list=RD{videoId}"], ct);
		var list = new List<Track>();
		if (json.TryGetProperty("entries", out var entries))
			foreach (var e in entries.EnumerateArray())
				if (e.TryGetProperty("id", out var idEl) && idEl.GetString() is { Length: 11 })
					list.Add(FromJson(e, ""));
		return list;
	}

	static Track FromJson(JsonElement j, string requestedBy)
	{
		string Str(string name) => j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
		var duration = j.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? (int)d.GetDouble() : 0;
		if (j.TryGetProperty("is_live", out var live) && live.ValueKind == JsonValueKind.True)
			duration = 0;
		var channel = Str("channel");
		if (channel.Length == 0) channel = Str("uploader");
		return new Track(Str("id"), Str("title"), channel, duration, requestedBy);
	}

	// The YouTube cookies only go to YouTube; other sites go through the link proxy (no local network).
	List<string> BaseArgs(bool youtube = true)
	{
		var args = new List<string> { "--ignore-config", "--no-warnings", "--no-progress" };
		var cookies = config.YouTube.CookiesFile;
		if (youtube && cookies.Length > 0 && File.Exists(cookies))
			args.AddRange(["--cookies", cookies]);
		if (!youtube && LinkProxy.Url is { } proxy)
			args.AddRange(["--proxy", proxy]);
		return args;
	}

	// YouTube turns a request down now and then (403, a bot check) and takes the next one: one more try.
	async Task<JsonElement> RunJsonAsync(IReadOnlyList<string> extra, CancellationToken ct, bool youtube = true)
	{
		try { return await RunJsonOnceAsync(extra, ct, youtube); }
		catch (YouTubeException ex) when (ex.Message != "timeout" && IsRetryable(ex.Message))
		{
			log.LogInformation("yt-dlp failed ({Reason}); trying again", ex.Message);
			await Task.Delay(TimeSpan.FromSeconds(1), ct);
			return await RunJsonOnceAsync(extra, ct, youtube);
		}
	}

	async Task<JsonElement> RunJsonOnceAsync(IReadOnlyList<string> extra, CancellationToken ct, bool youtube)
	{
		var psi = new ProcessStartInfo(config.YouTube.YtDlpPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			StandardOutputEncoding = Encoding.UTF8,
		};
		foreach (var a in BaseArgs(youtube)) psi.ArgumentList.Add(a);
		psi.ArgumentList.Add("-J");
		foreach (var a in extra) psi.ArgumentList.Add(a);

		// yt-dlp can hang on a stalled connection; a chat command must not wait forever.
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(MetadataTimeout);
		using var proc = StartProcess(psi);
		using var reg = timeout.Token.Register(() => Kill(proc));
		string output;
		Task<string> stderr;
		try
		{
			var stdout = proc.StandardOutput.ReadToEndAsync(timeout.Token);
			stderr = proc.StandardError.ReadToEndAsync(timeout.Token);
			await proc.WaitForExitAsync(timeout.Token);
			output = await stdout;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			throw new YouTubeException("timeout");
		}
		if (proc.ExitCode != 0 || output.Length == 0)
			throw new YouTubeException(FriendlyError(await stderr));
		using var doc = JsonDocument.Parse(output);
		return doc.RootElement.Clone();
	}

	static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Starts yt-dlp (download to stdout) piped into ffmpeg (decode to 48 kHz stereo s16le PCM).
	/// yt-dlp handles YouTube's chunked downloads and throttling; ffmpeg only decodes.
	/// </summary>
	/// <param name="teePath">Also write the downloaded bytes to this file (to save the song).</param>
	/// <param name="teeDone">Called when the download ended; true when it completed.</param>
	/// <param name="attempt">0 for the first try; from the third try on other YouTube clients are asked.</param>
	/// <param name="sound">Nightcore, slowed, the speed: ffmpeg filters (null: none).</param>
	public AudioStream OpenAudio(Track track, int startSeconds = 0, string? teePath = null, Action<bool>? teeDone = null, int attempt = 0, Sound? sound = null)
	{
		var yt = new ProcessStartInfo(config.YouTube.YtDlpPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in BaseArgs(track.IsYouTube)) yt.ArgumentList.Add(a);
		if (track.IsYouTube && attempt >= 2 && (config.YouTube.RetryPlayerClients ?? "").Trim() is { Length: > 0 } clients)
			foreach (var a in new[] { "--extractor-args", $"youtube:player_client={clients}" })
				yt.ArgumentList.Add(a);
		foreach (var a in new[] { "--quiet", "--no-playlist", "-f", Format(track), "-o", "-", track.Url })
			yt.ArgumentList.Add(a);

		// Seeking a pipe means decoding up to that point, so -ss goes after the input here.
		// Here -ss cuts the output, after the filters: in heard seconds, not song seconds.
		var ff = FfmpegInfo(["-i", "pipe:0", .. SeekArgs(startSeconds / (sound?.Rate ?? 1))], stdin: true, af: sound?.Filter);
		// Only this one: other calls get paths that may be relative to Panda's own folder.
		if (Directory.Exists(WorkDir.Value)) yt.WorkingDirectory = WorkDir.Value;
		var ytProc = StartProcess(yt);
		Process ffProc;
		try { ffProc = StartProcess(ff); }
		catch { Kill(ytProc); ytProc.Dispose(); throw; }
		return new AudioStream(ytProc, ffProc, log, teePath, teeDone);
	}

	// "saver": YouTube's ~70 kbps Opus instead of ~130–160 kbps; the bot sends 96 kbps anyway.
	string Format(Track track) =>
		track.IsYouTube && config.YouTube.Quality == "saver" ? "bestaudio[abr<=80]/worstaudio/best" : "bestaudio/best";

	// ffmpeg reading a link people sent: web protocols only (no file:, concat:...), and every connection,
	// HLS pieces too, through the link proxy.
	static string[] LinkInput() => LinkProxy.Url is { } proxy
		? ["-protocol_whitelist", "http,https,tls,tcp,crypto,httpproxy", "-http_proxy", proxy]
		: ["-protocol_whitelist", "http,https,tls,tcp,crypto,httpproxy"];

	/// <summary>An audio file or an HLS stream on the web, read by ffmpeg itself (it can seek in files).</summary>
	public AudioStream OpenUrl(Track track, int startSeconds = 0, Sound? sound = null) =>
		new(null, StartProcess(FfmpegInfo([.. LinkInput(), "-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5",
			.. SeekArgs(startSeconds), "-i", track.Url], stdin: false, af: sound?.Filter)), log, ffErrors: true);

	/// <summary>
	/// An internet radio stream (Icecast/Shoutcast), read by Panda and decoded by ffmpeg: the song titles
	/// the station sends between the audio go to <paramref name="title"/>.
	/// </summary>
	public AudioStream OpenRadio(Track track, Action<string> title, Sound? sound = null) =>
		AudioStream.Radio(track.Url, StartProcess(FfmpegInfo(["-i", "pipe:0"], stdin: true, af: sound?.Filter)), log, title);

	/// <summary>Plays a saved song; seeking in a file is instant.</summary>
	/// <param name="gainDb">Loudness correction for this song, if it was measured.</param>
	public AudioStream OpenFile(string path, int startSeconds = 0, double? gainDb = null, Sound? sound = null) =>
		new(null, StartProcess(FfmpegInfo([.. SeekArgs(startSeconds), "-i", path], stdin: false, gainDb, sound?.Filter)), log);

	static string[] SeekArgs(double seconds) => seconds > 0 ? ["-ss", seconds.ToString("0.###", CultureInfo.InvariantCulture)] : [];

	ProcessStartInfo FfmpegInfo(string[] input, bool stdin, double? gainDb = null, string? af = null)
	{
		var ff = new ProcessStartInfo(config.YouTube.FfmpegPath)
		{
			RedirectStandardInput = stdin,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		var filters = new List<string>();
		if (gainDb is { } g && Math.Abs(g) >= 0.1) filters.Add($"volume={g.ToString("0.0", CultureInfo.InvariantCulture)}dB");
		if (af is { Length: > 0 }) filters.Add(af);
		string[] filter = filters.Count > 0 ? ["-af", string.Join(',', filters)] : [];
		foreach (var a in (string[])["-hide_banner", "-loglevel", "error", .. input, "-vn", .. filter, "-f", "s16le", "-ar", "48000", "-ac", "2", "pipe:1"])
			ff.ArgumentList.Add(a);
		return ff;
	}

	[GeneratedRegex(@"I:\s+(-?\d+(?:\.\d+)?) LUFS")]
	private static partial Regex LoudnessRegex();

	[GeneratedRegex(@"Peak:\s+(-?\d+(?:\.\d+)?) dBFS")]
	private static partial Regex PeakRegex();

	/// <summary>
	/// Integrated loudness (LUFS) and sample peak (dBFS) of a song file, by ffmpeg's EBU R128 meter.
	/// Fast (hundreds of times real time) and run at low priority. Null for silence or errors.
	/// </summary>
	public async Task<(double Lufs, double Peak)?> MeasureAsync(string path, CancellationToken ct)
	{
		var psi = new ProcessStartInfo(config.YouTube.FfmpegPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in new[] { "-hide_banner", "-nostats", "-threads", "1", "-i", path, "-vn", "-af", "ebur128=peak=sample:framelog=quiet", "-f", "null", "-" })
			psi.ArgumentList.Add(a);
		using var proc = StartProcess(psi);
		try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
		using var reg = ct.Register(() => Kill(proc));
		var stderr = proc.StandardError.ReadToEndAsync(CancellationToken.None);
		_ = proc.StandardOutput.ReadToEndAsync(CancellationToken.None);
		await proc.WaitForExitAsync(CancellationToken.None);
		ct.ThrowIfCancellationRequested();
		var text = await stderr;
		// The summary comes last; the meter also prints "I:" while running, so take the last match.
		var i = LoudnessRegex().Matches(text).LastOrDefault();
		var p = PeakRegex().Matches(text).LastOrDefault();
		if (proc.ExitCode != 0 || i is null || p is null) return null;
		var lufs = double.Parse(i.Groups[1].Value, CultureInfo.InvariantCulture);
		return lufs <= -70 ? null : (lufs, double.Parse(p.Groups[1].Value, CultureInfo.InvariantCulture));
	}

	/// <summary>Downloads the song's audio as-is (no re-encoding) into <paramref name="path"/>.</summary>
	public async Task<bool> DownloadAsync(Track track, string path, CancellationToken ct)
	{
		var psi = new ProcessStartInfo(config.YouTube.YtDlpPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in BaseArgs()) psi.ArgumentList.Add(a);
		foreach (var a in new[] { "--quiet", "--no-playlist", "--no-part", "-f", Format(track), "-o", path, track.Url })
			psi.ArgumentList.Add(a);

		using var proc = StartProcess(psi);
		using var reg = ct.Register(() => Kill(proc));
		var stderr = proc.StandardError.ReadToEndAsync(CancellationToken.None);
		_ = proc.StandardOutput.ReadToEndAsync(CancellationToken.None);
		await proc.WaitForExitAsync(CancellationToken.None);
		ct.ThrowIfCancellationRequested();
		if (proc.ExitCode != 0)
			log.LogWarning("Could not download {Id}: {Error}", track.Id, FriendlyError(await stderr));
		return proc.ExitCode == 0;
	}

	// yt-dlp sending an HLS stream (SoundCloud, Twitch...) to stdout keeps its pieces ("--Frag3") in the
	// folder it runs in: a folder of its own, emptied when Panda starts.
	static readonly Lazy<string> WorkDir = new(() =>
	{
		var dir = Path.Combine(Path.GetTempPath(), $"panda-ytdlp-{Environment.UserName}");
		try
		{
			if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
			Directory.CreateDirectory(dir);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		return dir;
	});

	Process StartProcess(ProcessStartInfo psi)
	{
		try
		{
			return Process.Start(psi) ?? throw new YouTubeException($"could not start {psi.FileName}");
		}
		catch (System.ComponentModel.Win32Exception)
		{
			log.LogError("{File} was not found. Install it or set its path in config.json.", psi.FileName);
			throw new YouTubeException($"missing:{Path.GetFileName(psi.FileName)}");
		}
	}

	internal static void Kill(Process p)
	{
		try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
	}

	/// <summary>
	/// Whether another try may work: YouTube refusing or breaking off (403, a bot check, a timeout)
	/// often passes, a missing, private or too long video never does.
	/// </summary>
	public static bool IsRetryable(string reason) =>
		reason is not ("unavailable" or "age" or "not-found" or "empty" or "playlist-empty" or "not-youtube" or "too-long"
			or "link-private" or "link-unplayable" or "an HLS stream")
		&& !reason.StartsWith("missing:") && !reason.StartsWith("Unsupported URL", StringComparison.OrdinalIgnoreCase);

	/// <summary>Maps yt-dlp's stderr to a short reason the bot can show.</summary>
	public static string FriendlyError(string stderr)
	{
		if (stderr.Contains("confirm you", StringComparison.OrdinalIgnoreCase) || stderr.Contains("Sign in to confirm"))
			return "bot-check";
		// Age-gated videos also say "unavailable", so check this first.
		if (stderr.Contains("confirm your age", StringComparison.OrdinalIgnoreCase) || stderr.Contains("age-restricted", StringComparison.OrdinalIgnoreCase))
			return "age";
		if (stderr.Contains("Private video") || stderr.Contains("Video unavailable") || stderr.Contains("not available"))
			return "unavailable";
		// Missing or private playlists/channels ("[youtube:tab] ...").
		if (stderr.Contains("does not exist", StringComparison.OrdinalIgnoreCase) || stderr.Contains("[youtube:tab]"))
			return "not-found";
		var line = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(l => l.Contains("ERROR")) ?? "";
		return line.Length > 0 ? line.Replace("ERROR: ", "").Trim() : "failed";
	}
}

/// <summary>
/// PCM audio of one track. From YouTube: yt-dlp stdout is pumped into ffmpeg stdin (and optionally
/// into a file, to save the song). From disk: ffmpeg reads the file. ffmpeg stdout is the PCM.
/// </summary>
public sealed class AudioStream : IDisposable
{
	readonly Process? yt;
	readonly Process ff;
	readonly bool teeing;
	readonly StringBuilder ytErrors = new();
	public Stream Pcm => ff.StandardOutput.BaseStream;

	readonly CancellationTokenSource? radioStop;

	/// <param name="ffErrors">ffmpeg reads the source itself (a link): its errors count like yt-dlp's.</param>
	public AudioStream(Process? yt, Process ff, ILogger log, string? teePath = null, Action<bool>? teeDone = null, bool ffErrors = false)
	{
		this.yt = yt;
		this.ff = ff;
		teeing = yt != null && teePath != null;
		if (yt != null)
		{
			_ = Task.Run(() => Pump(yt, teePath, teeDone));
			_ = Task.Run(async () =>
			{
				try
				{
					string? line;
					while ((line = await yt.StandardError.ReadLineAsync()) != null)
						lock (ytErrors) ytErrors.AppendLine(line);
				}
				catch { }
			});
		}
		_ = Task.Run(async () =>
		{
			try
			{
				string? line;
				while ((line = await ff.StandardError.ReadLineAsync()) != null)
				{
					if (ffErrors) lock (ytErrors) ytErrors.AppendLine("ERROR: " + line);
					log.LogDebug("ffmpeg: {Line}", line);
				}
			}
			catch { }
		});
	}

	AudioStream(Process ff, ILogger log, CancellationTokenSource stop) : this(null, ff, log) { radioStop = stop; }

	/// <summary>An internet radio stream read by Panda into ffmpeg; see <see cref="YouTube.OpenRadio"/>.</summary>
	public static AudioStream Radio(string url, Process ff, ILogger log, Action<string> title)
	{
		var s = new AudioStream(ff, log, new CancellationTokenSource());
		_ = Task.Run(() => s.PumpRadio(url, title, s.radioStop!.Token));
		return s;
	}

	static readonly TimeSpan RadioSilence = TimeSpan.FromSeconds(30);

	// Audio goes to ffmpeg; every metaint bytes the station puts a block of text in between
	// ("StreamTitle='Artist - Song';"), which is taken out. An end of the stream is an error: a station
	// never ends, so the player tries again.
	async Task PumpRadio(string url, Action<string> title, CancellationToken ct)
	{
		void Error(string what) { lock (ytErrors) ytErrors.AppendLine("ERROR: " + what); }
		try
		{
			var http = await HttpStream.OpenAsync(url, ct);
			if (RadioLinks.IsPlaylist(http.Type, http.Url))
			{
				// A .m3u or .pls file names the stream.
				string text;
				try { text = await http.ReadTextAsync(64 * 1024, ct); }
				finally { http.Dispose(); }
				if (RadioLinks.IsHls(text)) { Error("an HLS stream"); return; }
				http = await HttpStream.OpenAsync(RadioLinks.FirstStream(text) ?? throw new IOException("the playlist names no stream"), ct);
			}
			using (http)
			{
				var dst = ff.StandardInput.BaseStream;
				var metaint = http.MetaInterval;
				var untilMeta = metaint;
				var buffer = new byte[16 * 1024];
				while (true)
				{
					var n = await ReadSomeAsync(http.Body, buffer.AsMemory(0, metaint > 0 ? Math.Min(buffer.Length, untilMeta) : buffer.Length), ct);
					if (n == 0) break;
					await dst.WriteAsync(buffer.AsMemory(0, n), ct);
					if (metaint == 0 || (untilMeta -= n) > 0) continue;
					untilMeta = metaint;
					var length = (await ReadExactAsync(http.Body, 1, ct))[0] * 16;
					if (length > 0 && RadioLinks.StreamTitle(await ReadExactAsync(http.Body, length, ct)) is { } t)
						title(t);
				}
				Error("the station's stream ended");
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
		catch (PrivateLinkException) { Error("link-private"); }
		catch (Exception ex) { Error(ex.Message); }
		finally
		{
			try { ff.StandardInput.Close(); } catch { }
		}
	}

	static async Task<int> ReadSomeAsync(Stream s, Memory<byte> into, CancellationToken ct)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(RadioSilence);
		try { return await s.ReadAsync(into, timeout.Token); }
		catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new IOException("the station sent nothing for 30 seconds"); }
	}

	static async Task<byte[]> ReadExactAsync(Stream s, int count, CancellationToken ct)
	{
		var b = new byte[count];
		for (var got = 0; got < count;)
		{
			var n = await ReadSomeAsync(s, b.AsMemory(got), ct);
			if (n == 0) throw new IOException("the station's stream ended");
			got += n;
		}
		return b;
	}

	// Copies yt-dlp's output to ffmpeg and, when saving, to a file. If the song is skipped while
	// saving, the download keeps going (now at full speed) so the song still ends up in the library.
	async Task Pump(Process yt, string? teePath, Action<bool>? teeDone)
	{
		var complete = false;
		FileStream? tee = null;
		try
		{
			if (teePath != null) tee = File.Create(teePath);
			var src = yt.StandardOutput.BaseStream;
			var dst = ff.StandardInput.BaseStream;
			var ffAlive = true;
			var buffer = new byte[64 * 1024];
			int n;
			while ((n = await src.ReadAsync(buffer)) > 0)
			{
				if (tee != null) await tee.WriteAsync(buffer.AsMemory(0, n));
				if (!ffAlive) continue;
				try { await dst.WriteAsync(buffer.AsMemory(0, n)); }
				catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
				{
					ffAlive = false;
					if (tee is null) break;
				}
			}
			await yt.WaitForExitAsync();
			complete = yt.ExitCode == 0;
		}
		catch { }
		finally
		{
			try { ff.StandardInput.Close(); } catch { }
			if (tee != null) await tee.DisposeAsync();
			if (teeing)
			{
				yt.Dispose();
				teeDone?.Invoke(complete);
			}
		}
	}

	public string Errors { get { lock (ytErrors) return ytErrors.ToString(); } }

	public void Dispose()
	{
		radioStop?.Cancel();
		YouTube.Kill(ff);
		ff.Dispose();
		// While saving, yt-dlp finishes the download on its own and Pump cleans up.
		if (yt != null && !teeing)
		{
			YouTube.Kill(yt);
			yt.Dispose();
		}
	}
}
