using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Panda;

/// <summary>
/// A backup of what makes this Panda itself: the settings (bots, panel users, keys), each bot's TeamSpeak
/// identity and play history, the playlists, favorite stations and cookies. Not the saved songs (they come
/// back by themselves). A restore is checked, put aside in data/.restore and swapped in when Panda starts
/// again (<see cref="ApplyPending"/>), before anything is loaded: nothing still running can overwrite it.
/// </summary>
public static partial class Backup
{
	[GeneratedRegex(@"^(config\.json|playlists\.json|radios\.json|cookies\.txt|bots/[A-Za-z0-9_-]{1,32}/(identity\.json|history\.jsonl))$")]
	private static partial Regex AllowedName();

	const long MaxFile = 20 * 1024 * 1024;
	const long MaxTotal = 200 * 1024 * 1024;
	const int MaxEntries = 4 + 2 * 100;
	public const long MaxZip = 50 * 1024 * 1024;
	const string Pending = ".restore";

	public static byte[] Make(DataPaths paths)
	{
		var ms = new MemoryStream();
		using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
		{
			void Add(string rel)
			{
				var full = Path.Combine(paths.Data, rel.Replace('/', Path.DirectorySeparatorChar));
				// A file a restore wouldn't take (a very long history) is left out rather than spoil the backup.
				if (!File.Exists(full) || new FileInfo(full).Length > MaxFile) return;
				var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
				using var to = entry.Open();
				using var from = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				from.CopyTo(to);
			}
			foreach (var f in new[] { "config.json", "playlists.json", "radios.json", "cookies.txt" }) Add(f);
			var bots = Path.Combine(paths.Data, "bots");
			if (Directory.Exists(bots))
				foreach (var dir in Directory.EnumerateDirectories(bots))
				{
					var id = Path.GetFileName(dir);
					if (!BotConfig.IsValidId(id)) continue;
					Add($"bots/{id}/identity.json");
					Add($"bots/{id}/history.jsonl");
				}
		}
		return ms.ToArray();
	}

	/// <summary>Checks the whole backup and puts it aside for the next start. Null when done, else what's wrong.</summary>
	public static string? Stage(Stream zipStream, DataPaths paths)
	{
		var files = new Dictionary<string, byte[]>();
		long total = 0;
		try
		{
			using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
			if (zip.Entries.Count > MaxEntries) return "bad-backup";
			foreach (var e in zip.Entries)
			{
				if (e.FullName.EndsWith('/')) continue;
				if (!AllowedName().IsMatch(e.FullName) || files.ContainsKey(e.FullName) || e.Length > MaxFile) return "bad-backup";
				var data = ReadBounded(e);
				if (data is null || (total += data.Length) > MaxTotal) return "bad-backup";
				files[e.FullName] = data;
			}
		}
		catch (InvalidDataException) { return "bad-backup"; }
		// It has to be a Panda's that starts and lets an admin in: bots and an admin with a password.
		if (!files.TryGetValue("config.json", out var config)) return "bad-backup";
		var tmp = Path.Combine(paths.Data, ".restore-check.json");
		try
		{
			File.WriteAllBytes(tmp, config);
			var c = PandaConfig.Load(tmp);
			if (c.Bots.Count == 0 || !c.Panel.Users.Any(u => u.Role == Roles.Admin && u.PasswordHash.Length > 0)) return "bad-backup";
		}
		catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or NotSupportedException) { return "bad-backup"; }
		finally { try { File.Delete(tmp); } catch (IOException) { } }

		// Written beside, then renamed into place in one step: a half-written backup is never applied.
		var staging = Path.Combine(paths.Data, Pending + "-new");
		var pending = Path.Combine(paths.Data, Pending);
		if (Directory.Exists(staging)) Directory.Delete(staging, true);
		foreach (var (name, data) in files)
		{
			var full = Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(full)!);
			File.WriteAllBytes(full, data);
		}
		if (Directory.Exists(pending)) Directory.Delete(pending, true);
		Directory.Move(staging, pending);
		return null;
	}

	static byte[]? ReadBounded(ZipArchiveEntry e)
	{
		using var s = e.Open();
		var buf = new MemoryStream();
		var chunk = new byte[81920];
		int n;
		while ((n = s.Read(chunk)) > 0)
		{
			if (buf.Length + n > MaxFile) return null;
			buf.Write(chunk, 0, n);
		}
		return buf.ToArray();
	}

	/// <summary>When Panda starts: a restored backup waiting in data/.restore replaces the files, and the
	/// queues saved at the last stop are forgotten (the restored setup may be a different one).</summary>
	public static bool ApplyPending(string dataDir)
	{
		var pending = Path.Combine(dataDir, Pending);
		if (!Directory.Exists(pending)) return false;
		foreach (var file in Directory.EnumerateFiles(pending, "*", SearchOption.AllDirectories))
		{
			var rel = Path.GetRelativePath(pending, file);
			var to = Path.Combine(dataDir, rel);
			Directory.CreateDirectory(Path.GetDirectoryName(to)!);
			var secret = rel is "config.json" or "cookies.txt" || rel.EndsWith("identity.json");
			SafeFile.Write(to, File.ReadAllText(file), ownerOnly: secret);
		}
		var bots = Path.Combine(dataDir, "bots");
		if (Directory.Exists(bots))
			foreach (var f in Directory.EnumerateFiles(bots, "session.json", SearchOption.AllDirectories)) File.Delete(f);
		Directory.Delete(pending, true);
		return true;
	}
}
