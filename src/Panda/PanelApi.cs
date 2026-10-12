using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Panda;

public sealed record DataPaths(string Data)
{
	/// <summary>A bot's own folder: identity and play history.</summary>
	public string BotDir(string id) => Path.Combine(Data, "bots", id);
}

/// <summary>JSON API behind the web panel. Everything except login needs the session cookie.</summary>
public static class PanelApi
{
	public static void Map(WebApplication app)
	{
		// A bot named in the URL that doesn't exist (any more): 404 before the endpoint runs.
		app.Use(async (ctx, next) =>
		{
			if (ctx.GetRouteValue("bot") is string id && ctx.RequestServices.GetRequiredService<BotManager>().Get(id) is null)
			{
				ctx.Response.StatusCode = StatusCodes.Status404NotFound;
				await ctx.Response.WriteAsJsonAsync(new { error = "no-bot" });
				return;
			}
			await next(ctx);
		});

		var api = app.MapGroup("/api");
		var authed = api.MapGroup("").RequireAuthorization();

		api.MapPost("/login", Login);
		api.MapPost("/logout", async (HttpContext ctx) =>
		{
			await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Results.NoContent();
		});
		api.MapGet("/version", () => new { version = Version });

		// --- bots ---

		authed.MapGet("/bots", (BotManager bots) => bots.Summaries());
		authed.MapPost("/bots", async (AddBotRequest req, BotManager bots) =>
		{
			var b = await bots.AddAsync(bots.Get(req.Like ?? ""));
			return Results.Ok(b.Summary());
		});

		var bot = authed.MapGroup("/bots/{bot}");
		bot.MapDelete("", async (BotInstance b, BotManager bots) => Done(await bots.RemoveAsync(b.Id)));
		bot.MapGet("/state", async (BotInstance b, BotManager bots, Library library) => await Snapshot(b, bots, library));
		bot.MapGet("/events", Events);

		bot.MapPost("/play", async (PlayRequest req, BotInstance b, Library library) =>
		{
			try
			{
				var track = await library.ResolveAsync(req.Query ?? "", req.RequestedBy is { Length: > 0 } by ? by : PanelUser);
				if (req.Now) b.Player.PlayNow(track);
				else b.Player.Enqueue(track);
				return Results.Ok(track);
			}
			catch (YouTubeException ex) { return Problem(ex.Message); }
			catch (QueueFullException) { return Problem("queue-full"); }
		});

		bot.MapPost("/control/{action}", (string action, BotInstance b) => action switch
		{
			"pause" => Done(b.Player.Pause()),
			"resume" => Done(b.Player.Resume()),
			"toggle" => Done(b.Player.IsPaused ? b.Player.Resume() : b.Player.Pause()),
			"skip" => Done(b.Player.Skip()),
			"stop" => Done(Run(b.Player.Stop)),
			"clear" => Done(Run(b.Player.ClearQueue)),
			"shuffle" => Done(Run(b.Player.Shuffle)),
			_ => Results.NotFound(),
		});

		bot.MapPost("/seek", (SeekRequest req, BotInstance b) => Done(b.Player.Seek((int)Math.Round(req.Seconds))));
		bot.MapPost("/volume", (VolumeRequest req, BotInstance b) => Done(Run(() => b.Player.SetVolume(req.Volume))));
		bot.MapPost("/loop", (LoopRequest req, BotInstance b) => Done(Run(() => b.Player.SetLoop(req.Mode))));
		// The song id guards against the queue having moved on since the panel showed it.
		bot.MapDelete("/queue/{index:int}", (int index, string? id, BotInstance b) => Done(b.Player.Remove(index, id) != null));
		bot.MapPost("/queue/move", (MoveRequest req, BotInstance b) => Done(b.Player.Move(req.From, req.To, req.Id)));

		bot.MapGet("/settings", (BotInstance b, PandaConfig config) => SettingsDto.From(config, b.Config));
		bot.MapPut("/settings", (SettingsDto dto, BotInstance b, PandaConfig config, Library library) =>
		{
			var reconnect = dto.Apply(config, b.Config);
			config.Save();
			library.SettingsChanged();
			if (reconnect) b.Ts.Reconnect();
			return Results.Ok(SettingsDto.From(config, b.Config));
		});
		bot.MapPost("/reconnect", (BotInstance b) => Done(Run(b.Ts.Reconnect)));

		// --- shared by all bots ---

		authed.MapGet("/search", async (string q, YouTube yt, HttpContext ctx) =>
		{
			try { return Results.Ok(await yt.SearchAsync(q, 10, PanelUser, ctx.RequestAborted)); }
			catch (YouTubeException ex) { return Problem(ex.Message); }
		});

		authed.MapGet("/library", (string? q, Library library) => library.GetInfo(q));
		authed.MapDelete("/library/{id}", (string id, Library library) => Done(library.Delete(id)));

		authed.MapPost("/cookies", async (HttpContext ctx, PandaConfig config, DataPaths paths) =>
		{
			if (ctx.Request.ContentLength > 2_000_000) return Problem("too-large");
			using var reader = new StreamReader(ctx.Request.Body);
			var text = await reader.ReadToEndAsync();
			if (text.Length > 2_000_000) return Problem("too-large");
			var path = Path.Combine(paths.Data, "cookies.txt");
			if (text.Trim().Length == 0)
			{
				File.Delete(path);
				config.YouTube.CookiesFile = "";
			}
			else
			{
				if (!text.Contains("youtube.com")) return Problem("cookies-invalid");
				SafeFile.Write(path, text.ReplaceLineEndings("\n"), ownerOnly: true);
				config.YouTube.CookiesFile = path;
			}
			config.Save();
			return Results.Ok(new { hasCookies = config.YouTube.CookiesFile.Length > 0 });
		});

		authed.MapPost("/password", (PasswordRequest req, PandaConfig config) =>
		{
			if (!PasswordHasher.Verify(req.Current ?? "", config.Panel.PasswordHash))
				return Problem("wrong-password");
			if ((req.New ?? "").Length < 6)
				return Problem("password-too-short");
			config.Panel.PasswordHash = PasswordHasher.Hash(req.New!);
			config.Save();
			return Results.NoContent();
		});
	}

	public static string Version =>
		typeof(PanelApi).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

	const string PanelUser = "🌐 Panel";

	static IResult Done(bool ok) => ok ? Results.NoContent() : Results.Conflict();
	static bool Run(Action a) { a(); return true; }
	static IResult Problem(string code) => Results.BadRequest(new { error = code });

	// --- login with a small brute-force guard ---

	static readonly ConcurrentDictionary<string, (int Fails, DateTime Until)> Failures = new();

	static async Task<IResult> Login(LoginRequest req, HttpContext ctx, PandaConfig config)
	{
		var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
		if (Failures.TryGetValue(ip, out var f) && f.Fails >= 5 && f.Until > DateTime.UtcNow)
			return Results.Json(new { error = "too-many-attempts" }, statusCode: 429);

		var ok = PasswordHasher.Verify(req.Password ?? "", config.Panel.PasswordHash);
		if (!ok)
		{
			// The password may have just been reset with `panda set-password`.
			config.ReloadPassword();
			ok = PasswordHasher.Verify(req.Password ?? "", config.Panel.PasswordHash);
		}
		if (!ok)
		{
			if (Failures.Count > 1000) // forget old entries so a flood of addresses can't grow this forever
				foreach (var (key, old) in Failures)
					if (old.Until < DateTime.UtcNow) Failures.TryRemove(key, out _);
			Failures.AddOrUpdate(ip, (1, DateTime.UtcNow.AddMinutes(10)), (_, old) => (old.Fails + 1, DateTime.UtcNow.AddMinutes(10)));
			await Task.Delay(700);
			return Results.Json(new { error = "wrong-password" }, statusCode: 401);
		}
		Failures.TryRemove(ip, out _);

		var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], CookieAuthenticationDefaults.AuthenticationScheme));
		await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
			new AuthenticationProperties { IsPersistent = true });
		return Results.NoContent();
	}

	// --- live updates (Server-Sent Events) ---

	/// <summary>Everything about one bot, plus a short line about every bot for the bot picker.</summary>
	static async Task<object> Snapshot(BotInstance b, BotManager bots, Library library) => new
	{
		player = b.Player.GetState(),
		bot = await b.Ts.GetStatusAsync(),
		bots = bots.Summaries(),
		library = library.GetSummary(),
		serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
	};

	static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

	static async Task Events(HttpContext ctx, BotInstance b, BotManager bots, Library library)
	{
		ctx.Response.Headers.ContentType = "text/event-stream";
		ctx.Response.Headers.CacheControl = "no-cache";
		ctx.Response.Headers["X-Accel-Buffering"] = "no";

		var signal = new SemaphoreSlim(1);
		void Poke() { if (signal.CurrentCount == 0) signal.Release(); }
		bots.Changed += Poke; // covers this bot's player and connection too
		library.Changed += Poke;
		var ct = ctx.RequestAborted;
		try
		{
			while (!ct.IsCancellationRequested)
			{
				// Wait for a change, but send at least every 15 s so proxies keep the stream open.
				await signal.WaitAsync(TimeSpan.FromSeconds(15), ct);
				await Task.Delay(100, ct); // coalesce bursts of changes
				if (bots.Get(b.Id) != b) break; // removed: the panel then picks another bot
				var json = JsonSerializer.Serialize(await Snapshot(b, bots, library), SseJson);
				await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
				await ctx.Response.Body.FlushAsync(ct);
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			bots.Changed -= Poke;
			library.Changed -= Poke;
		}
	}

	public sealed record LoginRequest(string? Password);
	/// <param name="Like">Copy the server address from this bot.</param>
	public sealed record AddBotRequest(string? Like);
	public sealed record PlayRequest(string? Query, bool Now = false, string? RequestedBy = null);
	public sealed record SeekRequest(double Seconds);
	public sealed record VolumeRequest(int Volume);
	public sealed record LoopRequest(LoopMode Mode);
	public sealed record MoveRequest(int From, int To, string? Id = null);
	public sealed record PasswordRequest(string? Current, string? New);
}

/// <summary>Editable settings. Passwords are write-only: empty/null keeps the stored value.</summary>
public sealed class SettingsDto
{
	public string ServerAddress { get; set; } = "";
	public string? ServerPassword { get; set; }
	public bool HasServerPassword { get; set; }
	public string Channel { get; set; } = "";
	public string? ChannelPassword { get; set; }
	public bool HasChannelPassword { get; set; }
	public string Nickname { get; set; } = "";
	public string CommandPrefix { get; set; } = "!";
	public string Language { get; set; } = "en";
	public bool Announce { get; set; }
	public bool ShowSongInDescription { get; set; }
	public List<string> AllowedUsers { get; set; } = [];
	public List<ulong> AllowedServerGroups { get; set; } = [];
	public int MaxQueueLength { get; set; }
	public int MaxDurationMinutes { get; set; }
	public bool LibraryEnabled { get; set; }
	public int LibraryMaxSizeMb { get; set; }
	public bool HasCookies { get; set; }

	/// <summary>The bot's own settings plus the shared ones (library, YouTube).</summary>
	public static SettingsDto From(PandaConfig c, BotConfig b) => new()
	{
		ServerAddress = b.Server.Address,
		HasServerPassword = b.Server.Password.Length > 0,
		Channel = b.Server.Channel,
		HasChannelPassword = b.Server.ChannelPassword.Length > 0,
		Nickname = b.Bot.Nickname,
		CommandPrefix = b.Bot.CommandPrefix,
		Language = b.Bot.Language,
		Announce = b.Bot.Announce,
		ShowSongInDescription = b.Bot.ShowSongInDescription,
		AllowedUsers = b.Bot.AllowedUsers,
		AllowedServerGroups = b.Bot.AllowedServerGroups,
		MaxQueueLength = b.Bot.MaxQueueLength,
		MaxDurationMinutes = c.YouTube.MaxDurationMinutes,
		LibraryEnabled = c.Library.Enabled,
		LibraryMaxSizeMb = c.Library.MaxSizeMb,
		HasCookies = c.YouTube.CookiesFile.Length > 0 && File.Exists(c.YouTube.CookiesFile),
	};

	/// <summary>Copies the values into the config; returns true when the bot has to reconnect.</summary>
	public bool Apply(PandaConfig c, BotConfig b)
	{
		var reconnect = false;
		void Set(ref bool flag, string oldValue, string newValue) { if (oldValue != newValue) flag = true; }

		var address = ServerAddress.Trim();
		if (address.Length > 0) { Set(ref reconnect, b.Server.Address, address); b.Server.Address = address; }
		if (ServerPassword != null) { Set(ref reconnect, b.Server.Password, ServerPassword); b.Server.Password = ServerPassword; }
		Set(ref reconnect, b.Server.Channel, Channel.Trim());
		b.Server.Channel = Channel.Trim();
		if (ChannelPassword != null) { Set(ref reconnect, b.Server.ChannelPassword, ChannelPassword); b.Server.ChannelPassword = ChannelPassword; }
		var nick = Nickname.Trim();
		if (nick.Length is >= 3 and <= 30) { Set(ref reconnect, b.Bot.Nickname, nick); b.Bot.Nickname = nick; }

		if (CommandPrefix.Trim().Length is > 0 and <= 3) b.Bot.CommandPrefix = CommandPrefix.Trim();
		if (Language is "en" or "tr") b.Bot.Language = Language;
		b.Bot.Announce = Announce;
		b.Bot.ShowSongInDescription = ShowSongInDescription;
		b.Bot.AllowedUsers = AllowedUsers.Select(u => u.Trim()).Where(u => u.Length > 0).Distinct().ToList();
		b.Bot.AllowedServerGroups = AllowedServerGroups.Distinct().ToList();
		if (MaxQueueLength is >= 1 and <= 1000) b.Bot.MaxQueueLength = MaxQueueLength;
		if (MaxDurationMinutes is >= 0 and <= 1440) c.YouTube.MaxDurationMinutes = MaxDurationMinutes;
		c.Library.Enabled = LibraryEnabled;
		if (LibraryMaxSizeMb is 0 or (>= 100 and <= 1_000_000)) c.Library.MaxSizeMb = LibraryMaxSizeMb;
		return reconnect;
	}
}