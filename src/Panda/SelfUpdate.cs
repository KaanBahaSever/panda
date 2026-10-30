using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Panda;

/// <summary>
/// A Panda that isn't installed by install.sh or Docker (Windows, macOS, a Linux download) updates itself:
/// the new release is downloaded and checked against its SHA256SUMS, its program is tried (--version), the
/// running one is renamed to .old (Windows allows that for a running program) and the new one put in its
/// place. Then the new Panda is started and this one stops properly (the queue is saved); the new one waits
/// for it to be gone (--after) before it starts. Anything going wrong leaves the old Panda as it was.
/// </summary>
public sealed partial class Updates
{
	/// <summary>Only for tests: where the releases are (instead of GitHub), e.g. http://127.0.0.1:8099.</summary>
	static readonly string? TestReleases = Environment.GetEnvironmentVariable("PANDA_TEST_RELEASES");
	static string LatestApi => TestReleases is { } t ? t + "/latest.json" : $"https://api.github.com/repos/{Repo}/releases/latest";
	static string Download(string version, string file) =>
		TestReleases is { } t ? $"{t}/v{version}/{file}" : $"https://github.com/{Repo}/releases/download/v{version}/{file}";

	/// <summary>The release archive for this system, or null when there is none.</summary>
	static string? PandaAsset() =>
		OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture == Architecture.X64 ? "panda-win-x64.zip"
		: OperatingSystem.IsMacOS() && RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "panda-osx-arm64.tar.gz"
		: OperatingSystem.IsLinux() ? RuntimeInformation.OSArchitecture switch
		{
			Architecture.X64 => "panda-linux-x64.tar.gz",
			Architecture.Arm64 => "panda-linux-arm64.tar.gz",
			_ => null,
		}
		: null;

	/// <summary>This program, when it is Panda's own single file (not "dotnet panda.dll", not a test run).</summary>
	static string? ThisProgram()
	{
		var path = Environment.ProcessPath;
		if (path is null) return null;
		var name = Path.GetFileNameWithoutExtension(path);
		return name.Equals("panda", StringComparison.OrdinalIgnoreCase) ? path : null;
	}

	static readonly Lazy<bool> canReplaceItself = new(() =>
	{
		if (PandaAsset() is null || ThisProgram() is not { } exe) return false;
		// The folder must take new files (not C:\Program Files without admin rights, not a read-only mount).
		var probe = Path.Combine(Path.GetDirectoryName(exe)!, ".panda-write-test");
		try { File.WriteAllText(probe, ""); File.Delete(probe); return true; }
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
	});

	string selfState = "idle";   // "running", "failed"
	readonly object selfGate = new();

	/// <summary>Starts the update to <paramref name="version"/>; false when one is running already.</summary>
	bool StartSelfUpdate(string version)
	{
		lock (selfGate)
		{
			if (selfState == "running") return false;
			selfState = "running";
		}
		_ = Task.Run(async () =>
		{
			try
			{
				await ReplaceItselfAsync(version);
			}
			catch (Exception ex)
			{
				log.LogWarning("The update to {Version} failed; this Panda keeps running: {Error}", version, ex.Message);
				lock (selfGate) selfState = "failed";
			}
		});
		return true;
	}

	async Task ReplaceItselfAsync(string version)
	{
		var exe = ThisProgram() ?? throw new InvalidOperationException("not Panda's own program");
		var asset = PandaAsset() ?? throw new InvalidOperationException("no release for this system");
		var dir = Path.GetDirectoryName(exe)!;
		var fresh = exe + ".new";
		var old = exe + ".old";
		log.LogInformation("Updating Panda to {Version}: downloading {Asset}", version, asset);

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		var sums = await Http.GetStringAsync(Download(version, "SHA256SUMS"), cts.Token);
		var expected = sums.Split('\n').Select(l => l.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
			.FirstOrDefault(p => p.Length == 2 && p[1].TrimStart('*') == asset)?[0]
			?? throw new InvalidOperationException("the release lists no checksum for " + asset);

		var archive = Path.Combine(dir, ".panda-update.download");
		try
		{
			using (var response = await Http.GetAsync(Download(version, asset), HttpCompletionOption.ResponseHeadersRead, cts.Token))
			{
				response.EnsureSuccessStatusCode();
				await using var file = File.Create(archive);
				await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
				var buffer = new byte[81920];
				long total = 0;
				int n;
				while ((n = await body.ReadAsync(buffer, cts.Token)) > 0)
				{
					// A release is ~45 MB; anything far bigger isn't one.
					if ((total += n) > 300L * 1024 * 1024) throw new InvalidOperationException("the download is too large");
					await file.WriteAsync(buffer.AsMemory(0, n), cts.Token);
				}
			}
			await using (var check = File.OpenRead(archive))
			{
				var hash = Convert.ToHexString(await SHA256.HashDataAsync(check, cts.Token));
				if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException("the download doesn't match its checksum");
			}
			Unpack(archive, asset, fresh);
		}
		finally { File.Delete(archive); }

		try
		{
			if (!OperatingSystem.IsWindows())
				File.SetUnixFileMode(fresh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
					| UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
			// The new program has to start at all, and be the version asked for.
			var (code, output) = await RunAsync(fresh, "--version");
			if (code != 0 || output.Trim() != version)
				throw new InvalidOperationException($"the new program doesn't start right ({code}: {output.Trim()})");

			if (File.Exists(old)) File.Delete(old);
			File.Move(exe, old);
			try { File.Move(fresh, exe); }
			catch { File.Move(old, exe); throw; }
		}
		catch { try { File.Delete(fresh); } catch (IOException) { } throw; }

		log.LogInformation("Panda {Version} is in place; restarting", version);
		Restart(exe);
	}

	static void Unpack(string archive, string asset, string to)
	{
		var name = asset.EndsWith(".zip") ? "panda.exe" : "panda";
		using var source = File.OpenRead(archive);
		if (asset.EndsWith(".zip"))
		{
			using var zip = new ZipArchive(source, ZipArchiveMode.Read);
			var entry = zip.GetEntry(name) ?? throw new InvalidOperationException("the archive has no " + name);
			entry.ExtractToFile(to, overwrite: true);
			return;
		}
		using var gz = new GZipStream(source, CompressionMode.Decompress);
		using var tar = new TarReader(gz);
		while (tar.GetNextEntry() is { } e)
			if (e.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile && e.Name.TrimStart('.', '/') == name)
			{
				e.ExtractToFile(to, overwrite: true);
				return;
			}
		throw new InvalidOperationException("the archive has no " + name);
	}

	/// <summary>
	/// Starts the new Panda and stops this one. Under a service manager (systemd, a Windows service) it is
	/// the one that starts Panda again (exit code 3); otherwise the new Panda starts in a window of its own
	/// (Windows) or in this one's place (its output goes where this one's went).
	/// </summary>
	void Restart(string exe)
	{
		var managed = Environment.GetEnvironmentVariable("INVOCATION_ID") != null || !Environment.UserInteractive;
		if (!managed)
		{
			// This Panda's own arguments, without the "--after" of the update that started it (if one did).
			var own = Environment.GetCommandLineArgs().Skip(1).ToList();
			for (var i = own.IndexOf("--after"); i >= 0; i = own.IndexOf("--after"))
				own.RemoveRange(i, Math.Min(2, own.Count - i));
			var args = own.Concat(["--after", Environment.ProcessId.ToString()]);
			Process.Start(new ProcessStartInfo(exe, string.Join(' ', args.Select(Quote)))
			{
				UseShellExecute = OperatingSystem.IsWindows(),
				WorkingDirectory = Environment.CurrentDirectory,
			});
		}
		else Environment.ExitCode = 3;
		lifetime.StopApplication();
	}

	// Command line quoting both Windows and .NET's own parser read back (a data folder with spaces).
	static string Quote(string a)
	{
		if (a.Length > 0 && !a.Any(c => char.IsWhiteSpace(c) || c == '"')) return a;
		var sb = new System.Text.StringBuilder("\"");
		var slashes = 0;
		foreach (var c in a)
		{
			if (c == '\\') { slashes++; continue; }
			// Backslashes count only before a quote: doubled there, and the quote escaped.
			if (c == '"') sb.Append('\\', slashes * 2 + 1).Append('"');
			else sb.Append('\\', slashes).Append(c);
			slashes = 0;
		}
		return sb.Append('\\', slashes * 2).Append('"').ToString();
	}

	/// <summary>A Panda started by an update waits for the one it replaces to be gone (the panel's port, the
	/// saved queue), then removes the old program.</summary>
	public static void AfterUpdate(string? previousPid)
	{
		if (!int.TryParse(previousPid, out var pid)) return;
		try
		{
			using var p = Process.GetProcessById(pid);
			// It saved the queue as it began to stop; one that hangs must not keep the new Panda from starting.
			if (!p.WaitForExit(TimeSpan.FromSeconds(60)) && p.ProcessName.StartsWith("panda", StringComparison.OrdinalIgnoreCase))
			{
				p.Kill();
				p.WaitForExit(TimeSpan.FromSeconds(10));
			}
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }   // already gone
		if (ThisProgram() is { } exe)
		{
			// Windows may still hold it for a moment after the process ends.
			for (var i = 0; i < 10 && File.Exists(exe + ".old"); i++)
			{
				try { File.Delete(exe + ".old"); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Thread.Sleep(500); }
			}
		}
	}
}
