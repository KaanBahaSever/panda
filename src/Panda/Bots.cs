namespace Panda;

public sealed record BotSummary(string Id, string Nickname, string State, string? Song);

/// <summary>One bot: its TeamSpeak connection, its player and its play history.</summary>
public sealed class BotInstance : IDisposable
{
	public BotConfig Config { get; }
	public string Id => Config.Id;
	public PlayHistory History { get; }
	public Player Player { get; }
	public TsBot Ts { get; }

	internal BotInstance(BotConfig config, PandaConfig global, YouTube youtube, Library library, Playlists playlists, string dir, ILoggerFactory logs)
	{
		Config = config;
		Directory.CreateDirectory(dir);
		History = new PlayHistory(dir, logs.CreateLogger<PlayHistory>());
		Player = new Player(config, global, youtube, library, History, logs.CreateLogger($"Panda.Player.{config.Id}"));
		Ts = new TsBot(config, Player, library, playlists, dir, logs.CreateLogger($"Panda.TsBot.{config.Id}"));
	}

	public BotSummary Summary() => new(Id, Config.Bot.Nickname, Ts.State, Player.Current?.Title);

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
public sealed class BotManager(PandaConfig config, YouTube youtube, Library library, Playlists playlists, DataPaths paths, ILoggerFactory logs, ILogger<BotManager> log)
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
		var b = new BotInstance(c, config, youtube, library, playlists, paths.BotDir(c.Id), logs);
		b.Player.Changed += RaiseChanged;
		b.Ts.StatusChanged += RaiseChanged;
		lock (gate) bots.Add(b);
		await b.Ts.StartAsync(ct);
		return b;
	}

	/// <summary>A new bot on the same server as <paramref name="like"/>; it connects right away.</summary>
	public async Task<BotInstance> AddAsync(BotInstance? like)
	{
		var b = await StartBotAsync(config.AddBot(like?.Config), CancellationToken.None);
		log.LogInformation("Added bot {Id}", b.Id);
		RaiseChanged();
		return b;
	}

	/// <summary>Disconnects the bot and deletes its identity and history. The last bot can't be removed.</summary>
	public async Task<bool> RemoveAsync(string id)
	{
		var b = Get(id);
		if (b is null || !config.RemoveBot(id)) return false;
		lock (gate) bots.Remove(b);
		b.Player.Changed -= RaiseChanged;
		b.Ts.StatusChanged -= RaiseChanged;
		await b.Ts.StopAsync(CancellationToken.None);
		b.Dispose();
		library.Forget(id);
		try { Directory.Delete(paths.BotDir(id), recursive: true); }
		catch (Exception ex) { log.LogWarning("Could not delete {Dir}: {Error}", paths.BotDir(id), ex.Message); }
		log.LogInformation("Removed bot {Id}", id);
		RaiseChanged();
		return true;
	}

	public Task StopAsync(CancellationToken ct) => Task.WhenAll(All.Select(b => b.Ts.StopAsync(ct)));

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

	public void Dispose()
	{
		foreach (var b in All) b.Dispose();
	}
}
