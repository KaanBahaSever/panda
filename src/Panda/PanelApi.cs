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
		// Any logged-in user ("dj" too): watch, search, add songs, change their own password.
		var authed = api.MapGroup("").RequireAuthorization();
		// Everything else is for admins.
		var admin = authed.MapGroup("").RequireAuthorization(Roles.Admin);

		api.MapPost("/login", Login);
		api.MapPost("/logout", async (HttpContext ctx) =>
		{
			await ctx.SignOutAsync(Scheme);
			return Results.NoContent();
		});
		api.MapGet("/version", () => new { version = Version });
		authed.MapGet("/me", (HttpContext ctx) => new { name = ctx.User.Identity?.Name, role = ctx.User.FindFirst(ClaimTypes.Role)?.Value });

		// --- bots ---

		authed.MapGet("/bots", (BotManager bots) => bots.Summaries());
		admin.MapPost("/bots", async (AddBotRequest req, BotManager bots) =>
		{
			var b = await bots.AddAsync(bots.Get(req.Like ?? ""));
			return Results.Ok(b.Summary());
		});

		var bot = authed.MapGroup("/bots/{bot}");
		var botAdmin = bot.MapGroup("").RequireAuthorization(Roles.Admin);
		botAdmin.MapDelete("", async (BotInstance b, BotManager bots) => Done(await bots.RemoveAsync(b.Id)));
		bot.MapGet("/state", async (BotInstance b, BotManager bots, Library library, Playlists lists) => await Snapshot(b, bots, library, lists));
		bot.MapGet("/events", Events);

		bot.MapPost("/play", async (PlayRequest req, BotInstance b, Sources sources, HttpContext ctx) =>
		{
			var isAdmin = ctx.User.IsInRole(Roles.Admin);
			if (req.Now && !isAdmin) return Results.StatusCode(StatusCodes.Status403Forbidden); // it skips the current song
			try
			{
				var by = isAdmin && req.RequestedBy is { Length: > 0 } r ? r : "🌐 " + ctx.User.Identity?.Name;
				var requester = "panel:" + ctx.User.Identity?.Name;
				var found = await sources.ResolveAsync(req.Query ?? "", by);
				var track = found.Track with { RequesterId = requester };
				if (req.Now) b.Player.PlayNow(track, exempt: true);   // admins only
				else b.Player.Enqueue(track, exempt: isAdmin);
				// An album or a playlist of a music service: the rest comes in the background.
				if (found.More is { Count: > 0 } more) AddLater(b, sources, more, by, requester, isAdmin);
				return Added(ctx, track, found.More?.Count ?? 0, found.ListName);
			}
			catch (YouTubeException ex) { return Problem(ex.Message); }
			catch (QueueFullException) { return Problem("queue-full"); }
			catch (UserLimitException) { return Problem("user-limit"); }
		});
		bot.MapPost("/fm", async (FmRequest req, BotInstance b, RadioBrowser radios, RadioFavorites favorites, PandaConfig config, HttpContext ctx) =>
		{
			if (!config.Sources.Radio) return Problem("source-off");
			var isAdmin = ctx.User.IsInRole(Roles.Admin);
			if (req.Now && !isAdmin) return Results.StatusCode(StatusCodes.Status403Forbidden);
			Station? station;
			try { station = await FindStationAsync(req.Uuid ?? "", radios, favorites, ctx.RequestAborted); }
			catch (RadioException) { return Problem("radio-list-down"); }
			if (station is null) return Results.NotFound();
			radios.Played(station);
			var track = station.ToTrack("🌐 " + ctx.User.Identity?.Name) with { RequesterId = "panel:" + ctx.User.Identity?.Name };
			try
			{
				if (req.Now) b.Player.PlayNow(track, exempt: true);
				else b.Player.Enqueue(track, exempt: isAdmin);
				return Results.Ok(track);
			}
			catch (QueueFullException) { return Problem("queue-full"); }
			catch (UserLimitException) { return Problem("user-limit"); }
		});

		botAdmin.MapPost("/control/{action}", (string action, BotInstance b) => action switch
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

		botAdmin.MapPost("/seek", (SeekRequest req, BotInstance b) => Done(b.Player.Seek((int)Math.Round(req.Seconds))));
		botAdmin.MapPost("/volume", (VolumeRequest req, BotInstance b) => Done(Run(() => b.Player.SetVolume(req.Volume))));
		botAdmin.MapPost("/loop", (LoopRequest req, BotInstance b) => Done(Run(() => b.Player.SetLoop(req.Mode))));
		botAdmin.MapPost("/autoplay", (AutoplayRequest req, BotInstance b) => Done(Run(() => b.Player.SetAutoplay(req.On))));
		// Equalizer, effects, speed, crossfade: what's left out stays as it is.
		botAdmin.MapPut("/sound", (SoundRequest req, BotInstance b) =>
		{
			if (req.Equalizer != null && !Sound.Equalizers.Contains(req.Equalizer)) return Problem("bad-equalizer");
			if (req.Effect != null && !Sound.Effects.Contains(req.Effect)) return Problem("bad-effect");
			if (req.Speed is < 0.5 or > 2 || req.Crossfade is < 0 or > Sound.MaxCrossfade || req.Bands is { Length: not 10 }) return Problem("bad-sound");
			var now = b.Player.UpdateSound(s => s with
			{
				Equalizer = req.Equalizer ?? s.Equalizer,
				Bands = req.Bands is { } bands ? Sound.Normalize(bands) : s.Bands,
				Effect = req.Effect ?? s.Effect,
				Speed = req.Speed is { } sp ? Math.Round(sp, 2) : s.Speed,
				EightD = req.EightD ?? s.EightD,
				Crossfade = req.Crossfade ?? s.Crossfade,
			});
			return Results.Ok(now);
		});
		// The name up to 0.5.
		botAdmin.MapPost("/radio", (AutoplayRequest req, BotInstance b) => Done(Run(() => b.Player.SetAutoplay(req.On))));
		// The song id guards against the queue having moved on since the panel showed it.
		botAdmin.MapDelete("/queue/{index:int}", (int index, string? id, BotInstance b) => Done(b.Player.Remove(index, id) != null));
		botAdmin.MapPost("/queue/move", (MoveRequest req, BotInstance b) => Done(b.Player.Move(req.From, req.To, req.Id)));

		botAdmin.MapGet("/settings", (BotInstance b, PandaConfig config) => SettingsDto.From(config, b.Config));
		botAdmin.MapPut("/settings", (SettingsDto dto, BotInstance b, PandaConfig config, Library library) =>
		{
			var reconnect = dto.Apply(config, b.Config);
			config.Save();
			library.SettingsChanged();
			if (reconnect) b.Ts.Reconnect();
			else b.Ts.SettingsChanged();
			return Results.Ok(SettingsDto.From(config, b.Config));
		});
		botAdmin.MapPost("/reconnect", (BotInstance b) => Done(Run(b.Ts.Reconnect)));
		botAdmin.MapGet("/ts/people", async (BotInstance b) => await b.Ts.GetPeopleAsync());
		botAdmin.MapGet("/ts/permissions", async (BotInstance b) => await b.Ts.GetPermissionsAsync());

		// --- shared by all bots ---

		authed.MapGet("/search", async (string q, Sources sources, HttpContext ctx) =>
		{
			try { return Results.Ok(await sources.SearchAnyAsync(q, 10, "🌐 " + ctx.User.Identity?.Name, ctx.RequestAborted)); }
			catch (YouTubeException ex) { return Problem(ex.Message); }
		});

		// --- internet radio: Radio Browser's list and the favorite stations (shared by all bots) ---

		authed.MapGet("/radio/search", async (string? name, string? country, string? tag, RadioBrowser radios, HttpContext ctx) =>
		{
			if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(tag)) return Results.Ok(Array.Empty<Station>());
			try { return Results.Ok(await radios.SearchAsync(Cut(name, 100), Cut(country, 2), Cut(tag, 40), 60, ctx.RequestAborted)); }
			catch (RadioException) { return Problem("radio-list-down"); }
		});
		authed.MapGet("/radio/countries", async (RadioBrowser radios, HttpContext ctx) =>
		{
			try { return Results.Ok(await radios.CountriesAsync(ctx.RequestAborted)); }
			catch (RadioException) { return Problem("radio-list-down"); }
		});
		authed.MapGet("/radio/tags", async (RadioBrowser radios, HttpContext ctx) =>
		{
			try { return Results.Ok(await radios.TagsAsync(ctx.RequestAborted)); }
			catch (RadioException) { return Problem("radio-list-down"); }
		});
		authed.MapGet("/radio/favorites", (RadioFavorites favorites) => favorites.All());
		// A station from Radio Browser (by its id), or what a bot is playing.
		admin.MapPost("/radio/favorites", async (FavoriteRequest req, RadioBrowser radios, RadioFavorites favorites, BotManager bots, HttpContext ctx) =>
		{
			Station? station = null;
			if (req.FromBot != null && bots.Get(req.FromBot)?.Player.Current is { } now) station = Stations.FromTrack(now);
			else if (req.Uuid is { Length: > 0 } uuid)
			{
				try { station = await FindStationAsync(uuid, radios, favorites, ctx.RequestAborted); }
				catch (RadioException) { return Problem("radio-list-down"); }
			}
			if (station is null) return Results.NotFound();
			return favorites.Add(station) ? Results.Ok(station) : Problem("fm-full");
		});
		admin.MapDelete("/radio/favorites/{uuid}", (string uuid, RadioFavorites favorites) => Done(favorites.Remove(uuid)));

		authed.MapGet("/library", (string? q, Library library) => library.GetInfo(q));
		// bot: an id or "all"; days: 0 = everything; tz: the browser's getTimezoneOffset().
		authed.MapGet("/stats", (string? bot, int? days, int? tz, Stats stats) => stats.Get(bot, days ?? 30, tz ?? 0));
		admin.MapDelete("/library/{id}", (string id, Library library) => Done(library.Delete(id)));

		admin.MapPost("/cookies", async (HttpContext ctx, PandaConfig config, DataPaths paths) =>
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

		// --- playlists (shared by all bots; everyone can play them, admins edit them) ---

		authed.MapGet("/playlists/{name}", (string name, Playlists lists) => lists.Find(name) is { } p ? Results.Ok(p) : Results.NotFound());
		// Creates a list or adds songs to it: the given songs, or what a bot is playing plus its queue.
		admin.MapPost("/playlists", (PlaylistRequest req, Playlists lists, BotManager bots) =>
		{
			var name = (req.Name ?? "").Trim();
			if (!Playlists.IsValidName(name)) return Problem("bad-playlist-name");
			var songs = (req.Songs ?? []).Where(IsValidSong).ToList();
			if (req.FromBot != null && bots.Get(req.FromBot) is { } b)
			{
				var s = b.Player.GetState();
				songs.AddRange(new[] { s.Current?.Track }.Concat(s.Queue).OfType<Track>().Where(t => !t.IsLive).Select(PlaylistSong.From));
			}
			var list = lists.Save(name, songs, out var left);
			return Results.Ok(new { list.Name, list.Songs, list.Updated, Left = left });
		});
		admin.MapPut("/playlists/{name}", (string name, PlaylistRequest req, Playlists lists) =>
			!Playlists.IsValidName(req.Name ?? "") ? Problem("bad-playlist-name")
			: lists.Rename(name, req.Name!) ? Results.NoContent() : Problem("playlist-exists"));
		admin.MapDelete("/playlists/{name}", (string name, Playlists lists) => Done(lists.Delete(name)));
		admin.MapDelete("/playlists/{name}/songs/{index:int}", (string name, int index, string id, Playlists lists) => Done(lists.RemoveSong(name, index, id)));

		bot.MapPost("/playlist", (PlayPlaylistRequest req, BotInstance b, Playlists lists, HttpContext ctx) =>
		{
			if (lists.Find(req.Name ?? "") is not { } p) return Results.NotFound();
			var songs = p.Songs.Select(s => s.ToTrack("🌐 " + ctx.User.Identity?.Name) with { RequesterId = "panel:" + ctx.User.Identity?.Name });
			if (req.Shuffle) songs = songs.OrderBy(_ => Random.Shared.Next());
			var added = b.Player.EnqueueMany(songs.ToList(), exempt: ctx.User.IsInRole(Roles.Admin), out var limited);
			return added == 0 && p.Songs.Count > 0 ? Problem(limited ? "user-limit" : "queue-full") : Results.Ok(new { added, limited });
		});

		// --- health and logs ---

		admin.MapGet("/system", async (bool? fresh, SystemInfo system) => await system.GetAsync(fresh == true));
		admin.MapGet("/system/logs", LogEvents);
		admin.MapGet("/system/logs.txt", (LogBuffer logs) =>
		{
			var text = string.Join("\n", logs.Since(0).Select(e =>
				$"{e.Time.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} {e.Level,-5} [{e.Source}] {e.Message}"));
			return Results.File(System.Text.Encoding.UTF8.GetBytes(text + "\n"), "text/plain; charset=utf-8",
				$"panda-log-{DateTime.Now:yyyyMMdd-HHmm}.txt");
		});

		// --- updates ---

		admin.MapGet("/update", async (bool? fresh, Updates updates) => await updates.CheckAsync(fresh == true));
		admin.MapPost("/update", async (Updates updates) =>
			await updates.StartUpgradeAsync() ? Results.Accepted() : Problem("cannot-update"));
		admin.MapGet("/update/progress", async (Updates updates) => new { version = Version, state = await updates.UpgradeStateAsync() });
		admin.MapPost("/update/yt-dlp", async (Updates updates) =>
		{
			try
			{
				return Results.Ok(await updates.UpdateYtDlpAsync());
			}
			catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException
				or UnauthorizedAccessException or OperationCanceledException)
			{
				return Problem("ytdlp-update-failed");
			}
		});

		// --- panel users ---

		authed.MapPost("/password", async (PasswordRequest req, PandaConfig config, HttpContext ctx) =>
		{
			var name = ctx.User.Identity?.Name ?? "";
			if (config.CheckLogin(name, req.Current ?? "") is null)
				return Problem("wrong-password");
			if ((req.New ?? "").Length < 6)
				return Problem("password-too-short");
			// A new password logs out the user's other sessions; this one gets a fresh cookie.
			if (config.SetPassword(name, req.New!) is not { } user) return Results.Unauthorized(); // removed meanwhile
			await SignIn(ctx, user);
			return Results.NoContent();
		});

		admin.MapGet("/users", (PandaConfig config) => config.Panel.Users.Select(u => new { u.Name, u.Role }));

		admin.MapGet("/apikeys", (PandaConfig config) => config.Panel.ApiKeys.Select(k => new { k.Id, k.Name, k.Role, k.Created, k.LastUsed }));
		admin.MapPost("/apikeys", (UserRequest req, PandaConfig config) =>
		{
			var name = (req.Name ?? "").Trim();
			if (name.Length is < 1 or > 40 || name.Any(ch => char.IsControl(ch) || ch is '[' or ']')) return Problem("bad-key-name");
			if (config.Panel.ApiKeys.Any(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return Problem("key-exists");
			if (!Roles.IsValid(req.Role ?? "")) return Problem("bad-role");
			if (config.Panel.ApiKeys.Count >= 50) return Problem("too-many-keys");
			var (key, secret) = config.AddApiKey(name, req.Role!);
			return Results.Ok(new { key.Id, key.Name, key.Role, key.Created, Secret = secret });
		});
		admin.MapDelete("/apikeys/{id}", (string id, PandaConfig config) => config.RemoveApiKey(id) ? Results.NoContent() : Results.NotFound());
		admin.MapPost("/users", (UserRequest req, PandaConfig config) =>
		{
			var name = (req.Name ?? "").Trim();
			if (!PanelUser.IsValidName(name)) return Problem("bad-name");
			if ((req.Password ?? "").Length < 6) return Problem("password-too-short");
			if (!Roles.IsValid(req.Role ?? "")) return Problem("bad-role");
			return config.AddUser(name, req.Password!, req.Role!) ? Results.NoContent() : Problem("user-exists");
		});
		// Changes the role and/or sets a new password.
		admin.MapPut("/users/{name}", (string name, UserRequest req, PandaConfig config) =>
		{
			if (config.FindUser(name) is null) return Results.NotFound();
			if (req.Password is { Length: > 0 and < 6 }) return Problem("password-too-short");
			if (req.Role != null && (!Roles.IsValid(req.Role) || !config.SetRole(name, req.Role))) return Problem("last-admin");
			if (req.Password is { Length: >= 6 } && config.SetPassword(name, req.Password) is null) return Results.NotFound();
			return Results.NoContent();
		});
		admin.MapDelete("/users/{name}", (string name, PandaConfig config, HttpContext ctx) =>
		{
			if (name.Equals(ctx.User.Identity?.Name, StringComparison.OrdinalIgnoreCase)) return Problem("self");
			if (config.FindUser(name) is null) return Results.NotFound();
			return config.RemoveUser(name) ? Results.NoContent() : Problem("last-admin");
		});
	}

	public static string Version =>
		typeof(PanelApi).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

	const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

	static IResult Done(bool ok) => ok ? Results.NoContent() : Results.Conflict();
	static bool Run(Action a) { a(); return true; }
	static IResult Problem(string code) => Results.BadRequest(new { error = code });

	// The song as before, plus for a list how many more are on the way ("more") and its name ("list").
	static IResult Added(HttpContext ctx, Track track, int more, string? list)
	{
		var opts = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
		var json = JsonSerializer.SerializeToNode(track, opts)!.AsObject();
		if (more > 0)
		{
			json["more"] = more;
			json["list"] = list;
		}
		return Results.Json(json, opts);
	}

	static string? Cut(string? s, int max) => s?.Trim() is { } t && t.Length > max ? t[..max] : s?.Trim();

	// Stations are looked up by id, never taken from the request: where Panda connects stays Radio Browser's
	// (or an admin's favorite).
	static async Task<Station?> FindStationAsync(string uuid, RadioBrowser radios, RadioFavorites favorites, CancellationToken ct)
	{
		if (uuid.Length is 0 or > 64) return null;
		if (favorites.All().FirstOrDefault(s => s.Uuid == uuid) is { } saved) return saved;
		return uuid.StartsWith("link-") ? null : await radios.FindAsync(uuid, ct);
	}

	// The rest of a music service's album or playlist, added as it's found (until the queue is full or cleared).
	static void AddLater(BotInstance b, Sources sources, IReadOnlyList<string> songs, string by, string requester, bool exempt)
	{
		var epoch = b.Player.Epoch;
		_ = Task.Run(() => sources.AddAllAsync(songs, by, t =>
		{
			if (b.Player.Disposed || b.Player.Epoch != epoch) return false;
			try { b.Player.Enqueue(t with { RequesterId = requester }, exempt); return true; }
			catch (Exception ex) when (ex is QueueFullException or UserLimitException) { return false; }
		}));
	}

	// --- login with a small brute-force guard ---

	static readonly ConcurrentDictionary<string, (int Fails, DateTime Until)> Failures = new();

	static async Task<IResult> Login(LoginRequest req, HttpContext ctx, PandaConfig config)
	{
		var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
		if (Failures.TryGetValue(ip, out var f) && f.Fails >= 5 && f.Until > DateTime.UtcNow)
			return Results.Json(new { error = "too-many-attempts" }, statusCode: 429);

		var name = (req.Name ?? "").Trim();
		var user = config.CheckLogin(name.Length > 0 ? name : "admin", req.Password ?? "");
		if (user is null)
		{
			if (Failures.Count > 1000) // forget old entries so a flood of addresses can't grow this forever
				foreach (var (key, old) in Failures)
					if (old.Until < DateTime.UtcNow) Failures.TryRemove(key, out _);
			Failures.AddOrUpdate(ip, (1, DateTime.UtcNow.AddMinutes(10)), (_, old) => (old.Fails + 1, DateTime.UtcNow.AddMinutes(10)));
			await Task.Delay(700);
			return Results.Json(new { error = "wrong-password" }, statusCode: 401);
		}
		Failures.TryRemove(ip, out _);
		await SignIn(ctx, user);
		return Results.NoContent();
	}

	static ClaimsPrincipal Principal(PanelUser u) => new(new ClaimsIdentity(
		[new Claim(ClaimTypes.Name, u.Name), new Claim(ClaimTypes.Role, u.Role), new Claim("stamp", u.Stamp)], Scheme));

	static Task SignIn(HttpContext ctx, PanelUser u) =>
		ctx.SignInAsync(Scheme, Principal(u), new AuthenticationProperties { IsPersistent = true });

	/// <summary>
	/// Runs for every request with a session cookie: a removed user or a changed password ends the
	/// session, and the role is taken from the settings so a change applies right away.
	/// </summary>
	public static Task ValidateSession(CookieValidatePrincipalContext ctx)
	{
		var config = ctx.HttpContext.RequestServices.GetRequiredService<PandaConfig>();
		var user = ctx.Principal?.Identity?.Name is { } name ? config.FindUser(name) : null;
		if (user is null || user.Stamp != ctx.Principal!.FindFirst("stamp")?.Value)
		{
			ctx.RejectPrincipal();
			return ctx.HttpContext.SignOutAsync(Scheme);
		}
		if (ctx.Principal.FindFirst(ClaimTypes.Role)?.Value != user.Role || ctx.Principal.Identity?.Name != user.Name)
		{
			ctx.ReplacePrincipal(Principal(user));
			ctx.ShouldRenew = true;
		}
		return Task.CompletedTask;
	}

	// --- live updates (Server-Sent Events) ---

	/// <summary>Everything about one bot, plus a short line about every bot for the bot picker.</summary>
	static async Task<object> Snapshot(BotInstance b, BotManager bots, Library library, Playlists lists) => new
	{
		player = b.Player.GetState(),
		bot = await b.Ts.GetStatusAsync(),
		bots = bots.Summaries(),
		library = library.GetSummary(),
		playlists = lists.All().Select(p => new { p.Name, Count = p.Songs.Count, Duration = p.Songs.Sum(s => s.Duration) }),
		serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
	};

	static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// Live streams were let in once, when they opened. This asks again while they run, so a removed
	/// user, a changed password or an admin made "dj" doesn't keep receiving them.
	/// </summary>
	static bool StillAllowed(HttpContext ctx, PandaConfig config, bool admin) =>
		CurrentRole(ctx, config) is { } role && (!admin || role == Roles.Admin);

	/// <summary>The role the request's user or API key has now; null when it was removed or logged out.</summary>
	static string? CurrentRole(HttpContext ctx, PandaConfig config)
	{
		if (ctx.User.FindFirst("apikey")?.Value is { } id) return config.FindApiKeyById(id)?.Role;
		var user = ctx.User.Identity?.Name is { } name ? config.FindUser(name) : null;
		return user != null && user.Stamp == ctx.User.FindFirst("stamp")?.Value ? user.Role : null;
	}

	static async Task Events(HttpContext ctx, BotInstance b, BotManager bots, Library library, Playlists lists, PandaConfig config,
		IHostApplicationLifetime life)
	{
		ctx.Response.Headers.ContentType = "text/event-stream";
		ctx.Response.Headers.CacheControl = "no-cache";
		ctx.Response.Headers["X-Accel-Buffering"] = "no";

		var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
		var signal = new SemaphoreSlim(1);
		void Poke() { if (signal.CurrentCount == 0) signal.Release(); }
		bots.Changed += Poke; // covers this bot's player and connection too
		library.Changed += Poke;
		lists.Changed += Poke;
		config.UsersChanged += Poke;
		// Ends when Panda stops, too: an open stream otherwise holds the shutdown for 30 s.
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, life.ApplicationStopping);
		var ct = stop.Token;
		try
		{
			while (!ct.IsCancellationRequested)
			{
				// Wait for a change, but send at least every 15 s so proxies keep the stream open.
				await signal.WaitAsync(TimeSpan.FromSeconds(15), ct);
				await Task.Delay(100, ct); // coalesce bursts of changes
				if (bots.Get(b.Id) != b) break; // removed: the panel then picks another bot
				if (!StillAllowed(ctx, config, admin: false)) break;
				// Made admin or "dj" meanwhile: the panel reconnects and shows the controls that go with it.
				if (CurrentRole(ctx, config) != role) break;
				var json = JsonSerializer.Serialize(await Snapshot(b, bots, library, lists), SseJson);
				await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
				await ctx.Response.Body.FlushAsync(ct);
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			bots.Changed -= Poke;
			library.Changed -= Poke;
			lists.Changed -= Poke;
			config.UsersChanged -= Poke;
		}
	}

	/// <param name="Name">Empty = "admin" (logins from before there were user names).</param>
	/// <summary>Log lines as they come: first everything after <paramref name="after"/>, then each new batch.</summary>
	static async Task LogEvents(HttpContext ctx, LogBuffer logs, PandaConfig config, IHostApplicationLifetime life, long after = 0)
	{
		ctx.Response.Headers.ContentType = "text/event-stream";
		ctx.Response.Headers.CacheControl = "no-cache";
		ctx.Response.Headers["X-Accel-Buffering"] = "no";

		var signal = new SemaphoreSlim(1);
		void Poke() { if (signal.CurrentCount == 0) signal.Release(); }
		logs.Added += Poke;
		config.UsersChanged += Poke;
		// Ends when Panda stops, too: an open stream otherwise holds the shutdown for 30 s.
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, life.ApplicationStopping);
		var ct = stop.Token;
		try
		{
			while (!ct.IsCancellationRequested)
			{
				// An empty batch every 15 s keeps proxies from closing the stream.
				await signal.WaitAsync(TimeSpan.FromSeconds(15), ct);
				await Task.Delay(250, ct); // a burst of lines goes out as one batch
				if (!StillAllowed(ctx, config, admin: true)) break;
				var batch = logs.Since(after);
				if (batch.Count > 0) after = batch[^1].Seq;
				await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(batch, SseJson)}\n\n", ct);
				await ctx.Response.Body.FlushAsync(ct);
			}
		}
		catch (OperationCanceledException) { }
		finally { logs.Added -= Poke; config.UsersChanged -= Poke; }
	}

	public sealed record LoginRequest(string? Name, string? Password);
	public sealed record UserRequest(string? Name, string? Password, string? Role);
	/// <param name="Like">Copy the server address from this bot.</param>
	public sealed record AddBotRequest(string? Like);
	public sealed record PlayRequest(string? Query, bool Now = false, string? RequestedBy = null);
	public sealed record SeekRequest(double Seconds);
	public sealed record VolumeRequest(int Volume);
	public sealed record LoopRequest(LoopMode Mode);
	public sealed record AutoplayRequest(bool On);
	public sealed record SoundRequest(string? Equalizer = null, double[]? Bands = null, string? Effect = null, double? Speed = null, bool? EightD = null, int? Crossfade = null);
	public sealed record FmRequest(string? Uuid, bool Now = false);
	public sealed record FavoriteRequest(string? Uuid = null, string? FromBot = null);

	public sealed record PlaylistRequest(string? Name, List<PlaylistSong>? Songs = null, string? FromBot = null);
	public sealed record PlayPlaylistRequest(string? Name, bool Shuffle = false);

	// A YouTube video, or a song from another site with its link (not a radio station: that's a favorite station).
	static bool IsValidSong(PlaylistSong s) =>
		(s.Source is null ? s.Id is { } id && Media.IsVideoId(id)
			: s.Source is Media.Link or Media.Direct && s.Id is { Length: > 0 and <= 120 } other && !other.Any(char.IsControl)
				&& Media.IsWebLink(s.Link) && (s.Art is null || Media.IsWebLink(s.Art)))
		&& s.Title is { Length: > 0 and <= 300 } && (s.Channel ?? "").Length <= 200 && s.Duration is >= 0 and <= 86400;
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
	public bool ConfirmCommands { get; set; } = true;
	public bool ShowSongInDescription { get; set; }
	public bool EveryoneCanUse { get; set; } = true;
	public List<ChatPerson> Members { get; set; } = [];
	public List<ulong> MemberGroups { get; set; } = [];
	public List<ChatPerson> Admins { get; set; } = [];
	public List<ulong> AdminGroups { get; set; } = [];
	public List<ChatPerson> Djs { get; set; } = [];
	public List<ulong> DjGroups { get; set; } = [];
	public List<ChatPerson> Blocked { get; set; } = [];
	public Dictionary<string, string> CommandLevels { get; set; } = [];
	/// <summary>For the panel's "recommended setup" button; ignored when saving.</summary>
	public IReadOnlyDictionary<string, string>? RecommendedLevels { get; set; }
	public string OthersSongs { get; set; } = "everyone";
	public bool OnlyFromChannel { get; set; }
	public int MaxQueueLength { get; set; }
	public int MaxSongsPerUser { get; set; }
	public bool VoteSkip { get; set; }
	public int MaxDurationMinutes { get; set; }
	public bool LibraryEnabled { get; set; }
	public int LibraryMaxSizeMb { get; set; }
	public bool NormalizeVolume { get; set; }
	public bool HasCookies { get; set; }
	/// <summary>Shared by all bots: where songs may come from (see <see cref="SourcesSettings"/>).</summary>
	public SourcesDto? Sources { get; set; }
	/// <summary>Shared by all bots: YouTube's own settings.</summary>
	public YouTubeDto? YouTube { get; set; }

	public sealed class SourcesDto
	{
		public string Search { get; set; } = "youtube";
		public bool Fallback { get; set; } = true;
		public bool YouTubeMusic { get; set; } = true;
		public bool SoundCloud { get; set; } = true;
		public bool Sites { get; set; } = true;
		public bool Services { get; set; } = true;
		public bool Pages { get; set; } = true;
		public bool Radio { get; set; } = true;
		public bool LocalLinks { get; set; }
	}

	public sealed class YouTubeDto
	{
		public string Quality { get; set; } = "best";
		public int SearchResults { get; set; } = 5;
		public bool HideLive { get; set; }
		public bool HideShort { get; set; }
		public bool SponsorBlock { get; set; }
		public List<string> SponsorBlockCategories { get; set; } = [];
		public int PlayAttempts { get; set; } = 3;
		public string RetryPlayerClients { get; set; } = "";
		public int AutoplayNoRepeat { get; set; } = 30;
		public bool AutoplaySameArtist { get; set; } = true;
	}

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
		ConfirmCommands = b.Bot.ConfirmCommands,
		ShowSongInDescription = b.Bot.ShowSongInDescription,
		EveryoneCanUse = b.Bot.EveryoneCanUse,
		Members = b.Bot.Members,
		MemberGroups = b.Bot.MemberGroups,
		RecommendedLevels = ChatPermissions.Recommended,
		Admins = b.Bot.Admins,
		AdminGroups = b.Bot.AdminGroups,
		Djs = b.Bot.Djs,
		DjGroups = b.Bot.DjGroups,
		Blocked = b.Bot.Blocked,
		CommandLevels = ChatPermissions.Commands.ToDictionary(c => c, c => ChatPermissions.Name(ChatPermissions.Required(b.Bot, c))),
		OthersSongs = b.Bot.OthersSongs ?? "everyone",
		OnlyFromChannel = b.Bot.OnlyFromChannel,
		MaxQueueLength = b.Bot.MaxQueueLength,
		MaxSongsPerUser = b.Bot.MaxSongsPerUser,
		VoteSkip = b.Bot.VoteSkip,
		MaxDurationMinutes = c.YouTube.MaxDurationMinutes,
		LibraryEnabled = c.Library.Enabled,
		LibraryMaxSizeMb = c.Library.MaxSizeMb,
		NormalizeVolume = c.Audio.Normalize,
		HasCookies = c.YouTube.CookiesFile.Length > 0 && File.Exists(c.YouTube.CookiesFile),
		Sources = new()
		{
			Search = c.Sources.Search, Fallback = c.Sources.Fallback, YouTubeMusic = c.Sources.YouTubeMusic, SoundCloud = c.Sources.SoundCloud,
			Sites = c.Sources.Sites, Services = c.Sources.Services, Pages = c.Sources.Pages, Radio = c.Sources.Radio, LocalLinks = c.Sources.LocalLinks,
		},
		YouTube = new()
		{
			Quality = c.YouTube.Quality, SearchResults = c.YouTube.SearchResults, HideLive = c.YouTube.HideLive, HideShort = c.YouTube.HideShort,
			SponsorBlock = c.YouTube.SponsorBlock, SponsorBlockCategories = c.YouTube.SponsorBlockCategories,
			PlayAttempts = c.YouTube.PlayAttempts, RetryPlayerClients = c.YouTube.RetryPlayerClients,
			AutoplayNoRepeat = c.YouTube.AutoplayNoRepeat, AutoplaySameArtist = c.YouTube.AutoplaySameArtist,
		},
	};

	// Unique ids as TeamSpeak shows them; names only for showing.
	static List<ChatPerson> People(List<ChatPerson>? list) => (list ?? [])
		.Where(p => p != null)
		.Select(p => new ChatPerson((p.Uid ?? "").Trim(), (p.Name ?? "").Trim() is var n && n.Length > 64 ? n[..64] : (p.Name ?? "").Trim()))
		.Where(p => p.Uid.Length is > 0 and <= 100)
		.DistinctBy(p => p.Uid).Take(500).ToList();

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
		if (Strings.Languages.Contains(Language)) b.Bot.Language = Language;
		b.Bot.Announce = Announce;
		b.Bot.ConfirmCommands = ConfirmCommands;
		b.Bot.ShowSongInDescription = ShowSongInDescription;
		b.Bot.EveryoneCanUse = EveryoneCanUse;
		b.Bot.Members = People(Members);
		b.Bot.MemberGroups = (MemberGroups ?? []).Distinct().Take(100).ToList();
		b.Bot.Admins = People(Admins);
		b.Bot.Djs = People(Djs);
		b.Bot.Blocked = People(Blocked);
		b.Bot.AdminGroups = (AdminGroups ?? []).Distinct().Take(100).ToList();
		b.Bot.DjGroups = (DjGroups ?? []).Distinct().Take(100).ToList();
		b.Bot.CommandLevels = (CommandLevels ?? [])
			.Where(kv => ChatPermissions.Commands.Contains(kv.Key) && ChatPermissions.IsLevel(kv.Value) && kv.Value != "everyone")
			.ToDictionary(kv => kv.Key, kv => kv.Value);
		b.Bot.OnlyFromChannel = OnlyFromChannel;
		if (ChatPermissions.IsLevel(OthersSongs)) b.Bot.OthersSongs = OthersSongs;
		if (MaxQueueLength is >= 1 and <= 1000) b.Bot.MaxQueueLength = MaxQueueLength;
		if (MaxSongsPerUser is >= 0 and <= 1000) b.Bot.MaxSongsPerUser = MaxSongsPerUser;
		b.Bot.VoteSkip = VoteSkip;
		if (MaxDurationMinutes is >= 0 and <= 1440) c.YouTube.MaxDurationMinutes = MaxDurationMinutes;
		c.Library.Enabled = LibraryEnabled;
		if (LibraryMaxSizeMb is 0 or (>= 100 and <= 1_000_000)) c.Library.MaxSizeMb = LibraryMaxSizeMb;
		c.Audio.Normalize = NormalizeVolume;
		// Left out (an older panel, a script): kept as they are.
		if (Sources is { } s)
		{
			if (SourcesSettings.Searches.Contains(s.Search)) c.Sources.Search = s.Search;
			c.Sources.Fallback = s.Fallback;
			c.Sources.YouTubeMusic = s.YouTubeMusic;
			c.Sources.SoundCloud = s.SoundCloud;
			c.Sources.Sites = s.Sites;
			c.Sources.Services = s.Services;
			c.Sources.Pages = s.Pages;
			c.Sources.Radio = s.Radio;
			c.Sources.LocalLinks = s.LocalLinks;
		}
		if (YouTube is { } y)
		{
			if (YouTubeSettings.Qualities.Contains(y.Quality)) c.YouTube.Quality = y.Quality;
			if (y.SearchResults is >= 1 and <= 10) c.YouTube.SearchResults = y.SearchResults;
			c.YouTube.HideLive = y.HideLive;
			c.YouTube.HideShort = y.HideShort;
			c.YouTube.SponsorBlock = y.SponsorBlock;
			c.YouTube.SponsorBlockCategories = (y.SponsorBlockCategories ?? []).Where(YouTubeSettings.SponsorCategories.Contains).Distinct().ToList();
			if (y.PlayAttempts is >= 1 and <= 10) c.YouTube.PlayAttempts = y.PlayAttempts;
			// yt-dlp's client names: letters, digits, "_", "-" and commas.
			var clients = (y.RetryPlayerClients ?? "").Replace(" ", "");
			if (clients.Length <= 200 && clients.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or ',')) c.YouTube.RetryPlayerClients = clients;
			if (y.AutoplayNoRepeat is >= 0 and <= PlayHistory.RecentSize) c.YouTube.AutoplayNoRepeat = y.AutoplayNoRepeat;
			c.YouTube.AutoplaySameArtist = y.AutoplaySameArtist;
		}
		// A vote is for the people who may not skip others' songs; with everyone allowed there'd be none.
		if (b.Bot.VoteSkip && b.Bot.OthersSongs == "everyone") b.Bot.OthersSongs = "dj";
		return reconnect;
	}
}
