using System.Text.Json;

namespace Panda;

public sealed record BotSummary(string Id, string Nickname, string State, string? Song, long Played);

/// <summary>What every bot shares: where songs come from and where they are kept.</summary>
public sealed record BotShared(YouTube YouTube, Library Library, Playlists Playlists, Sources Sources, SponsorBlock SponsorBlock,
	RadioBrowser Radios, RadioFavorites Favorites);

/// <summary>One bot: its TeamSpeak connection, its player and its play history.</summary>
public sealed class BotInstance : IDisposable
{
	public BotConfig Config { get; }
	public string Id => Config.Id;
	public PlayHistory History { get; }
	public Player Player { get; }
	public TsBot Ts { get; }

	readonly string sessionPath;
	readonly ILogger log;

	internal BotInstance(BotConfig config, PandaConfig global, BotShared s, string dir, ILoggerFactory logs)
	{
		Config = config;
		Directory.CreateDirectory(dir);
		sessionPath = Path.Combine(dir, "session.json");
		log = logs.CreateLogger($"Panda.Bot.{config.Id}");
		History = new PlayHistory(dir, logs.CreateLogger<PlayHistory>());
		Player = new Player(config, global, s.YouTube, s.Library, s.SponsorBlock, History, logs.CreateLogger($"Panda.Player.{config.Id}"));
		Ts = new TsBot(config, global, Player, s.Library, s.Sources, s.Radios, s.Favorites, s.Playlists, dir, logs.CreateLogger($"Panda.TsBot.{config.Id}"));
		RestoreSession();
	}

	static readonly JsonSerializerOptions SessionJson = new(JsonSerializerDefaults.Web);

	/// <summary>Panda is stopping (an update or a restart): remember the queue and the song.</summary>
	public void SaveSession()
	{
		var s = Player.SaveSession();
		if (s.Current is null && s.Queue.Count == 0) return;
		try
		{
			SafeFile.Write(sessionPath, JsonSerializer.Serialize(s, SessionJson));
			log.LogInformation("Saved the queue ({Count} songs) for after the restart", s.Queue.Count + (s.Current is null ? 0 : 1));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			log.LogWarning("Could not save the queue: {Error}", ex.Message);
		}
	}

	// Read once: a session that breaks the start must not come back on every start.
	void RestoreSession()
	{
		if (!File.Exists(sessionPath)) return;
		try
		{
			var s = JsonSerializer.Deserialize<PlayerSession>(File.ReadAllText(sessionPath), SessionJson);
			File.Delete(sessionPath);
			if (s is null) return;
			Player.Restore(s with { Queue = s.Queue ?? [] });
			log.LogInformation("Picked up where it left off: {Title}, {Count} songs queued", s.Current?.Title ?? "-", s.Queue?.Count ?? 0);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			log.LogWarning("Could not restore the queue: {Error}", ex.Message);
			try { File.Delete(sessionPath); } catch (IOException) { }
		}
	}

	public BotSummary Summary() => new(Id, Config.Bot.Nickname, Ts.State, Player.Current?.Title, History.Added);

	/// <summary>Lets panel endpoints take the bot named in the route (/api/bots/{bot}/...) as a parameter.</summary>
	public static ValueTask<BotInstance?> BindAsync(HttpContext ctx) =>
		ValueTask.FromResult(ctx.RequestServices.GetRequiredService<BotManager>().Get(ctx.GetRouteValue("bot") as string ?? ""));

	public void Dispose()
	{
		Ts.Dispose();
		Player.Dispose();
	}
}

/// <summary>Starts a bot for every entry in config.Bots; bots can be added and removed while running.</summary>
public sealed class BotManager(PandaConfig config, BotShared shared, DataPaths paths, ILoggerFactory logs, ILogger<BotManager> log)
	: IHostedService, IDisposable
{
	readonly object gate = new();
	readonly List<BotInstance> bots = [];

	/// <summary>A bot was added or removed, or the state or song of any bot changed.</summary>
	public event Action? Changed;

	public IReadOnlyList<BotInstance> All { get { lock (gate) return bots.ToArray(); } }
	public BotInstance? Get(string id) { lock (gate) return bots.FirstOrDefault(b => b.Id == id); }
	public IReadOnlyList<BotSummary> Summaries() => All.Select(b => b.Summary()).ToList();

	public async Task StartAsync(CancellationToken ct)
	{
		CopySingleBotFiles();
		foreach (var c in config.Bots)
			await StartBotAsync(c, ct);
	}

	async Task<BotInstance> StartBotAsync(BotConfig c, CancellationToken ct)
	{
		var b = new BotInstance(c, config, shared, paths.BotDir(c.Id), logs);
		var made = b;
		b.Ts.CreateBot = channel => CreateFromChatAsync(made, channel);
		b.Player.Changed += RaiseChanged;
		b.Ts.StatusChanged += RaiseChanged;
		lock (gate) bots.Add(b);
		// Started on the thread pool: a bot made from another bot's chat must not run on that bot's thread.
		await Task.Run(() => b.Ts.StartAsync(ct));
		return b;
	}

	// Adding and removing one at a time: a new bot must not get the id (and folder) of one that is
	// still being taken down.
	readonly SemaphoreSlim changing = new(1);

	/// <summary>A new bot on the same server as <paramref name="like"/>; it connects right away.</summary>
	public async Task<BotInstance> AddAsync(BotInstance? like) => (await AddAsync(like, null, null))!;

	/// <param name="check">Looked at while nothing else is added or removed; false: no bot is added (null).</param>
	public async Task<BotInstance?> AddAsync(BotInstance? like, Func<bool>? check, Action<BotConfig>? setUp)
	{
		await changing.WaitAsync();
		try
		{
			if (check != null && !check()) return null;
			// A folder left behind (it couldn't be deleted) keeps its id taken, so nobody inherits its identity.
			var leftovers = Directory.Exists(Path.Combine(paths.Data, "bots"))
				? Directory.EnumerateDirectories(Path.Combine(paths.Data, "bots")).Select(d => Path.GetFileName(d)!)
				: [];
			var c = config.AddBot(like?.Config, leftovers.ToList());
			if (setUp != null)
			{
				setUp(c);
				config.Save();
			}
			var b = await StartBotAsync(c, CancellationToken.None);
			log.LogInformation("Added bot {Id}", b.Id);
			RaiseChanged();
			return b;
		}
		finally { changing.Release(); }
	}

	/// <summary>
	/// "!create bot": a new bot on the same server, in the channel of who asked, with the same chat
	/// permissions and language, and a prefix no other bot uses. Null and a reason when it can't.
	/// </summary>
	public async Task<(BotInstance? Bot, string? Error)> CreateFromChatAsync(BotInstance from, ulong channel)
	{
		string? prefix = null;
		// The count and the prefix are looked at while no other bot is being added (two !create at once).
		var b = await AddAsync(from, check: () =>
		{
			if (All.Count >= Math.Max(1, config.Panel.MaxBots)) return false;
			var used = All.Select(x => x.Config.Bot.CommandPrefix).ToHashSet();
			prefix = new[] { "!", ".", "?", "-", "+", "~", ">", "$", "%", "&" }.FirstOrDefault(p => !used.Contains(p));
			return prefix != null;
		}, setUp: c =>
		{
			c.Server.Channel = "/" + channel;
			var s = c.Bot;
			var f = from.Config.Bot;
			s.CommandPrefix = prefix!;
			// A bot made from the chat goes when nobody listens (30 min unless the first bot says otherwise).
			s.LeaveWhenEmpty = f.LeaveWhenEmpty > 0 ? f.LeaveWhenEmpty : 30;
			s.EveryoneCanUse = f.EveryoneCanUse;
			s.Members = [.. f.Members]; s.MemberGroups = [.. f.MemberGroups];
			s.Admins = [.. f.Admins]; s.AdminGroups = [.. f.AdminGroups];
			s.Djs = [.. f.Djs]; s.DjGroups = [.. f.DjGroups];
			s.Blocked = [.. f.Blocked];
			s.CommandLevels = new(f.CommandLevels);
			s.OthersSongs = f.OthersSongs;
			s.SettingsVersion = f.SettingsVersion;
		});
		return b is null ? (null, "too-many-bots") : (b, null);
	}

	/// <summary>Disconnects the bot and deletes its identity and history. The last bot can't be removed.</summary>
	public async Task<bool> RemoveAsync(string id)
	{
		await changing.WaitAsync();
		try
		{
			var b = Get(id);
			if (b is null || !config.RemoveBot(id)) return false;
			lock (gate) bots.Remove(b);
			b.Player.Changed -= RaiseChanged;
			b.Ts.StatusChanged -= RaiseChanged;
			await b.Ts.StopAsync(CancellationToken.None);
			b.Dispose();
			shared.Library.Forget(id);
			try { Directory.Delete(paths.BotDir(id), recursive: true); }
			catch (Exception ex) { log.LogWarning("Could not delete {Dir}: {Error}", paths.BotDir(id), ex.Message); }
			log.LogInformation("Removed bot {Id}", id);
			RaiseChanged();
			return true;
		}
		finally { changing.Release(); }
	}

	public Task StopAsync(CancellationToken ct)
	{
		foreach (var b in All) b.SaveSession();
		return Task.WhenAll(All.Select(b => b.Ts.StopAsync(ct)));
	}

	// Up to 0.2 the only bot kept its files in the data folder itself. The identity matters most:
	// server groups belong to it. Copied, not moved, so an older version still finds them.
	void CopySingleBotFiles()
	{
		if (Directory.Exists(Path.Combine(paths.Data, "bots")) || config.Bots.Count == 0) return;
		var dir = paths.BotDir(config.Bots[0].Id);
		Directory.CreateDirectory(dir);
		foreach (var name in new[] { "identity.json", "history.jsonl" })
		{
			var old = Path.Combine(paths.Data, name);
			if (!File.Exists(old)) continue;
			File.Copy(old, Path.Combine(dir, name), overwrite: false);
			if (!OperatingSystem.IsWindows())
				File.SetUnixFileMode(Path.Combine(dir, name), UnixFileMode.UserRead | UnixFileMode.UserWrite);
			log.LogInformation("Copied {File} to {Dir}", name, dir);
		}
	}

	void RaiseChanged()
	{
		try { Changed?.Invoke(); }
		catch (Exception ex) { log.LogError(ex, "Changed handler failed"); }
	}

	// Called twice: the container disposes the singleton and the hosted service made from it.
	public void Dispose()
	{
		BotInstance[] all;
		lock (gate) { all = bots.ToArray(); bots.Clear(); }
		foreach (var b in all) b.Dispose();
	}
}
