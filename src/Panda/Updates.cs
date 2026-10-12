using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Panda;

/// <summary>Installed and newest versions of Panda and of yt-dlp.</summary>
/// <param name="Install">
/// "systemd": installed by install.sh, the panel can update it. "systemd-old": installed before
/// one-click updates; running the installer once more enables them. "docker" or "manual": only told.
/// </param>
/// <param name="Error">"unreachable" when GitHub could not be asked; the newest versions are then null.</param>
/// <param name="Upgrading">Panda's update is running (the panel was maybe reloaded or reopened since).</param>
/// <param name="YtDlpUpdating">yt-dlp's update is running.</param>
public sealed record UpdateInfo(
	string Current, string? Latest, bool Newer, string? Notes, string? Url, DateTimeOffset? Published,
	string? YtDlp, string? YtDlpLatest, bool YtDlpNewer,
	string Install, DateTimeOffset? CheckedAt, string? Error, bool Upgrading, bool YtDlpUpdating);

/// <param name="Result">
/// "updated", "current" (was already the newest), "pending" (systemd hasn't finished yet), "installed"
/// (Panda's own copy), "failed", or "reinstall" (an install.sh install from before one-click updates).
/// </param>
public sealed record YtDlpUpdate(string Result, string? From, string? To);

/// <summary>Looks for new Panda releases on GitHub and starts updates of Panda and yt-dlp.</summary>
public sealed class Updates(PandaConfig config, DataPaths paths, SystemInfo system, ILogger<Updates> log)
{
	const string Repo = "KaanBahaSever/panda";
	static readonly HttpClient Http = CreateHttp();

	readonly SemaphoreSlim checking = new(1);

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

	/// <summary>
	/// The newest Panda and yt-dlp releases; GitHub is asked at most once an hour unless
	/// <paramref name="fresh"/>. The installed yt-dlp version is read again when <paramref name="fresh"/>.
	/// </summary>
	public async Task<UpdateInfo> CheckAsync(bool fresh = false)
	{
		await checking.WaitAsync();
		try
		{
			var ytDlp = (await system.ToolsAsync(fresh)).YtDlp;
			if (fresh || latest is null || DateTime.UtcNow - checkedAt > TimeSpan.FromHours(1))
			{
				try
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
					var panda = GetJsonAsync($"https://api.github.com/repos/{Repo}/releases/latest", cts.Token);
					var yt = GetJsonAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", cts.Token);
					using var p = await panda;
					using var y = await yt;
					var r = p.RootElement;
					latest = new Release(
						r.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "",
						r.TryGetProperty("body", out var body) ? body.GetString() : null,
						r.TryGetProperty("html_url", out var url) ? url.GetString() : null,
						r.TryGetProperty("published_at", out var at) && at.TryGetDateTimeOffset(out var when) ? when : null,
						y.RootElement.GetProperty("tag_name").GetString());
					checkedAt = DateTime.UtcNow;
				}
				catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
					or KeyNotFoundException or InvalidOperationException)
				{
					log.LogWarning("Could not check for updates: {Error}", ex.Message);
					return new UpdateInfo(PanelApi.Version, null, false, null, null, null,
						ytDlp, null, false, InstallKind(), null, "unreachable", await UpgradingAsync(), await YtDlpUpdatingAsync());
				}
			}
			var l = latest;
			return new UpdateInfo(PanelApi.Version, l.Panda, IsNewer(l.Panda, PanelApi.Version), l.Notes, l.Url, l.Published,
				ytDlp, l.YtDlp, IsNewer(l.YtDlp, ytDlp), InstallKind(), new DateTimeOffset(checkedAt, TimeSpan.Zero), null,
				await UpgradingAsync(), await YtDlpUpdatingAsync());
		}
		finally { checking.Release(); }
	}

	sealed record Release(string Panda, string? Notes, string? Url, DateTimeOffset? Published, string? YtDlp);
	Release? latest;
	DateTime checkedAt;

	static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct) =>
		JsonDocument.Parse(await Http.GetStringAsync(url, ct));

	// Panda: "0.3.1"; yt-dlp: "2025.10.22" (a nightly build adds ".123456" and counts as newer).
	static bool IsNewer(string? latest, string? installed) =>
		latest != null && installed != null && Version.TryParse(latest, out var l) && Version.TryParse(installed, out var i) && l > i;

	/// <summary>
	/// Asks systemd to run the installer, which downloads the latest release and restarts Panda.
	/// Only on installs made by install.sh; false elsewhere.
	/// </summary>
	public async Task<bool> StartUpgradeAsync()
	{
		if (InstallKind() != "systemd") return false;
		if (await UpgradingAsync()) return true; // already on its way: a second click changes nothing
		log.LogInformation("Updating Panda: the installer runs now and restarts Panda");
		File.WriteAllText(UpgradeTrigger, DateTimeOffset.UtcNow.ToString("o"));
		return true;
	}

	/// <summary>
	/// Updates yt-dlp and waits for the result. On installs made by install.sh systemd does it (the
	/// program belongs to root); otherwise yt-dlp updates itself, and if it can't (no write access, or a
	/// package manager's copy) Panda downloads its own copy into the data folder and uses that from then on.
	/// </summary>
	public Task<YtDlpUpdate> UpdateYtDlpAsync()
	{
		// A second click (another tab, or the panel reopened) waits for the update that is running.
		lock (ytDlpGate)
		{
			if (ytDlpRun is { IsCompleted: false }) return ytDlpRun;
			return ytDlpRun = Task.Run(RunYtDlpUpdateAsync);
		}
	}

	readonly object ytDlpGate = new();
	Task<YtDlpUpdate>? ytDlpRun;

	async Task<bool> UpgradingAsync() =>
		InstallKind() == "systemd" && (File.Exists(UpgradeTrigger) || await ServiceRunningAsync("panda-upgrade.service"));

	async Task<bool> YtDlpUpdatingAsync()
	{
		lock (ytDlpGate) if (ytDlpRun is { IsCompleted: false }) return true;
		return InstallKind() == "systemd" && (File.Exists(YtDlpTrigger) || await ServiceRunningAsync("panda-update.service"));
	}

	static async Task<bool> ServiceRunningAsync(string unit) =>
		(await RunAsync("systemctl", "is-active " + unit)).Output.Trim() is "activating" or "active" or "reloading";

	async Task<YtDlpUpdate> RunYtDlpUpdateAsync()
	{
		var before = (await system.ToolsAsync(fresh: true)).YtDlp;
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
			log.LogInformation("Updating yt-dlp {Version}", before);
			File.WriteAllText(YtDlpTrigger, DateTimeOffset.UtcNow.ToString("o"));
			var state = await WaitForSystemdAsync();
			var after = (await system.ToolsAsync(fresh: true)).YtDlp;
			var result = state switch
			{
				"failed" => "failed",
				"pending" => "pending",
				_ => after != before ? "updated" : "current",
			};
			log.LogInformation("yt-dlp update: {Result} ({From} -> {To})", result, before, after);
			return new YtDlpUpdate(result, before, after);
		case "systemd-old":
			// Downloading a private copy here would leave the daily updates behind for good.
			return new YtDlpUpdate("reinstall", before, before);
		}
		var (code, output) = await RunAsync(config.YouTube.YtDlpPath, "-U");
		log.LogInformation("yt-dlp -U: {Output}", output.Trim());
		if (code == 0 && !output.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
		{
			var after = (await system.ToolsAsync(fresh: true)).YtDlp;
			return new YtDlpUpdate(after != before ? "updated" : "current", before, after);
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
		return new YtDlpUpdate("installed", before, (await system.ToolsAsync(fresh: true)).YtDlp);
	}

	/// <summary>
	/// Waits for panda-update.service to pick up the trigger and finish: "done", "failed", or "pending"
	/// when it hasn't finished within two minutes.
	/// </summary>
	async Task<string> WaitForSystemdAsync()
	{
		var until = DateTime.UtcNow + TimeSpan.FromMinutes(2);
		while (DateTime.UtcNow < until)
		{
			await Task.Delay(1500);
			// The service deletes the trigger as its first step, then runs "yt-dlp -U".
			if (File.Exists(YtDlpTrigger)) continue;
			if (await ServiceRunningAsync("panda-update.service")) continue;
			return (await RunAsync("systemctl", "is-active panda-update.service")).Output.Trim() == "failed" ? "failed" : "done";
		}
		return "pending";
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
