using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Panda;

/// <param name="Install">
/// "systemd": installed by install.sh, the panel can update it. "systemd-old": installed before
/// one-click updates; running the installer once more enables them. "docker" or "manual": only told.
/// </param>
public sealed record UpdateInfo(
	string Current, string? Latest, bool Newer, string? Notes, string? Url, DateTimeOffset? Published,
	string Install, string? Error);

/// <summary>Looks for new Panda releases on GitHub and starts updates of Panda and yt-dlp.</summary>
public sealed class Updates(PandaConfig config, DataPaths paths, SystemInfo system, ILogger<Updates> log)
{
	const string Repo = "KaanBahaSever/panda";
	static readonly HttpClient Http = CreateHttp();

	readonly SemaphoreSlim checking = new(1);
	UpdateInfo? cached;
	DateTime cachedAt;

	static HttpClient CreateHttp()
	{
		var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
		http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("panda-bot", PanelApi.Version));
		return http;
	}

	// The installer's systemd units (see scripts/install.sh) watch these files and run as root.
	string UpgradeTrigger => Path.Combine(paths.Data, ".upgrade");
	string YtDlpTrigger => Path.Combine(paths.Data, ".update-ytdlp");

	static string InstallKind()
	{
		if (File.Exists("/.dockerenv")) return "docker";
		if (OperatingSystem.IsLinux() && File.Exists("/etc/systemd/system/panda.service"))
			return File.Exists("/etc/systemd/system/panda-upgrade.path") ? "systemd" : "systemd-old";
		return "manual";
	}

	/// <summary>The latest release; asks GitHub at most once an hour unless <paramref name="fresh"/>.</summary>
	public async Task<UpdateInfo> CheckAsync(bool fresh = false)
	{
		await checking.WaitAsync();
		try
		{
			if (!fresh && cached != null && DateTime.UtcNow - cachedAt < TimeSpan.FromHours(1)) return cached;
			var current = PanelApi.Version;
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
				using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest", cts.Token));
				var r = doc.RootElement;
				var tag = r.GetProperty("tag_name").GetString() ?? "";
				var latest = tag.TrimStart('v');
				var newer = Version.TryParse(latest, out var l) && Version.TryParse(current, out var c) && l > c;
				cached = new UpdateInfo(current, latest, newer,
					r.TryGetProperty("body", out var body) ? body.GetString() : null,
					r.TryGetProperty("html_url", out var url) ? url.GetString() : null,
					r.TryGetProperty("published_at", out var p) && p.TryGetDateTimeOffset(out var at) ? at : null,
					InstallKind(), null);
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
			{
				log.LogWarning("Could not check for updates: {Error}", ex.Message);
				return new UpdateInfo(current, null, false, null, null, null, InstallKind(), "unreachable");
			}
			cachedAt = DateTime.UtcNow;
			return cached;
		}
		finally { checking.Release(); }
	}

	/// <summary>
	/// Asks systemd to run the installer, which downloads the latest release and restarts Panda.
	/// Only on installs made by install.sh; false elsewhere.
	/// </summary>
	public bool StartUpgrade()
	{
		if (InstallKind() != "systemd") return false;
		log.LogInformation("Updating Panda: the installer runs now and restarts Panda");
		File.WriteAllText(UpgradeTrigger, DateTimeOffset.UtcNow.ToString("o"));
		return true;
	}

	/// <summary>
	/// Updates yt-dlp. On installs made by install.sh systemd does it (the program belongs to root);
	/// otherwise yt-dlp updates itself, and if it can't (no write access, or a package manager's copy)
	/// Panda downloads its own copy into the data folder and uses that from then on.
	/// </summary>
	/// <returns>
	/// "started" (systemd, takes a few seconds), "updated", "installed" (own copy), or "reinstall"
	/// (an install.sh install from before one-click updates: its yt-dlp belongs to root and is kept up
	/// to date daily; running the installer once more enables the button).
	/// </returns>
	public async Task<string> UpdateYtDlpAsync()
	{
		switch (InstallKind())
		{
		case "systemd":
			// The daily timer and this trigger update the installer's copy; make sure that is the one in use.
			const string installed = "/opt/panda/bin/yt-dlp";
			if (config.YouTube.YtDlpPath != installed && File.Exists(installed)
				&& Path.GetFullPath(config.YouTube.YtDlpPath).StartsWith(Path.GetFullPath(paths.Data)))
			{
				config.YouTube.YtDlpPath = installed;
				config.Save();
			}
			File.WriteAllText(YtDlpTrigger, DateTimeOffset.UtcNow.ToString("o"));
			return "started";
		case "systemd-old":
			// Downloading a private copy here would leave the daily updates behind for good.
			return "reinstall";
		}
		var (code, output) = await RunAsync(config.YouTube.YtDlpPath, "-U");
		log.LogInformation("yt-dlp -U: {Output}", output.Trim());
		if (code == 0 && !output.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
		{
			await system.ToolsAsync(fresh: true);
			return "updated";
		}

		var asset = YtDlpAsset() ?? throw new InvalidOperationException("no yt-dlp build for this system");
		var dir = Path.Combine(paths.Data, "bin");
		Directory.CreateDirectory(dir);
		var target = Path.Combine(dir, OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
		var tmp = target + ".download";
		log.LogInformation("Downloading yt-dlp ({Asset}) into {Dir}", asset, dir);
		using (var response = await Http.GetAsync($"https://github.com/yt-dlp/yt-dlp/releases/latest/download/{asset}", HttpCompletionOption.ResponseHeadersRead))
		{
			response.EnsureSuccessStatusCode();
			await using var file = File.Create(tmp);
			await response.Content.CopyToAsync(file);
		}
		if (!OperatingSystem.IsWindows())
			File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
				| UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
		File.Move(tmp, target, overwrite: true);
		config.YouTube.YtDlpPath = target;
		config.Save();
		await system.ToolsAsync(fresh: true);
		return "installed";
	}

	static string? YtDlpAsset() =>
		OperatingSystem.IsWindows() ? "yt-dlp.exe"
		: OperatingSystem.IsMacOS() ? "yt-dlp_macos"
		: OperatingSystem.IsLinux() ? RuntimeInformation.OSArchitecture switch
		{
			Architecture.X64 => "yt-dlp_linux",
			Architecture.Arm64 => "yt-dlp_linux_aarch64",
			_ => null,
		}
		: null;

	static async Task<(int Code, string Output)> RunAsync(string file, string arg)
	{
		try
		{
			using var p = Process.Start(new ProcessStartInfo(file, arg)
			{
				RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
			});
			if (p is null) return (-1, "");
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			var stdout = p.StandardOutput.ReadToEndAsync(cts.Token);
			var stderr = p.StandardError.ReadToEndAsync(cts.Token);
			await p.WaitForExitAsync(cts.Token);
			return (p.ExitCode, await stdout + await stderr);
		}
		catch (Exception ex) { return (-1, ex.Message); }
	}
}
