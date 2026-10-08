using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Panda;

public sealed record DataPaths(string Data);

/// <summary>JSON API behind the web panel. Everything except login needs the session cookie.</summary>
public static class PanelApi
{
	public static void Map(WebApplication app)
	{
		var api = app.MapGroup("/api");
		var authed = api.MapGroup("").RequireAuthorization();

		api.MapPost("/login", Login);
		api.MapPost("/logout", async (HttpContext ctx) =>
		{
			await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Results.NoContent();
		});
		api.MapGet("/version", () => new { version = Version });

		authed.MapGet("/state", async (Player player, TsBot bot, Library library) => await Snapshot(player, bot, library));
		authed.MapGet("/events", Events);

		authed.MapGet("/search", async (string q, YouTube yt, HttpContext ctx) =>
		{
			try { return Results.Ok(await yt.SearchAsync(q, 10, PanelUser, ctx.RequestAborted)); }
			catch (YouTubeException ex) { return Problem(ex.Message); }
		});

		authed.MapPost("/play", async (PlayRequest req, Library library, Player player) =>
		{
			try
			{
				var track = await library.ResolveAsync(req.Query ?? "", req.RequestedBy is { Length: > 0 } by ? by : PanelUser);
				if (req.Now) player.PlayNow(track);
				else player.Enqueue(track);
				return Results.Ok(track);
			}
			catch (YouTubeException ex) { return Problem(ex.Message); }
			catch (QueueFullException) { return Problem("queue-full"); }
		});

		authed.MapPost("/control/{action}", (string action, Player player) => action switch
		{
			"pause" => Done(player.Pause()),
			"resume" => Done(player.Resume()),
			"toggle" => Done(player.IsPaused ? player.Resume() : player.Pause()),
			"skip" => Done(player.Skip()),
			"stop" => Done(Run(player.Stop)),
			"clear" => Done(Run(player.ClearQueue)),
			"shuffle" => Done(Run(player.Shuffle)),
			_ => Results.NotFound(),
		});

		authed.MapPost("/seek", (SeekRequest req, Player player) => Done(player.Seek((int)Math.Round(req.Seconds))));
		authed.MapPost("/volume", (VolumeRequest req, Player player) => Done(Run(() => player.SetVolume(req.Volume))));
		authed.MapPost("/loop", (LoopRequest req, Player player) => Done(Run(() => player.SetLoop(req.Mode))));
		authed.MapDelete("/queue/{index:int}", (int index, Player player) => Done(player.Remove(index) != null));
		authed.MapPost("/queue/move", (MoveRequest req, Player player) => Done(player.Move(req.From, req.To)));

		authed.MapGet("/settings", (PandaConfig config) => SettingsDto.From(config));
		authed.MapPut("/settings", (SettingsDto dto, PandaConfig config, TsBot bot, Library library) =>
		{
			var reconnect = dto.Apply(config);
			config.Save();
			library.SettingsChanged();
			if (reconnect) bot.Reconnect();
			return Results.Ok(SettingsDto.From(config));
		});

		authed.MapPost("/reconnect", (TsBot bot) => Done(Run(bot.Reconnect)));

		authed.MapGet("/library", (string? q, Library library) => library.GetInfo(q));
		authed.MapDelete("/library/{id}", (string id, Library library) => Done(library.Delete(id)));

		authed.MapPost("/cookies", async (HttpContext ctx, PandaConfig config, DataPaths paths) =>
		{
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
				await File.WriteAllTextAsync(path, text.ReplaceLineEndings("\n"));
				if (!OperatingSystem.IsWindows())
					File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
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

	static async Task<object> Snapshot(Player player, TsBot bot, Library library) => new
	{
		player = player.GetState(),
		bot = await bot.GetStatusAsync(),
		library = LibrarySummary(library),
		serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
	};

	static object LibrarySummary(Library library)
	{
		var info = library.GetInfo(max: 0);
		return new { info.Enabled, info.UsedBytes, info.LimitBytes, info.Count };
	}

	static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

	static async Task Events(HttpContext ctx, Player player, TsBot bot, Library library)
	{
		ctx.Response.Headers.ContentType = "text/event-stream";
		ctx.Response.Headers.CacheControl = "no-cache";
		ctx.Response.Headers["X-Accel-Buffering"] = "no";

		var signal = new SemaphoreSlim(1);
		void Poke() { if (signal.CurrentCount == 0) signal.Release(); }
		player.Changed += Poke;
		library.Changed += Poke;
		bot.StatusChanged += Poke;
		var ct = ctx.RequestAborted;
		try
		{
			while (!ct.IsCancellationRequested)
			{
				// Wait for a change, but send at least every 15 s so proxies keep the stream open.
				await signal.WaitAsync(TimeSpan.FromSeconds(15), ct);
				await Task.Delay(100, ct); // coalesce bursts of changes
				var json = JsonSerializer.Serialize(await Snapshot(player, bot, library), SseJson);
				await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
				await ctx.Response.Body.FlushAsync(ct);
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			player.Changed -= Poke;
			library.Changed -= Poke;
			bot.StatusChanged -= Poke;
		}
	}

	public sealed record LoginRequest(string? Password);
	public sealed record PlayRequest(string? Query, bool Now = false, string? RequestedBy = null);
	public sealed record SeekRequest(double Seconds);
	public sealed record VolumeRequest(int Volume);
	public sealed record LoopRequest(LoopMode Mode);
	public sealed record MoveRequest(int From, int To);
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

	public static SettingsDto From(PandaConfig c) => new()
	{
		ServerAddress = c.Server.Address,
		HasServerPassword = c.Server.Password.Length > 0,
		Channel = c.Server.Channel,
		HasChannelPassword = c.Server.ChannelPassword.Length > 0,
		Nickname = c.Bot.Nickname,
		CommandPrefix = c.Bot.CommandPrefix,
		Language = c.Bot.Language,
		Announce = c.Bot.Announce,
		ShowSongInDescription = c.Bot.ShowSongInDescription,
		AllowedUsers = c.Bot.AllowedUsers,
		AllowedServerGroups = c.Bot.AllowedServerGroups,
		MaxQueueLength = c.Bot.MaxQueueLength,
		MaxDurationMinutes = c.YouTube.MaxDurationMinutes,
		LibraryEnabled = c.Library.Enabled,
		LibraryMaxSizeMb = c.Library.MaxSizeMb,
		HasCookies = c.YouTube.CookiesFile.Length > 0 && File.Exists(c.YouTube.CookiesFile),
	};

	/// <summary>Copies the values into the config; returns true when the bot has to reconnect.</summary>
	public bool Apply(PandaConfig c)
	{
		var reconnect = false;
		void Set(ref bool flag, string oldValue, string newValue) { if (oldValue != newValue) flag = true; }

		var address = ServerAddress.Trim();
		if (address.Length > 0) { Set(ref reconnect, c.Server.Address, address); c.Server.Address = address; }
		if (ServerPassword != null) { Set(ref reconnect, c.Server.Password, ServerPassword); c.Server.Password = ServerPassword; }
		Set(ref reconnect, c.Server.Channel, Channel.Trim());
		c.Server.Channel = Channel.Trim();
		if (ChannelPassword != null) { Set(ref reconnect, c.Server.ChannelPassword, ChannelPassword); c.Server.ChannelPassword = ChannelPassword; }
		var nick = Nickname.Trim();
		if (nick.Length is >= 3 and <= 30) { Set(ref reconnect, c.Bot.Nickname, nick); c.Bot.Nickname = nick; }

		if (CommandPrefix.Trim().Length is > 0 and <= 3) c.Bot.CommandPrefix = CommandPrefix.Trim();
		if (Language is "en" or "tr") c.Bot.Language = Language;
		c.Bot.Announce = Announce;
		c.Bot.ShowSongInDescription = ShowSongInDescription;
		c.Bot.AllowedUsers = AllowedUsers.Select(u => u.Trim()).Where(u => u.Length > 0).Distinct().ToList();
		c.Bot.AllowedServerGroups = AllowedServerGroups.Distinct().ToList();
		if (MaxQueueLength is >= 1 and <= 1000) c.Bot.MaxQueueLength = MaxQueueLength;
		if (MaxDurationMinutes is >= 0 and <= 1440) c.YouTube.MaxDurationMinutes = MaxDurationMinutes;
		c.Library.Enabled = LibraryEnabled;
		if (LibraryMaxSizeMb is 0 or (>= 100 and <= 1_000_000)) c.Library.MaxSizeMb = LibraryMaxSizeMb;
		return reconnect;
	}
}
