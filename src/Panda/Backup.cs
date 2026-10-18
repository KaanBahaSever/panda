using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Panda;

/// <summary>
/// A backup of what makes this Panda itself: the settings (bots, panel users, keys), each bot's TeamSpeak
/// identity and play history, the playlists, favorite stations and cookies. Not the saved songs (they come
/// back by themselves). Restoring writes the files and restarts Panda.
/// </summary>
public static partial class Backup
{
	[GeneratedRegex(@"^(config\.json|playlists\.json|radios\.json|cookies\.txt|bots/[A-Za-z0-9_-]{1,32}/(identity\.json|history\.jsonl))$")]
	private static partial Regex AllowedName();

	const long MaxFile = 20 * 1024 * 1024;
	public const long MaxZip = 50 * 1024 * 1024;

	public static byte[] Make(DataPaths paths)
	{
		var ms = new MemoryStream();
		using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
		{
			void Add(string rel)
			{
				var full = Path.Combine(paths.Data, rel.Replace('/', Path.DirectorySeparatorChar));
				if (!File.Exists(full)) return;
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

	/// <summary>Checks the whole backup first, then writes it. Null when done, else what's wrong.</summary>
	public static string? Restore(Stream zipStream, DataPaths paths)
	{
		var files = new List<(string Name, byte[] Data)>();
		try
		{
			using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
			foreach (var e in zip.Entries)
			{
				if (e.FullName.EndsWith('/')) continue;
				if (!AllowedName().IsMatch(e.FullName) || e.Length > MaxFile) return "bad-backup";
				using var s = e.Open();
				var buf = new MemoryStream();
				s.CopyTo(buf);
				if (buf.Length > MaxFile) return "bad-backup";
				files.Add((e.FullName, buf.ToArray()));
			}
		}
		catch (InvalidDataException) { return "bad-backup"; }
		// It has to be a Panda's: a config that reads.
		var config = files.FirstOrDefault(f => f.Name == "config.json");
		if (config.Data is null) return "bad-backup";
		try { using var _ = JsonDocument.Parse(config.Data, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
		catch (JsonException) { return "bad-backup"; }
		foreach (var (name, data) in files)
		{
			var full = Path.Combine(paths.Data, name.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(full)!);
			SafeFile.Write(full, System.Text.Encoding.UTF8.GetString(data), ownerOnly: name is "config.json" or "cookies.txt" || name.EndsWith("identity.json"));
		}
		return null;
	}
}
