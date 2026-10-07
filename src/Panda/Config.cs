using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Panda;

public sealed class PandaConfig
{
	public ServerSettings Server { get; set; } = new();
	public BotSettings Bot { get; set; } = new();
	public PanelSettings Panel { get; set; } = new();
	public YouTubeSettings YouTube { get; set; } = new();
	public LibrarySettings Library { get; set; } = new();

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
		return cfg;
	}

	readonly object saveLock = new();

	public void Save()
	{
		lock (saveLock)
		{
			var tmp = Path + ".tmp";
			WritePrivate(tmp, JsonSerializer.Serialize(this, JsonOptions));
			File.Move(tmp, Path, overwrite: true);
		}
	}

	/// <summary>Writes a file only its owner can read (it holds passwords and keys).</summary>
	public static void WritePrivate(string path, string text)
	{
		File.WriteAllText(path, text);
		if (!OperatingSystem.IsWindows())
			File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
	}
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
		if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations))
			return false;
		var salt = Convert.FromBase64String(parts[2]);
		var expected = Convert.FromBase64String(parts[3]);
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
