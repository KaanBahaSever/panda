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

		// Up to 0.2 the panel had one password and no user names.
		if (cfg.Panel.Users.Count == 0 && !string.IsNullOrEmpty(cfg.Panel.PasswordHash))
			cfg.Panel.Users.Add(new PanelUser { Name = "admin", Role = Roles.Admin, PasswordHash = cfg.Panel.PasswordHash, Stamp = PasswordHasher.Generate() });
		cfg.Panel.PasswordHash = null;
		cfg.Panel.Users.RemoveAll(u => !PanelUser.IsValidName(u.Name) || u.PasswordHash.Length == 0);
		foreach (var u in cfg.Panel.Users.Where(u => !Roles.IsValid(u.Role))) u.Role = Roles.Dj;
		cfg.knownUsers = cfg.UsersJson();

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

	// ---- panel users ----

	public PanelUser? FindUser(string name)
	{
		lock (saveLock) return Panel.Users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>The user when the password is right. Also tries again after picking up a password
	/// that `panda set-password` just wrote to the file.</summary>
	public PanelUser? CheckLogin(string name, string password)
	{
		if (Check() is { } user) return user;
		ReloadUsers();
		return Check();

		PanelUser? Check()
		{
			var u = FindUser(name);
			// The same work for unknown names, so the answer time doesn't tell which names exist.
			var ok = PasswordHasher.Verify(password, u?.PasswordHash ?? DummyHash);
			return ok ? u : null;
		}
	}

	static readonly string DummyHash = PasswordHasher.Hash(PasswordHasher.Generate());

	/// <summary>Adds a user or, when the name exists, gives it a new password (and role). Saves.</summary>
	public PanelUser SetUser(string name, string password, string? role = null)
	{
		PanelUser user;
		lock (saveLock)
		{
			user = Panel.Users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				?? new PanelUser { Name = name, Role = Roles.Admin };
			if (role != null) user.Role = role;
			user.PasswordHash = PasswordHasher.Hash(password);
			user.Stamp = PasswordHasher.Generate(); // logs out the user's other sessions
			if (!Panel.Users.Contains(user)) Panel.Users = [.. Panel.Users, user];
		}
		Save();
		return user;
	}

	/// <summary>Changes a user's role. The last admin always stays an admin.</summary>
	public bool SetRole(string name, string role)
	{
		lock (saveLock)
		{
			var user = Panel.Users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (user is null || (role != Roles.Admin && IsLastAdminLocked(user))) return false;
			user.Role = role;
		}
		Save();
		return true;
	}

	/// <summary>Removes a user. The last admin can't be removed.</summary>
	public bool RemoveUser(string name)
	{
		lock (saveLock)
		{
			var user = Panel.Users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (user is null || IsLastAdminLocked(user)) return false;
			Panel.Users = Panel.Users.Where(u => u != user).ToList();
		}
		Save();
		return true;
	}

	bool IsLastAdminLocked(PanelUser user) =>
		user.Role == Roles.Admin && Panel.Users.Count(u => u.Role == Roles.Admin) <= 1;

	// ---- saving ----

	readonly object saveLock = new();
	// The users as last read from or written to the file. `panda set-password` changes the file
	// while the bot is running; comparing against this tells such a change from our own.
	string knownUsers = "";

	string UsersJson() => JsonSerializer.Serialize(Panel.Users, JsonOptions);

	public void Save()
	{
		lock (saveLock)
		{
			AdoptUsersFromFileLocked();
			SafeFile.Write(Path, JsonSerializer.Serialize(this, JsonOptions), ownerOnly: true);
			knownUsers = UsersJson();
		}
	}

	/// <summary>Picks up users or passwords changed with `panda set-password` while the bot is running.</summary>
	public void ReloadUsers()
	{
		lock (saveLock) AdoptUsersFromFileLocked();
	}

	void AdoptUsersFromFileLocked()
	{
		if (UsersJson() != knownUsers) return; // changed here (e.g. in the panel): ours wins
		try
		{
			var onDisk = Load(Path).Panel.Users;
			if (onDisk.Count > 0 && JsonSerializer.Serialize(onDisk, JsonOptions) != knownUsers)
			{
				Panel.Users = onDisk;
				knownUsers = UsersJson();
			}
		}
		catch (Exception ex) when (ex is IOException or JsonException) { }
	}
}

/// <summary>Someone who can log in to the panel.</summary>
public sealed class PanelUser
{
	public string Name { get; set; } = "";
	/// <summary>See <see cref="Roles"/>.</summary>
	public string Role { get; set; } = Roles.Admin;
	/// <summary>PBKDF2 hash; set in the panel or with `panda set-password`.</summary>
	public string PasswordHash { get; set; } = "";
	/// <summary>Changes with the password; sessions made with an older one are logged out.</summary>
	public string Stamp { get; set; } = "";

	public static bool IsValidName(string name) =>
		name.Length is >= 2 and <= 32 && name.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');
}

public static class Roles
{
	/// <summary>Everything: playback, settings, bots, users.</summary>
	public const string Admin = "admin";
	/// <summary>Can watch and add songs to the queue, nothing else.</summary>
	public const string Dj = "dj";

	public static bool IsValid(string role) => role is Admin or Dj;
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
	/// <summary>When the queue runs out, keep playing songs like the last one.</summary>
	public bool Radio { get; set; }
}

public sealed class PanelSettings
{
	public string Listen { get; set; } = "0.0.0.0";
	public int Port { get; set; } = 8080;
	public List<PanelUser> Users { get; set; } = [];
	/// <summary>The single panel password up to 0.2; Load turns it into the "admin" user.</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? PasswordHash { get; set; }
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
