using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
	public string Url => $"https://www.youtube.com/watch?v={Id}";
	public string Thumbnail => $"https://i.ytimg.com/vi/{Id}/hqdefault.jpg";
	public bool IsLive => Duration <= 0;
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
		|| input.StartsWith("music.youtube", StringComparison.OrdinalIgnoreCase);

	[GeneratedRegex(@"^(?:https?://)?(?:www\.|m\.|music\.)?(?:youtube\.com|youtu\.be)/", RegexOptions.IgnoreCase)]
	private static partial Regex YouTubeHostRegex();

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

		var max = config.YouTube.MaxDurationMinutes;
		if (max > 0 && track.Duration > max * 60)
			throw new YouTubeException("too-long");
		return track;
	}

	public async Task<List<Track>> SearchAsync(string query, int count, string requestedBy, CancellationToken ct = default)
	{
		query = CleanInput(query);
		if (query.Length == 0)
			return [];
		// --flat-playlist only reads the search page, which is much faster than resolving every video.
		var json = await RunJsonAsync(["--flat-playlist", $"ytsearch{Math.Clamp(count, 1, 20)}:{query}"], ct);
		var list = new List<Track>();
		if (json.TryGetProperty("entries", out var entries))
			foreach (var e in entries.EnumerateArray())
				if (e.TryGetProperty("id", out var idEl) && idEl.GetString() is { Length: 11 })
					list.Add(FromJson(e, requestedBy));
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

	List<string> BaseArgs()
	{
		var args = new List<string> { "--ignore-config", "--no-warnings", "--no-progress" };
		var cookies = config.YouTube.CookiesFile;
		if (cookies.Length > 0 && File.Exists(cookies))
			args.AddRange(["--cookies", cookies]);
		return args;
	}

	async Task<JsonElement> RunJsonAsync(IEnumerable<string> extra, CancellationToken ct)
	{
		var psi = new ProcessStartInfo(config.YouTube.YtDlpPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			StandardOutputEncoding = Encoding.UTF8,
		};
		foreach (var a in BaseArgs()) psi.ArgumentList.Add(a);
		psi.ArgumentList.Add("-J");
		foreach (var a in extra) psi.ArgumentList.Add(a);

		using var proc = StartProcess(psi);
		using var reg = ct.Register(() => Kill(proc));
		var stdout = proc.StandardOutput.ReadToEndAsync(ct);
		var stderr = proc.StandardError.ReadToEndAsync(ct);
		await proc.WaitForExitAsync(ct);
		var output = await stdout;
		if (proc.ExitCode != 0 || output.Length == 0)
			throw new YouTubeException(FriendlyError(await stderr));
		return JsonDocument.Parse(output).RootElement.Clone();
	}

	/// <summary>
	/// Starts yt-dlp (download to stdout) piped into ffmpeg (decode to 48 kHz stereo s16le PCM).
	/// yt-dlp handles YouTube's chunked downloads and throttling; ffmpeg only decodes.
	/// </summary>
	/// <param name="teePath">Also write the downloaded bytes to this file (to save the song).</param>
	/// <param name="teeDone">Called when the download ended; true when it completed.</param>
	public AudioStream OpenAudio(Track track, int startSeconds = 0, string? teePath = null, Action<bool>? teeDone = null)
	{
		var yt = new ProcessStartInfo(config.YouTube.YtDlpPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in BaseArgs()) yt.ArgumentList.Add(a);
		foreach (var a in new[] { "--quiet", "--no-playlist", "-f", "bestaudio/best", "-o", "-", track.Url })
			yt.ArgumentList.Add(a);

		// Seeking a pipe means decoding up to that point, so -ss goes after the input here.
		var ff = FfmpegInfo(["-i", "pipe:0", .. SeekArgs(startSeconds)], stdin: true);
		var ytProc = StartProcess(yt);
		Process ffProc;
		try { ffProc = StartProcess(ff); }
		catch { Kill(ytProc); throw; }
		return new AudioStream(ytProc, ffProc, log, teePath, teeDone);
	}

	/// <summary>Plays a saved song; seeking in a file is instant.</summary>
	public AudioStream OpenFile(string path, int startSeconds = 0) =>
		new(null, StartProcess(FfmpegInfo([.. SeekArgs(startSeconds), "-i", path], stdin: false)), log);

	static string[] SeekArgs(int seconds) => seconds > 0 ? ["-ss", seconds.ToString()] : [];

	ProcessStartInfo FfmpegInfo(string[] input, bool stdin)
	{
		var ff = new ProcessStartInfo(config.YouTube.FfmpegPath)
		{
			RedirectStandardInput = stdin,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in (string[])["-hide_banner", "-loglevel", "error", .. input, "-vn", "-f", "s16le", "-ar", "48000", "-ac", "2", "pipe:1"])
			ff.ArgumentList.Add(a);
		return ff;
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
		foreach (var a in new[] { "--quiet", "--no-playlist", "--no-part", "-f", "bestaudio/best", "-o", path, track.Url })
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

	public AudioStream(Process? yt, Process ff, ILogger log, string? teePath = null, Action<bool>? teeDone = null)
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
					log.LogDebug("ffmpeg: {Line}", line);
			}
			catch { }
		});
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
