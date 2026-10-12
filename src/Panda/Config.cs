using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Panda;

public sealed class PandaConfig
{
	/// <summary>Each bot has its own connection, queue and chat settings; everything else is shared.</summary>
	public List<BotConfig> Bots { get; set; } = [];
	public PanelSettings Panel { get; set; } = new();
	public YouTubeSettings YouTube { get; set; } = new();
	public LibrarySettings Library { get; set; } = new();

	// Up to 0.2 there was one bot, set up by these two at the top level. Load moves them into Bots.
	[JsonPropertyName("server"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public ServerSettings? OldServer { get; set; }
	[JsonPropertyName("bot"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public BotSettings? OldBot { get; set; }

	[JsonIgnore] public string Path { get; private set; } = "";

	static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	public static PandaConfig Load(string path)
	{
		PandaConfig cfg;
		if (File.Exists(path))
			cfg = JsonSerializer.Deserialize<PandaConfig>(File.ReadAllText(path), JsonOptions) ?? new();
		else
			cfg = new();
		cfg.Path = path;
		cfg.knownHash = cfg.Panel.PasswordHash;

		if (cfg.Bots.Count == 0)
			cfg.Bots.Add(new BotConfig { Id = "1", Server = cfg.OldServer ?? new(), Bot = cfg.OldBot ?? new() });
		cfg.OldServer = null;
		cfg.OldBot = null;
		// Hand-edited files: every bot needs a unique id that is safe as a folder name.
		var seen = new HashSet<string>();
		foreach (var b in cfg.Bots)
			if (!BotConfig.IsValidId(b.Id) || !seen.Add(b.Id))
				seen.Add(b.Id = cfg.NextBotId(seen));
		return cfg;
	}

	string NextBotId(IEnumerable<string> taken)
	{
		var used = taken.Concat(Bots.Select(b => b.Id)).ToHashSet();
		var n = 1;
		while (used.Contains(n.ToString())) n++;
		return n.ToString();
	}

	/// <summary>Adds a bot with default settings (connection copied from <paramref name="like"/>) and saves.</summary>
	public BotConfig AddBot(BotConfig? like)
	{
		BotConfig bot;
		lock (saveLock)
		{
			var id = NextBotId([]);
			bot = new BotConfig
			{
				Id = id,
				Server = new ServerSettings { Address = like?.Server.Address ?? new ServerSettings().Address, Password = like?.Server.Password ?? "" },
				Bot = new BotSettings { Nickname = $"Panda {id} 🐼", Language = like?.Bot.Language ?? "en" },
			};
			Bots = [.. Bots, bot];
		}
		Save();
		return bot;
	}

	public bool RemoveBot(string id)
	{
		lock (saveLock)
		{
			if (Bots.Count <= 1 || Bots.All(b => b.Id != id)) return false;
			Bots = Bots.Where(b => b.Id != id).ToList();
		}
		Save();
		return true;
	}

	readonly object saveLock = new();
	// The password hash as last read from or written to the file. `panda set-password` changes the
	// file while the bot is running; comparing against this tells such a change from our own.
	string knownHash = "";

	public void Save()
	{
		lock (saveLock)
		{
			AdoptPasswordFromFileLocked();
			SafeFile.Write(Path, JsonSerializer.Serialize(this, JsonOptions), ownerOnly: true);
			knownHash = Panel.PasswordHash;
		}
	}

	/// <summary>Picks up a password set with `panda set-password` while the bot is running.</summary>
	public void ReloadPassword()
	{
		lock (saveLock) AdoptPasswordFromFileLocked();
	}

	void AdoptPasswordFromFileLocked()
	{
		if (Panel.PasswordHash != knownHash) return; // changed here (e.g. in the panel): ours wins
		try
		{
			var onDisk = JsonSerializer.Deserialize<PandaConfig>(File.ReadAllText(Path), JsonOptions)?.Panel.PasswordHash;
			if (!string.IsNullOrEmpty(onDisk) && onDisk != knownHash)
				Panel.PasswordHash = knownHash = onDisk;
		}
		catch (Exception ex) when (ex is IOException or JsonException) { }
	}
}

public static class SafeFile
{
	const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

	/// <summary>
	/// Writes a temporary file, flushes it to disk and renames it over the target, so a crash or a
	/// power cut leaves either the old or the new file, never an empty or half-written one.
	/// </summary>
	/// <param name="ownerOnly">Only the owner may read it (passwords, keys). The file never exists with wider rights.</param>
	public static void Write(string path, string text, bool ownerOnly = false)
	{
		var tmp = path + ".tmp";
		var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
		if (ownerOnly && !OperatingSystem.IsWindows())
			options.UnixCreateMode = OwnerOnly;
		using (var fs = new FileStream(tmp, options))
		{
			fs.Write(Encoding.UTF8.GetBytes(text));
			fs.Flush(flushToDisk: true);
		}
		// UnixCreateMode only applies to new files; a leftover temp file keeps its old rights.
		if (ownerOnly && !OperatingSystem.IsWindows())
			File.SetUnixFileMode(tmp, OwnerOnly);
		File.Move(tmp, path, overwrite: true);
	}
}

public sealed class BotConfig
{
	/// <summary>Short and stable; also the name of the bot's folder (identity, history).</summary>
	public string Id { get; set; } = "";
	public ServerSettings Server { get; set; } = new();
	public BotSettings Bot { get; set; } = new();

	public static bool IsValidId(string id) =>
		id.Length is > 0 and <= 32 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

public sealed class ServerSettings
{
	/// <summary>host or host:port of the TeamSpeak server.</summary>
	public string Address { get; set; } = "127.0.0.1";
	public string Password { get; set; } = "";
	/// <summary>Channel name path ("Music/Lounge") or "/id". Empty = default channel.</summary>
	public string Channel { get; set; } = "";
	public string ChannelPassword { get; set; } = "";
}

public sealed class BotSettings
{
	public string Nickname { get; set; } = "Panda 🐼";
	public string CommandPrefix { get; set; } = "!";
	/// <summary>Language of chat replies: "en" or "tr".</summary>
	public string Language { get; set; } = "en";
	public int Volume { get; set; } = 50;
	/// <summary>Post "Now playing" in the channel when a song starts.</summary>
	public bool Announce { get; set; } = true;
	/// <summary>Show the current song in the bot's description.</summary>
	public bool ShowSongInDescription { get; set; } = true;
	/// <summary>Leave empty to let everyone use the bot. Otherwise only these client UIDs or server group IDs may.</summary>
	public List<string> AllowedUsers { get; set; } = new();
	public List<ulong> AllowedServerGroups { get; set; } = new();
	public int MaxQueueLength { get; set; } = 100;
}

public sealed class PanelSettings
{
	public string Listen { get; set; } = "0.0.0.0";
	public int Port { get; set; } = 8080;
	/// <summary>PBKDF2 hash; set with `panda set-password`.</summary>
	public string PasswordHash { get; set; } = "";
}

public sealed class YouTubeSettings
{
	public string YtDlpPath { get; set; } = "yt-dlp";
	public string FfmpegPath { get; set; } = "ffmpeg";
	/// <summary>Netscape cookies.txt, needed when YouTube asks to "confirm you're not a bot".</summary>
	public string CookiesFile { get; set; } = "";
	public int MaxDurationMinutes { get; set; } = 60;
}

public static class PasswordHasher
{
	const int Iterations = 100_000;

	public static string Hash(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(16);
		var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
		return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
	}

	public static bool Verify(string password, string stored)
	{
		var parts = stored.Split('$');
		if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
			return false;
		byte[] salt, expected;
		try
		{
			salt = Convert.FromBase64String(parts[2]);
			expected = Convert.FromBase64String(parts[3]);
		}
		catch (FormatException) { return false; } // a hand-edited config.json
		if (expected.Length == 0) return false;
		var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
		return CryptographicOperations.FixedTimeEquals(actual, expected);
	}

	public static string Generate() =>
		Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('+', 'x').Replace('/', 'y').TrimEnd('=');
}

public sealed class LibrarySettings
{
	/// <summary>Save every requested song on disk. When off, only the next song is downloaded ahead of time.</summary>
	public bool Enabled { get; set; } = true;
	/// <summary>When the library is bigger than this, the least played songs are deleted. 0 = no limit.</summary>
	public int MaxSizeMb { get; set; } = 5120;
	/// <summary>Folder for the songs; empty = "library" inside the data folder.</summary>
	public string Path { get; set; } = "";
}
