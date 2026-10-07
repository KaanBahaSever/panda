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
		else if (PlaylistRegex().IsMatch(input))
		{
			throw new YouTubeException("playlist");
		}
		else if (input.StartsWith("http://") || input.StartsWith("https://"))
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
	public AudioStream OpenAudio(Track track, int startSeconds = 0)
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

		var ff = new ProcessStartInfo(config.YouTube.FfmpegPath)
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		var ffArgs = new List<string> { "-hide_banner", "-loglevel", "error", "-i", "pipe:0" };
		if (startSeconds > 0)
			ffArgs.AddRange(["-ss", startSeconds.ToString()]);
		ffArgs.AddRange(["-vn", "-f", "s16le", "-ar", "48000", "-ac", "2", "pipe:1"]);
		foreach (var a in ffArgs) ff.ArgumentList.Add(a);

		var ytProc = StartProcess(yt);
		Process ffProc;
		try { ffProc = StartProcess(ff); }
		catch { Kill(ytProc); throw; }
		return new AudioStream(ytProc, ffProc, log);
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
		var line = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(l => l.Contains("ERROR")) ?? "";
		return line.Length > 0 ? line.Replace("ERROR: ", "").Trim() : "failed";
	}
}

/// <summary>PCM audio of one track: yt-dlp stdout is pumped into ffmpeg stdin, ffmpeg stdout is the PCM.</summary>
public sealed class AudioStream : IDisposable
{
	readonly Process yt, ff;
	readonly StringBuilder ytErrors = new();
	public Stream Pcm => ff.StandardOutput.BaseStream;

	public AudioStream(Process yt, Process ff, ILogger log)
	{
		this.yt = yt;
		this.ff = ff;
		_ = Task.Run(async () =>
		{
			try { await yt.StandardOutput.BaseStream.CopyToAsync(ff.StandardInput.BaseStream); }
			catch (IOException) { }
			catch (ObjectDisposedException) { }
			finally { try { ff.StandardInput.Close(); } catch { } }
		});
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

	public string Errors { get { lock (ytErrors) return ytErrors.ToString(); } }

	public void Dispose()
	{
		YouTube.Kill(yt);
		YouTube.Kill(ff);
		yt.Dispose();
		ff.Dispose();
	}
}
