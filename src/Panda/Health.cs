using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Panda;

public sealed record HealthInfo(
	string Version, long UptimeSeconds, string Os, string Runtime,
	ProcessHealth Process, MachineHealth Machine, DiskHealth? Disk, LibrarySummary Library,
	ToolVersions Tools, IReadOnlyList<BotHealth> Bots);

/// <param name="CpuPercent">Share of the whole machine (all cores), like the machine's own figure.</param>
public sealed record ProcessHealth(double? CpuPercent, long MemoryBytes, long ManagedBytes, int Threads);
public sealed record MachineHealth(int Cores, double? CpuPercent, long? MemoryTotalBytes, long? MemoryUsedBytes);
public sealed record DiskHealth(string Path, long TotalBytes, long FreeBytes);
/// <summary>null = the program could not be run.</summary>
public sealed record ToolVersions(string? YtDlp, string? Ffmpeg);
public sealed record BotHealth(string Id, string Nickname, string State, string? Error, string Address, string? Server, string? Channel, int Listeners);

/// <summary>What the panel's health screen shows: resources, versions and the bots' connections.</summary>
public sealed partial class SystemInfo(PandaConfig config, Library library, BotManager bots)
{
	readonly object gate = new();
	(long At, TimeSpan Process, (ulong Busy, ulong Total)? Machine)? lastCpu;
	double? processCpu, machineCpu;
	ToolVersions? tools;
	DateTime toolsAt;

	/// <param name="freshTools">Ask yt-dlp and ffmpeg for their versions again (after an update).</param>
	public async Task<HealthInfo> GetAsync(bool freshTools = false)
	{
		// CPU use is the difference between two readings; the first time, take two.
		if (lastCpu is null)
		{
			SampleCpu();
			await Task.Delay(300);
		}
		var (procCpu, sysCpu) = SampleCpu();

		using var self = Process.GetCurrentProcess();
		var botHealth = new List<BotHealth>();
		foreach (var b in bots.All)
		{
			var s = await b.Ts.GetStatusAsync();
			botHealth.Add(new(b.Id, b.Config.Bot.Nickname, s.State, s.Error, b.Config.Server.Address, s.ServerName, s.ChannelName, s.Listeners));
		}
		var (memTotal, memUsed) = MachineMemory();

		return new HealthInfo(
			PanelApi.Version,
			(long)(DateTime.Now - self.StartTime).TotalSeconds,
			$"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})" + (File.Exists("/.dockerenv") ? ", Docker" : ""),
			RuntimeInformation.FrameworkDescription,
			new ProcessHealth(procCpu, self.WorkingSet64, GC.GetTotalMemory(false), self.Threads.Count),
			new MachineHealth(Environment.ProcessorCount, sysCpu, memTotal, memUsed),
			Disk(library.Folder),
			library.GetSummary(),
			await ToolsAsync(freshTools),
			botHealth);
	}

	/// <summary>yt-dlp and ffmpeg versions, asked at most every 10 minutes (yt-dlp updates itself daily).</summary>
	public async Task<ToolVersions> ToolsAsync(bool fresh = false)
	{
		lock (gate)
			if (!fresh && tools != null && DateTime.UtcNow - toolsAt < TimeSpan.FromMinutes(10)) return tools;
		var yt = FirstLineAsync(config.YouTube.YtDlpPath, "--version");
		var ff = FirstLineAsync(config.YouTube.FfmpegPath, "-version");
		var ffLine = await ff;
		var result = new ToolVersions(await yt, ffLine is null ? null : FfmpegVersion().Match(ffLine) is { Success: true } m ? m.Groups[1].Value : ffLine);
		lock (gate) { tools = result; toolsAt = DateTime.UtcNow; }
		return result;
	}

	[GeneratedRegex(@"version\s+(\S+)")]
	private static partial Regex FfmpegVersion();

	static async Task<string?> FirstLineAsync(string file, string arg)
	{
		try
		{
			using var p = Process.Start(new ProcessStartInfo(file, arg)
			{
				RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
			});
			if (p is null) return null;
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			_ = p.StandardError.ReadToEndAsync(cts.Token);
			var output = await p.StandardOutput.ReadToEndAsync(cts.Token);
			await p.WaitForExitAsync(cts.Token);
			return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
		}
		catch { return null; }
	}

	(double? Process, double? Machine) SampleCpu()
	{
		using var self = Process.GetCurrentProcess();
		var now = Stopwatch.GetTimestamp();
		var proc = self.TotalProcessorTime;
		var machine = MachineCpuTicks();
		lock (gate)
		{
			if (lastCpu is { } last)
			{
				var wall = Stopwatch.GetElapsedTime(last.At, now).TotalSeconds;
				if (wall < 0.2) return (processCpu, machineCpu); // too close to the last reading
				processCpu = Math.Clamp((proc - last.Process).TotalSeconds / (wall * Environment.ProcessorCount) * 100, 0, 100);
				if (machine is { } m && last.Machine is { } lm && m.Total > lm.Total)
					machineCpu = Math.Clamp((double)(m.Busy - lm.Busy) / (m.Total - lm.Total) * 100, 0, 100);
			}
			lastCpu = (now, proc, machine);
			return (processCpu, machineCpu);
		}
	}

	// Busy and total CPU time of the whole machine since boot, in the OS's own units.
	static (ulong Busy, ulong Total)? MachineCpuTicks()
	{
		try
		{
			if (OperatingSystem.IsLinux())
			{
				// "cpu  user nice system idle iowait irq softirq steal ..."
				var f = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(ulong.Parse).ToArray();
				var total = f.Aggregate(0UL, (a, b) => a + b);
				return (total - f[3] - f[4], total);
			}
			if (OperatingSystem.IsWindows() && GetSystemTimes(out var idle, out var kernel, out var user))
				return ((ulong)(kernel + user - idle), (ulong)(kernel + user)); // kernel time includes idle
		}
		catch { }
		return null;
	}

	static (long? Total, long? Used) MachineMemory()
	{
		try
		{
			if (OperatingSystem.IsLinux())
			{
				long Kb(string key) => long.Parse(File.ReadLines("/proc/meminfo").First(l => l.StartsWith(key + ":"))
					.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]) * 1024;
				var total = Kb("MemTotal");
				return (total, total - Kb("MemAvailable"));
			}
			if (OperatingSystem.IsWindows())
			{
				var m = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
				if (GlobalMemoryStatusEx(ref m))
					return ((long)m.TotalPhys, (long)(m.TotalPhys - m.AvailPhys));
			}
		}
		catch { }
		return (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, null);
	}

	// The mount the folder is on: the drive whose root is the longest prefix of its path.
	static DiskHealth? Disk(string folder)
	{
		try
		{
			var full = Path.GetFullPath(folder);
			var drive = DriveInfo.GetDrives()
				.Where(d => d.IsReady && full.StartsWith(d.RootDirectory.FullName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
				.MaxBy(d => d.RootDirectory.FullName.Length);
			return drive is null ? null : new DiskHealth(drive.RootDirectory.FullName, drive.TotalSize, drive.AvailableFreeSpace);
		}
		catch { return null; }
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

	[StructLayout(LayoutKind.Sequential)]
	struct MemoryStatusEx
	{
		public uint Length, MemoryLoad;
		public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
	}
}
