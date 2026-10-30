using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Panda;

/// <summary>
/// A Panda that isn't installed by install.sh or Docker (Windows, macOS, a Linux download) updates itself:
/// the new release is downloaded and checked against its SHA256SUMS, its program is tried (--version), the
/// running one is renamed (Windows allows that for a running program) and the new one put in its place.
/// Then this Panda stops properly (the queue is saved) and the new one takes over: on Linux and macOS in the
/// same process (exec, so a service manager, a container or a terminal keep it), on Windows in a new window
/// (it waits for this one to be gone, --after). The new one is on trial: if it can't start, or hasn't run two
/// minutes after two tries, the old program comes back. Anything going wrong before leaves the old Panda as it was.
/// </summary>
public sealed partial class Updates
{
	/// <summary>Only for tests: where the releases are (instead of GitHub), e.g. http://127.0.0.1:8099; this machine only.</summary>
	public static readonly string? TestReleases = Environment.GetEnvironmentVariable("PANDA_TEST_RELEASES") is { } t
		&& Uri.TryCreate(t, UriKind.Absolute, out var u) && u.IsLoopback ? t.TrimEnd('/') : null;
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

	/// <summary>This program, when it is Panda's own single file (not "dotnet panda.dll", not a build folder).</summary>
	static string? ThisProgram()
	{
		var path = Environment.ProcessPath;
		if (path is null || !Path.GetFileNameWithoutExtension(path).Equals("panda", StringComparison.OrdinalIgnoreCase)) return null;
#pragma warning disable IL3000 // empty in a single-file program: that's the point
		return string.IsNullOrEmpty(typeof(Updates).Assembly.Location) ? path : null;
#pragma warning restore IL3000
	}

	static readonly Lazy<bool> canReplaceItself = new(() =>
	{
		try
		{
			if (PandaAsset() is null || ThisProgram() is not { } exe) return false;
			// A Windows service has no one to start the new Panda.
			if (OperatingSystem.IsWindows() && !Environment.UserInteractive) return false;
			// The folder must take new files (not C:\Program Files without admin rights, not a read-only mount).
			var probe = Path.Combine(Path.GetDirectoryName(exe)!, $".panda-write-test-{Environment.ProcessId}");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		}
		catch { return false; }
	});

	// Containers other than Docker: the next container would start from the old image again.
	static bool InContainer() =>
		File.Exists("/.dockerenv") || File.Exists("/run/.containerenv") || Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null;

	string selfState = "idle";   // "running", "failed"
	readonly object selfGate = new();

	/// <summary>Starts the update to <paramref name="version"/>; false when one is running already or Panda is
	/// stopping. <paramref name="mayRestart"/>: asked right before the switch (automatic updates wait until nothing plays).</summary>
	bool StartSelfUpdate(string version, Func<bool>? mayRestart = null)
	{
		lock (selfGate)
		{
			if (selfState == "running" || lifetime.ApplicationStopping.IsCancellationRequested || RestorePending) return false;
			selfState = "running";
		}
		_ = Task.Run(async () =>
		{
			try
			{
				await ReplaceItselfAsync(version, mayRestart);
			}
			catch (Exception ex)
			{
				log.LogWarning("The update to {Version} failed; this Panda keeps running: {Error}", version, ex.Message);
				lock (selfGate) selfState = "failed";
			}
		});
		return true;
	}

	async Task ReplaceItselfAsync(string version, Func<bool>? mayRestart)
	{
		var exe = ThisProgram() ?? throw new InvalidOperationException("not Panda's own program");
		var asset = PandaAsset() ?? throw new InvalidOperationException("no release for this system");
		var dir = Path.GetDirectoryName(exe)!;
		var fresh = exe + ".new";
		log.LogInformation("Updating Panda to {Version}: downloading {Asset}", version, asset);

		// Ends with Panda's own stop too (Ctrl+C, a closed window, a restored backup).
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
		cts.CancelAfter(TimeSpan.FromMinutes(15));
		var sums = await Http.GetStringAsync(Download(version, "SHA256SUMS"), cts.Token);
		var expected = sums.Split('\n').Select(l => l.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
			.FirstOrDefault(p => p.Length == 2 && p[1].Trim().TrimStart('*') == asset)?[0]
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
			if (code != 0 || !Version.TryParse(output.Trim(), out var got) || !Version.TryParse(version, out var want) || got != want)
				throw new InvalidOperationException($"the new program doesn't start right ({code}: {output.Trim()})");

			// Automatic updates: not while something plays (it may have started during the download).
			while (mayRestart != null && !mayRestart())
				await Task.Delay(TimeSpan.FromSeconds(15), lifetime.ApplicationStopping);
			lifetime.ApplicationStopping.ThrowIfCancellationRequested();
			if (RestorePending) throw new InvalidOperationException("a backup is being restored");

			var old = Swap(exe, fresh);
			try
			{
				File.WriteAllLines(TrialFile(exe), [old, "0"]);
				log.LogInformation("Panda {Version} is in place; restarting", version);
				Restart(exe);
			}
			catch
			{
				// Couldn't start the new one: the old program goes back where it was.
				File.Delete(TrialFile(exe));
				File.Move(exe, fresh, overwrite: true);
				File.Move(old, exe);
				throw;
			}
		}
		catch
		{
			if (File.Exists(exe)) try { File.Delete(fresh); } catch (IOException) { }
			throw;
		}
	}

	/// <summary>The running program steps aside (renamed), the new one takes its place. Returns where the old one went.</summary>
	static string Swap(string exe, string fresh)
	{
		var old = exe + ".old";
		// One left from an earlier update may still be held (Windows): then another name.
		try { if (File.Exists(old)) File.Delete(old); }
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { old = $"{exe}.old-{Environment.ProcessId}"; }
		File.Move(exe, old);
		try { File.Move(fresh, exe); }
		catch { File.Move(old, exe); throw; }
		return old;
	}

	static string TrialFile(string exe) => exe + ".update";

	// A restored backup waits for the next start (Backup.cs): this Panda is about to stop for it.
	bool RestorePending => Directory.Exists(Path.Combine(paths.Data, ".restore"));

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

	/// <summary>Set when this Panda should become the new program once it has stopped (Linux, macOS).</summary>
	public static string? ExecAfterStop { get; private set; }

	/// <summary>Stops this Panda for the new program: on Windows it is started now (its own window) and waits
	/// for this one; elsewhere this process turns into it after stopping (<see cref="ExecNow"/>).</summary>
	void Restart(string exe)
	{
		if (OperatingSystem.IsWindows())
		{
			var args = OwnArguments().Concat(["--after", Environment.ProcessId.ToString()]);
			Process.Start(new ProcessStartInfo(exe, string.Join(' ', args.Select(Quote)))
			{
				UseShellExecute = true,
				WorkingDirectory = Environment.CurrentDirectory,
			});
		}
		else ExecAfterStop = exe;
		lifetime.StopApplication();
	}

	// This Panda's own arguments, without the "--after" of the update that started it (if one did).
	static List<string> OwnArguments()
	{
		var own = Environment.GetCommandLineArgs().Skip(1).ToList();
		for (var i = own.IndexOf("--after"); i >= 0; i = own.IndexOf("--after"))
			own.RemoveRange(i, Math.Min(2, own.Count - i));
		return own;
	}

	[DllImport("libc", SetLastError = true)]
	static extern int execv(string path, string?[] argv);

	/// <summary>
	/// Linux, macOS: this process becomes <paramref name="exe"/> (the same process id, so systemd, launchd, a
	/// container or a terminal keep it as theirs). Only if that fails is it started as a new process.
	/// </summary>
	public static void ExecNow(string exe)
	{
		var argv = new List<string?> { exe };
		argv.AddRange(OwnArguments());
		argv.Add(null);
		Console.Out.Flush();
		execv(exe, argv.ToArray());
		Console.Error.WriteLine($"Could not restart as the new Panda (errno {Marshal.GetLastWin32Error()}); starting it beside.");
		var start = new ProcessStartInfo(exe) { UseShellExecute = false };
		foreach (var a in OwnArguments()) start.ArgumentList.Add(a);
		Process.Start(start);
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

	/// <summary>Windows: a Panda started by an update waits for the one it replaces to be gone (the panel's
	/// port, the saved queue).</summary>
	public static void AfterUpdate(string? previousPid)
	{
		if (!int.TryParse(previousPid, out var pid)) return;
		try
		{
			using var p = Process.GetProcessById(pid);
			using var me = Process.GetCurrentProcess();
			// The process id may already belong to something else.
			if (p.StartTime >= me.StartTime || !p.ProcessName.StartsWith("panda", StringComparison.OrdinalIgnoreCase)) return;
			Console.WriteLine("Waiting for the previous Panda to stop…");
			// It saved the queue as it began to stop; one that hangs must not keep the new Panda from starting.
			if (!p.WaitForExit(TimeSpan.FromSeconds(60)))
			{
				p.Kill();
				p.WaitForExit(TimeSpan.FromSeconds(10));
			}
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }   // already gone
	}

	/// <summary>
	/// At start: a Panda just put in place by an update is on trial. On its third start without having run two
	/// minutes, the old program comes back (true: the caller starts that one). Otherwise the try is counted.
	/// </summary>
	public static bool TrialFailedBefore()
	{
		if (ThisProgram() is not { } exe || !File.Exists(TrialFile(exe))) return false;
		try
		{
			var lines = File.ReadAllLines(TrialFile(exe));
			var tries = lines.Length > 1 && int.TryParse(lines[1], out var n) ? n : 0;
			if (tries >= 2) return RollBack(exe, "it didn't start twice");
			File.WriteAllLines(TrialFile(exe), [lines.FirstOrDefault() ?? "", (tries + 1).ToString()]);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		return false;
	}

	/// <summary>The new Panda couldn't start: the old program comes back (true when it did).</summary>
	public static bool RollBackIfOnTrial(string why) =>
		ThisProgram() is { } exe && File.Exists(TrialFile(exe)) && RollBack(exe, why);

	static bool RollBack(string exe, string why)
	{
		try
		{
			var old = File.ReadAllLines(TrialFile(exe)).FirstOrDefault();
			if (old is not { Length: > 0 } || !File.Exists(old)) { File.Delete(TrialFile(exe)); return false; }
			Console.Error.WriteLine($"The updated Panda doesn't work ({why}); going back to the previous version.");
			File.Move(exe, exe + ".failed", overwrite: true);
			File.Move(old, exe);
			File.Delete(TrialFile(exe));
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
	}

	/// <summary>Starts the program at the usual place again (after a roll back): the same process on Linux and
	/// macOS, a new one on Windows.</summary>
	public static void StartAgain()
	{
		if (ThisProgram() is not { } exe) return;
		if (!OperatingSystem.IsWindows()) { ExecNow(exe); return; }
		Process.Start(new ProcessStartInfo(exe, string.Join(' ', OwnArguments().Select(Quote)))
		{
			UseShellExecute = true,
			WorkingDirectory = Environment.CurrentDirectory,
		});
	}

	/// <summary>Two minutes up: the update worked, the old program and the trial go.</summary>
	public static void TrialPassed()
	{
		if (ThisProgram() is not { } exe || !File.Exists(TrialFile(exe))) return;
		try
		{
			var old = File.ReadAllLines(TrialFile(exe)).FirstOrDefault();
			File.Delete(TrialFile(exe));
			if (old is { Length: > 0 } && old.StartsWith(exe + ".old")) File.Delete(old);
			foreach (var f in Directory.EnumerateFiles(Path.GetDirectoryName(exe)!, Path.GetFileName(exe) + ".old-*")) File.Delete(f);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
	}
}
