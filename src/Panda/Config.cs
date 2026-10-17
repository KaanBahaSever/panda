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
	public AudioSettings Audio { get; set; } = new();
	public SourcesSettings Sources { get; set; } = new();

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
		foreach (var b in cfg.Bots)
			ChatPermissions.Upgrade(b.Bot);
		cfg.Panel.ApiKeys.RemoveAll(k => k.Hash.Length == 0 || !Roles.IsValid(k.Role));
		// A hand-edited config.json may say null.
		cfg.Sources ??= new();
		cfg.YouTube.SponsorBlockCategories = (cfg.YouTube.SponsorBlockCategories ?? []).Where(YouTubeSettings.SponsorCategories.Contains).Distinct().ToList();
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
	/// <param name="taken">Ids that are not free although no bot has them (a removed bot's folder still there).</param>
	public BotConfig AddBot(BotConfig? like, IEnumerable<string> taken)
	{
		BotConfig bot;
		lock (saveLock)
		{
			var id = NextBotId(taken);
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

	/// <summary>A user was added, removed or got a new role or password here (open panel streams recheck theirs).</summary>
	public event Action? UsersChanged;

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

	/// <summary>
	/// Adds a user or, when the name exists, gives it a new password (and role). Saves. Only for
	/// "add user" and `panda set-password`; requests about an existing user use <see cref="SetPassword"/>.
	/// </summary>
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
		UsersChanged?.Invoke();
		return user;
	}

	/// <summary>Adds a new user; false when the name is taken.</summary>
	public bool AddUser(string name, string password, string role)
	{
		var user = new PanelUser { Name = name, Role = role, PasswordHash = PasswordHasher.Hash(password), Stamp = PasswordHasher.Generate() };
		lock (saveLock)
		{
			if (Panel.Users.Any(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;
			Panel.Users = [.. Panel.Users, user];
		}
		Save();
		UsersChanged?.Invoke();
		return true;
	}

	/// <summary>
	/// New password for an existing user (logs out their other sessions). Null when the user is gone,
	/// e.g. removed while their request was on its way: that must not bring them back.
	/// </summary>
	public PanelUser? SetPassword(string name, string password)
	{
		var hash = PasswordHasher.Hash(password);
		PanelUser? user;
		lock (saveLock)
		{
			user = Panel.Users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (user is null) return null;
			user.PasswordHash = hash;
			user.Stamp = PasswordHasher.Generate();
		}
		Save();
		UsersChanged?.Invoke();
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
		UsersChanged?.Invoke();
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
		UsersChanged?.Invoke();
		return true;
	}

	/// <summary>Makes an API key; returns it with the key itself, which is shown only this once.</summary>
	public (ApiKey Key, string Secret) AddApiKey(string name, string role)
	{
		var secret = "pk_" + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
			.TrimEnd('=').Replace('+', '-').Replace('/', '_');
		var key = new ApiKey
		{
			Id = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6)).ToLowerInvariant(),
			Name = name, Role = role, Hash = ApiKey.HashOf(secret), Created = DateTimeOffset.UtcNow,
		};
		lock (saveLock) Panel.ApiKeys = [.. Panel.ApiKeys, key];
		Save();
		UsersChanged?.Invoke();
		return (key, secret);
	}

	public bool RemoveApiKey(string id)
	{
		lock (saveLock)
		{
			if (Panel.ApiKeys.All(k => k.Id != id)) return false;
			Panel.ApiKeys = Panel.ApiKeys.Where(k => k.Id != id).ToList();
		}
		Save();
		UsersChanged?.Invoke();
		return true;
	}

	/// <summary>The key with this secret, or null. Notes when it was last used (saved now and then, not on every call).</summary>
	public ApiKey? FindApiKey(string secret)
	{
		var hash = ApiKey.HashOf(secret);
		ApiKey? key;
		bool save;
		lock (saveLock)
		{
			key = Panel.ApiKeys.FirstOrDefault(k => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
				System.Text.Encoding.ASCII.GetBytes(k.Hash), System.Text.Encoding.ASCII.GetBytes(hash)));
			if (key is null) return null;
			save = key.LastUsed is not { } last || DateTimeOffset.UtcNow - last > TimeSpan.FromHours(1);
			key.LastUsed = DateTimeOffset.UtcNow;
		}
		if (save) Save();
		return key;
	}

	public ApiKey? FindApiKeyById(string id)
	{
		lock (saveLock) return Panel.ApiKeys.FirstOrDefault(k => k.Id == id);
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

/// <summary>A key a program sends instead of logging in: "Authorization: Bearer pk_...". Only its hash is kept.</summary>
public sealed class ApiKey
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Role { get; set; } = Roles.Dj;
	/// <summary>SHA-256 of the key, hex. The key itself is shown once, when it is made.</summary>
	public string Hash { get; set; } = "";
	public DateTimeOffset Created { get; set; }
	public DateTimeOffset? LastUsed { get; set; }

	public static string HashOf(string key) =>
		Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
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
	/// <summary>Language of chat replies: "en", "tr", "fr" or "de".</summary>
	public string Language { get; set; } = "en";
	public int Volume { get; set; } = 50;
	/// <summary>Post "Now playing" in the channel when a song starts.</summary>
	public bool Announce { get; set; } = true;
	/// <summary>
	/// Confirm chat commands that change something ("Added …", "Skipped"). Questions (!np, !queue,
	/// !help) and errors are always answered.
	/// </summary>
	public bool ConfirmCommands { get; set; } = true;
	/// <summary>Show the current song in the bot's description.</summary>
	public bool ShowSongInDescription { get; set; } = true;
	/// <summary>Up to 0.4: only these unique ids / server groups could use the bot. Load turns them into DJs.</summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<string>? AllowedUsers { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<ulong>? AllowedServerGroups { get; set; }

	// ---- who may do what in the chat (see ChatPermissions) ----
	/// <summary>Anyone on the server may use the bot; off: only the members, DJs and admins.</summary>
	public bool EveryoneCanUse { get; set; } = true;
	/// <summary>With <see cref="EveryoneCanUse"/> off: who may use the bot (as "everyone" would).</summary>
	public List<ChatPerson> Members { get; set; } = [];
	public List<ulong> MemberGroups { get; set; } = [];
	public List<ChatPerson> Admins { get; set; } = [];
	public List<ulong> AdminGroups { get; set; } = [];
	public List<ChatPerson> Djs { get; set; } = [];
	public List<ulong> DjGroups { get; set; } = [];
	/// <summary>Never answered, whatever group they are in.</summary>
	public List<ChatPerson> Blocked { get; set; } = [];
	/// <summary>Command → "everyone", "dj" or "admin"; a command that isn't here is for everyone.</summary>
	public Dictionary<string, string> CommandLevels { get; set; } = [];
	/// <summary>Who may skip, remove or move other people's songs ("everyone", "dj", "admin"). Below it,
	/// skipping is a vote when <see cref="VoteSkip"/> is on.</summary>
	public string? OthersSongs { get; set; }
	/// <summary>Only people in the bot's channel may give commands (admins anywhere).</summary>
	public bool OnlyFromChannel { get; set; }
	public int MaxQueueLength { get; set; } = 100;
	/// <summary>Songs one person may have waiting in the queue; 0 = no limit. DJs and admins aren't limited.</summary>
	public int MaxSongsPerUser { get; set; }
	/// <summary>People who may not change others' songs vote to skip them; half of the bot's channel skips the song.</summary>
	public bool VoteSkip { get; set; }
	/// <summary>When the queue runs out, keep playing songs like the last one (YouTube's Mix).</summary>
	public bool Autoplay { get; set; }
	/// <summary>Autoplay up to 0.5, when it was called radio; Load moves it.</summary>
	[JsonPropertyName("radio"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? OldRadio { get; set; }
}

public sealed class PanelSettings
{
	public string Listen { get; set; } = "0.0.0.0";
	public int Port { get; set; } = 8080;
	public List<PanelUser> Users { get; set; } = [];
	/// <summary>Keys for programs (scripts, a management panel) that use the API without logging in.</summary>
	public List<ApiKey> ApiKeys { get; set; } = [];
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
	/// <summary>
	/// Tries per song when YouTube refuses it (403, bot check, a broken stream...) before the bot
	/// says it couldn't play it. YouTube does this now and then; a new try usually works.
	/// </summary>
	public int PlayAttempts { get; set; } = 3;
	/// <summary>YouTube clients yt-dlp uses from the third try on (yt-dlp's player_client); empty = yt-dlp's own choice.</summary>
	public string RetryPlayerClients { get; set; } = "android_vr,tv,web_safari";
	/// <summary>"best", or "saver": a smaller audio stream (about 70 kbps), less data and smaller saved songs.</summary>
	public string Quality { get; set; } = "best";
	/// <summary>Results !search shows (1–10).</summary>
	public int SearchResults { get; set; } = 5;
	/// <summary>Searches leave out live streams.</summary>
	public bool HideLive { get; set; }
	/// <summary>Searches leave out videos under a minute (Shorts, clips).</summary>
	public bool HideShort { get; set; }
	/// <summary>Skip the parts of a video that aren't music, as marked on SponsorBlock (sponsor.ajay.app).</summary>
	public bool SponsorBlock { get; set; }
	/// <summary>SponsorBlock's kinds of parts to skip: music_offtopic, sponsor, selfpromo, intro, outro.</summary>
	public List<string> SponsorBlockCategories { get; set; } = ["music_offtopic"];
	/// <summary>The SponsorBlock server.</summary>
	public string SponsorBlockUrl { get; set; } = "https://sponsor.ajay.app";
	/// <summary>Autoplay doesn't pick a song played among the last this many (up to 30).</summary>
	public int AutoplayNoRepeat { get; set; } = 30;
	/// <summary>Autoplay may pick the same artist (channel) as the song before.</summary>
	public bool AutoplaySameArtist { get; set; } = true;

	public static readonly string[] Qualities = ["best", "saver"];
	public static readonly string[] SponsorCategories = ["music_offtopic", "sponsor", "selfpromo", "intro", "outro"];
}

/// <summary>Where songs may come from besides YouTube, and where a search looks.</summary>
public sealed class SourcesSettings
{
	/// <summary>Where a search (!play with words, !search, the panel) looks first: "youtube",
	/// "youtubeMusic" or "soundcloud". The others are asked next when it finds nothing (see <see cref="Fallback"/>).</summary>
	public string Search { get; set; } = "youtube";
	public bool Fallback { get; set; } = true;
	public bool YouTubeMusic { get; set; } = true;
	public bool SoundCloud { get; set; } = true;
	/// <summary>Links to other sites yt-dlp can play (Bandcamp, Mixcloud, Vimeo, Twitch...) and audio files.</summary>
	public bool Sites { get; set; } = true;
	/// <summary>Spotify, Apple Music, Deezer and Tidal links: the song is looked up on YouTube.</summary>
	public bool Services { get; set; } = true;
	/// <summary>Any other link: the page's title is looked up on YouTube.</summary>
	public bool Pages { get; set; } = true;
	/// <summary>Internet radio: stream links, !fm and the radio list.</summary>
	public bool Radio { get; set; } = true;
	/// <summary>Links may point into the local network (a radio or a NAS at home). Off: such links are refused,
	/// so nobody can make Panda open a router's page or a cloud metadata service.</summary>
	public bool LocalLinks { get; set; }
	/// <summary>The Radio Browser server; empty = one of radio-browser.info's.</summary>
	public string RadioBrowserUrl { get; set; } = "";

	public static readonly string[] Searches = ["youtube", "youtubeMusic", "soundcloud"];

	/// <summary>The searches to try, in order.</summary>
	public IEnumerable<string> SearchOrder()
	{
		var first = Searches.Contains(Search) && Enabled(Search) ? Search : "youtube";
		yield return first;
		if (!Fallback) yield break;
		foreach (var s in Searches)
			if (s != first && Enabled(s)) yield return s;
	}

	public bool Enabled(string search) => search switch
	{
		"youtubeMusic" => YouTubeMusic,
		"soundcloud" => SoundCloud,
		_ => true,
	};
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

public sealed class AudioSettings
{
	/// <summary>
	/// Play saved songs equally loud: each is measured once (EBU R128) and played with a fixed gain,
	/// so its own dynamics stay. Songs not measured yet (the first play) play as they are.
	/// </summary>
	public bool Normalize { get; set; } = true;
	/// <summary>Integrated loudness to aim for, in LUFS. -14 is what YouTube and Spotify use.</summary>
	public double TargetLufs { get; set; } = -14;
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
