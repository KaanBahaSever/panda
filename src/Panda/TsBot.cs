using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using TSLib;
using TSLib.Full;
using TSLib.Helper;
using TSLib.Messages;
using TSLib.Scheduler;

namespace Panda;

public sealed record BotStatus(
	string State,
	string? Error,
	string? ServerName,
	string? ChannelName,
	string Nickname,
	int Listeners,
	/// <summary>Nickname of the client the bot follows around, if any.</summary>
	string? Following = null,
	/// <summary>While connecting: the identity security level the bot is raising its identity to.</summary>
	int? RaisingLevel = null);

/// <summary>
/// Keeps the bot connected to TeamSpeak (reconnecting when needed) and handles chat commands.
/// Every TSLib call runs on the client's own scheduler thread.
/// </summary>
public sealed class TsBot : BackgroundService
{
	readonly BotConfig settings;
	readonly Player player;
	readonly Library library;
	readonly Playlists playlists;
	readonly ILogger log;
	readonly string identityPath;
	readonly DedicatedTaskScheduler scheduler = new(Id.Null);

	TsFullClient? client;
	IdentityData? identity;
	volatile string state = "connecting";
	string? lastError;
	TaskCompletionSource reconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
	// "Follow me": the bot moves along when this client changes channels, until it leaves the
	// server, says so, or the connection drops. Only touched on the scheduler thread.
	ClientId? following;
	string? followingName;
	volatile bool disposed;
	// Set when the server turned the identity down for a too low security level (scheduler thread).
	int neededLevel;
	volatile int raisingTo;

	public event Action? StatusChanged;

	/// <summary>"connecting", "connected" or "disconnected".</summary>
	public string State => state;

	BotSettings Bot => settings.Bot;
	ServerSettings Server => settings.Server;
	string Lang => Bot.Language;
	string Prefix => Bot.CommandPrefix;

	/// <param name="dir">The bot's folder, where its identity is kept.</param>
	public TsBot(BotConfig settings, Player player, Library library, Playlists playlists, string dir, ILogger log)
	{
		this.settings = settings;
		this.player = player;
		this.library = library;
		this.playlists = playlists;
		this.log = log;
		identityPath = Path.Combine(dir, "identity.json");

		player.TrackStarted += t => _ = scheduler.InvokeAsync(() => OnTrackStarted(t));
		player.TrackFailed += (t, reason) => _ = scheduler.InvokeAsync(() => OnTrackFailed(t, reason));
		player.Changed += () =>
		{
			if (player.Current is null)
				_ = scheduler.InvokeAsync(() => SetDescription(""));
		};
	}

	/// <summary>Drops the connection and connects again with the current settings.</summary>
	public void Reconnect() => reconnect.TrySetResult();

	public async Task<BotStatus> GetStatusAsync()
	{
		// A removed bot's scheduler no longer runs anything, so never wait on it for long.
		if (!disposed)
		{
			try { return await scheduler.Invoke(StatusOnScheduler).WaitAsync(TimeSpan.FromSeconds(5)); }
			catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException) { }
		}
		return new BotStatus(raisingTo > 0 ? "connecting" : "disconnected", lastError, null, null, Bot.Nickname, 0,
			RaisingLevel: raisingTo > 0 ? raisingTo : null);
	}

	BotStatus StatusOnScheduler()
	{
		var book = client?.Book;
		var self = state == "connected" ? book?.Self() : null;
		string? channelName = null;
		int listeners = 0;
		if (book != null && self != null)
		{
			channelName = book.CurrentChannel()?.Name;
			listeners = book.Clients.Values.Count(c =>
				c.Channel == self.Channel && c.Id != self.Id && c.ClientType == ClientType.Full);
		}
		return new BotStatus(state, lastError, book?.Server?.Name, channelName, self?.Name ?? Bot.Nickname, listeners, followingName,
			raisingTo > 0 ? raisingTo : null);
	}

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		identity = LoadOrCreateIdentity();
		var delay = TimeSpan.FromSeconds(3);
		var stopped = Task.Delay(Timeout.Infinite, ct);
		while (!ct.IsCancellationRequested)
		{
			var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			reconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
			SetState("connecting", null);

			var error = await scheduler.InvokeAsync(() => ConnectAsync(disconnected, ct));
			if (error != null && ct.IsCancellationRequested) break;
			if (error is null)
			{
				delay = TimeSpan.FromSeconds(3);
				SetState("connected", null);
				var done = await Task.WhenAny(disconnected.Task, reconnect.Task, stopped);
				player.AttachOutput(null);
				if (done == disconnected.Task)
				{
					log.LogWarning("Disconnected: {Reason}", disconnected.Task.Result);
					SetState("disconnected", disconnected.Task.Result);
				}
				else
				{
					await scheduler.InvokeAsync(() => client!.Disconnect());
					if (done == reconnect.Task) continue;
					break;
				}
			}
			else if (neededLevel > 0 && await RaiseIdentityAsync(ct))
			{
				continue;   // connect again right away with the stronger identity
			}
			else
			{
				log.LogWarning("Could not connect to {Address}: {Error}", Server.Address, error);
				SetState("disconnected", error);
			}

			// After a crash the old connection lingers on the server for a bit; back off and retry.
			try { await Task.WhenAny(Task.Delay(delay, ct), reconnect.Task); }
			catch (OperationCanceledException) { break; }
			delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
		}
	}

	async Task<string?> ConnectAsync(TaskCompletionSource<string> disconnected, CancellationToken ct)
	{
		Unfollow(); // client ids don't survive a reconnect
		client?.Dispose();
		var c = new TsFullClient(scheduler);
		client = c;
		c.OnDisconnected += (_, e) => disconnected.TrySetResult(e.Error?.ErrorFormat() ?? e.ExitReason.ToString());
		c.OnEachTextMessage += (_, msg) => _ = HandleMessageAsync(c, msg);
		c.OnEachClientMoved += (_, e) =>
		{
			StatusChanged?.Invoke();
			if (e.ClientId == following) _ = FollowAsync(c, e.TargetChannelId);
		};
		c.OnEachClientEnterView += (_, _) => StatusChanged?.Invoke();
		c.OnEachClientLeftView += (_, e) =>
		{
			if (e.ClientId == following)
			{
				// Moved into a channel we don't see: still a move. No target: gone from the server.
				if (e.TargetChannelId != ChannelId.Null) _ = FollowAsync(c, e.TargetChannelId);
				else Unfollow();
			}
			StatusChanged?.Invoke();
		};

		var s = Server;
		var data = new ConnectionDataFull(
			s.Address, identity!,
			username: Bot.Nickname,
			serverPassword: s.Password.Length > 0 ? Password.FromPlain(s.Password) : (Password?)null,
			defaultChannel: s.Channel,
			defaultChannelPassword: s.ChannelPassword.Length > 0 ? Password.FromPlain(s.ChannelPassword) : (Password?)null);

		log.LogInformation("Connecting to {Address} as {Nickname}", s.Address, Bot.Nickname);
		// TSLib waits forever when the server never answers (for example while our previous connection
		// is still lingering there after a crash), so give up after a while and let the loop retry.
		// Stopping (shutdown, bot removed) also gives up right away.
		var connect = c.Connect(data);
		if (await Task.WhenAny(connect, Task.Delay(TimeSpan.FromSeconds(20), ct)) != connect)
		{
			_ = c.Disconnect();
			return "the server did not answer";
		}
		var result = await connect;
		if (!result.Ok)
		{
			// The server wants a stronger identity; extra_msg says how strong.
			if (result.Error.Id == TsErrorCode.client_could_not_validate_identity
				&& int.TryParse(result.Error.ExtraMessage, out var level))
				neededLevel = level;
			return result.Error.ErrorFormat();
		}

		log.LogInformation("Connected to {Server}", c.Book.Server?.Name);
		player.AttachOutput(c);
		descriptionWarned = false;
		description = null;
		await UploadAvatarIfNeeded(c);
		var now = player.Current;
		await SetDescription(now is null ? "" : "♪ " + now.Title);
		return null;
	}

	async Task UploadAvatarIfNeeded(TsFullClient c)
	{
		try
		{
			var file = new ManifestEmbeddedFileProvider(typeof(TsBot).Assembly, "wwwroot").GetFileInfo("img/avatar.png");
			if (!file.Exists) return;
			using var ms = new MemoryStream();
			using (var s = file.CreateReadStream()) await s.CopyToAsync(ms);
			var hash = Convert.ToHexString(MD5.HashData(ms.ToArray())).ToLowerInvariant();
			if (c.Book.Self()?.AvatarHash == hash) return;
			ms.Position = 0;
			var r = await c.UploadAvatar(ms);
			if (!r.Ok) log.LogInformation("Avatar not set: {Error}", r.Error.ErrorFormat());
		}
		catch (Exception ex)
		{
			log.LogInformation("Avatar not set: {Error}", ex.Message);
		}
	}

	async Task SetDescription(string text)
	{
		if (client is not { Connected: true } c) return;
		// What Panda last wrote: the client's own view of itself doesn't follow its own edits.
		var current = description ??= c.Book.Self()?.Description ?? "";
		if (!Bot.ShowSongInDescription)
		{
			// Turned off: clear the song Panda put there, but leave a description someone else wrote.
			if (!current.StartsWith("♪ ")) return;
			text = "";
		}
		if (text.Length > 80) text = text[..79] + "…";
		if (current == text) return;
		var r = await c.ChangeDescription(text);
		if (r.Ok) description = text;
		// Said once per connection: the setting looks on in the panel, so the reason must be findable.
		else if (!descriptionWarned)
		{
			descriptionWarned = true;
			if (r.Error.Id == TsErrorCode.permissions_client_insufficient)
				log.LogWarning("Could not show the song in the bot's description: {Error}. On the TeamSpeak server, give the bot's group the permission b_client_modify_own_description", r.Error.ErrorFormat());
			else
				log.LogWarning("Could not show the song in the bot's description: {Error}", r.Error.ErrorFormat());
		}
	}
	bool descriptionWarned;
	string? description;   // null: not known yet on this connection

	/// <summary>The panel saved this bot's settings: what can change without reconnecting does so now.</summary>
	public void SettingsChanged() =>
		_ = scheduler.InvokeAsync(() => SetDescription(player.Current is { } t ? "♪ " + t.Title : ""));

	async Task OnTrackStarted(Track t)
	{
		if (client is not { Connected: true } c) return;
		await SetDescription("♪ " + t.Title);
		if (Bot.Announce)
			await c.SendChannelMessage(Strings.Get(Lang, "now-playing-by", Link(t), Strings.Duration(Lang, t), t.RequestedBy));
	}

	async Task OnTrackFailed(Track t, string reason)
	{
		if (client is not { Connected: true } c) return;
		await c.SendChannelMessage(Strings.Get(Lang, "play-failed", t.Title, Strings.ShortError(Lang, reason)));
	}

	async Task FollowAsync(TsFullClient c, ChannelId target)
	{
		if (!c.Connected || c.Book.Self()?.Channel == target) return;
		var r = await c.ClientMove(c.ClientId, target);
		if (r.Ok) StatusChanged?.Invoke();
		else log.LogInformation("Could not follow {Name}: {Error}", followingName, r.Error.ErrorFormat());
	}

	void Unfollow()
	{
		if (following is null) return;
		following = null;
		followingName = null;
		StatusChanged?.Invoke();
	}

	static string Link(Track t) => $"[url={t.Url}]{Escape(t.Title)}[/url]";

	string PlaylistNames() =>
		string.Join(", ", playlists.All().Select(p => $"[b]{Escape(p.Name)}[/b] ({p.Songs.Count})"));
	static string Escape(string s) => s.Replace("[", "(").Replace("]", ")");

	// ---- chat commands ----

	async Task HandleMessageAsync(TsFullClient c, TextMessage msg)
	{
		try
		{
			if (msg.InvokerId == c.ClientId) return;
			if (msg.Target is not (TextMessageTargetMode.Private or TextMessageTargetMode.Channel)) return;
			var text = msg.Message.Trim();
			if (!text.StartsWith(Prefix)) return;

			var space = text.IndexOf(' ');
			var cmd = (space < 0 ? text[Prefix.Length..] : text[Prefix.Length..space]).ToLowerInvariant();
			var arg = space < 0 ? "" : text[(space + 1)..].Trim();

			async Task Reply(string reply)
			{
				if (msg.Target == TextMessageTargetMode.Private)
					await c.SendPrivateMessage(reply, msg.InvokerId);
				else
					await c.SendChannelMessage(reply);
			}

			var command = Commands.Find(cmd);
			if (command is null)
			{
				// In the channel it may be meant for another bot ("!!play" for one on "!!"); stay quiet there.
				if (msg.Target == TextMessageTargetMode.Private)
					await Reply(Strings.Get(Lang, "unknown-command", Prefix));
				return;
			}
			if (!IsAllowed(c, msg))
			{
				await Reply(Strings.Get(Lang, "no-permission"));
				return;
			}
			var reply = await RunCommand(c, command, arg, msg);
			if (reply != null)
				await Reply(reply);
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Command failed: {Message}", msg.Message);
		}
	}

	bool IsAllowed(TsFullClient c, TextMessage msg)
	{
		var users = Bot.AllowedUsers;
		var groups = Bot.AllowedServerGroups;
		if (users.Count == 0 && groups.Count == 0) return true;
		if (msg.InvokerUid is { } uid && users.Contains(uid.Value)) return true;
		if (c.Book.Clients.TryGetValue(msg.InvokerId, out var who))
			return who.ServerGroups.Any(g => groups.Contains(g.Value));
		return false;
	}

	/// <summary>A reply that only confirms what the command did; left out when the bot is set not to.</summary>
	string? Confirm(string text) => Bot.ConfirmCommands ? text : null;

	async Task<string?> RunCommand(TsFullClient c, string command, string arg, TextMessage msg)
	{
		var who = msg.InvokerName.ToString();
		switch (command)
		{
		case "play":
		case "playnow":
			if (arg.Length == 0) return Strings.Get(Lang, "usage-play", Prefix);
			try
			{
				var track = await library.ResolveAsync(arg, who);
				// A song that starts now is announced in the bot's channel; that answers a command written
				// there, not one sent privately (maybe from another channel).
				var announced = Bot.Announce && msg.Target == TextMessageTargetMode.Channel;
				if (command == "playnow")
				{
					player.PlayNow(track);
					return announced ? null : Confirm(Strings.Get(Lang, "now-playing", Link(track), Strings.Duration(Lang, track)));
				}
				var pos = player.Enqueue(track);
				return pos == 0 && announced ? null
					: Confirm(pos == 0 ? Strings.Get(Lang, "now-playing", Link(track), Strings.Duration(Lang, track))
						: Strings.Get(Lang, "added", Link(track), pos, Strings.Duration(Lang, track)));
			}
			catch (YouTubeException ex) { return Strings.Error(Lang, ex.Message); }
			catch (QueueFullException) { return Strings.Get(Lang, "queue-full"); }

		case "skip":
			return player.Skip() ? Confirm(Strings.Get(Lang, "skipped")) : Strings.Get(Lang, "nothing-playing");

		case "stop":
			player.Stop();
			return Confirm(Strings.Get(Lang, "stopped"));

		case "pause":
			return player.Pause() ? Confirm(Strings.Get(Lang, "paused")) : Strings.Get(Lang, "nothing-playing");

		case "resume":
			return player.Resume() ? Confirm(Strings.Get(Lang, "resumed")) : Strings.Get(Lang, "nothing-playing");

		case "np":
		{
			var s = player.GetState();
			if (s.Current is null) return Strings.Get(Lang, "nothing-playing");
			var t = s.Current.Track;
			var pos = Strings.FormatTime(s.Current.Position);
			return Strings.Get(Lang, "now-playing", Link(t), t.IsLive ? Strings.Get(Lang, "live") : $"{pos} / {Strings.FormatTime(t.Duration)}")
				+ (s.Current.Paused ? " ⏸" : "");
		}

		case "queue":
		{
			// "!liste rock" plays the playlist "rock"; "!liste" alone still shows the queue.
			if (arg.Length > 0) goto case "playlist";
			var s = player.GetState();
			if (s.Current is null && s.Queue.Count == 0) return Strings.Get(Lang, "queue-empty");
			var sb = new StringBuilder();
			if (s.Current != null)
				sb.Append("▶ ").Append(Link(s.Current.Track)).Append('\n');
			if (s.Queue.Count > 0)
			{
				sb.Append(Strings.Get(Lang, "queue-title", s.Queue.Count)).Append('\n');
				foreach (var (t, i) in s.Queue.Take(10).Select((t, i) => (t, i)))
					sb.Append($"[b]{i + 1}.[/b] {Escape(t.Title)} ({Strings.Duration(Lang, t)})\n");
				if (s.Queue.Count > 10)
					sb.Append(Strings.Get(Lang, "queue-more", s.Queue.Count - 10));
			}
			return sb.ToString().TrimEnd();
		}

		case "vol":
			if (arg.Length == 0) return Strings.Get(Lang, "volume", player.Volume);
			if (!int.TryParse(arg.TrimEnd('%'), out var v)) return Strings.Get(Lang, "volume", player.Volume);
			player.SetVolume(v);
			return Confirm(Strings.Get(Lang, "volume-set", player.Volume));

		case "remove":
		{
			if (!int.TryParse(arg, out var n)) return Strings.Get(Lang, "bad-index", arg);
			var removed = player.Remove(n - 1);
			return removed is null ? Strings.Get(Lang, "bad-index", n) : Confirm(Strings.Get(Lang, "removed", Escape(removed.Title)));
		}

		case "clear":
			player.ClearQueue();
			return Confirm(Strings.Get(Lang, "cleared"));

		case "shuffle":
			player.Shuffle();
			return Confirm(Strings.Get(Lang, "shuffled"));

		case "loop":
		{
			var mode = arg.ToLowerInvariant() switch
			{
				"off" or "kapalı" or "kapali" or "non" or "désactivé" or "desactive" or "0" => LoopMode.Off,
				"one" or "song" or "şarkı" or "sarki" or "un" or "morceau" or "1" => LoopMode.One,
				"all" or "queue" or "hepsi" or "tümü" or "tout" or "file" => LoopMode.All,
				_ => player.Loop switch { LoopMode.Off => LoopMode.All, LoopMode.All => LoopMode.One, _ => LoopMode.Off },
			};
			player.SetLoop(mode);
			return Confirm(Strings.Get(Lang, mode switch { LoopMode.One => "loop-one", LoopMode.All => "loop-all", _ => "loop-off" }));
		}

		case "playlist":
		{
			if (arg.Length == 0) goto case "playlists";
			if (playlists.Find(arg) is not { } list)
				return Strings.Get(Lang, "playlist-not-found", Escape(arg), PlaylistNames());
			var added = player.EnqueueMany(list.Songs.Select(s => s.ToTrack(who)));
			return added == 0 && list.Songs.Count > 0 ? Strings.Get(Lang, "queue-full")
				: Confirm(Strings.Get(Lang, "playlist-added", added, Escape(list.Name)));
		}

		case "playlists":
			return playlists.All().Count == 0 ? Strings.Get(Lang, "playlists-none") : Strings.Get(Lang, "playlists", PlaylistNames());

		case "fav":
		{
			if (player.Current is not { } now) return Strings.Get(Lang, "nothing-playing");
			// One shared list; it is named in the bot's chat language when it's first made.
			var name = Strings.Languages.Select(l => playlists.Find(Strings.Get(l, "favorites"))?.Name).FirstOrDefault(n => n != null)
				?? Strings.Get(Lang, "favorites");
			playlists.Save(name, [PlaylistSong.From(now)], out var left);
			if (left > 0) return Strings.Get(Lang, "playlist-full", Escape(name), Playlists.MaxSongs);
			return Confirm(Strings.Get(Lang, "fav-added", Link(now), Escape(name)));
		}

		case "radio":
		{
			var on = arg.ToLowerInvariant() switch
			{
				"on" or "aç" or "ac" or "açık" or "acik" or "oui" or "activer" or "1" => true,
				"off" or "kapat" or "kapalı" or "kapali" or "non" or "désactiver" or "desactiver" or "0" => false,
				_ => !player.Radio,
			};
			player.SetRadio(on);
			return Confirm(Strings.Get(Lang, on ? "radio-on" : "radio-off"));
		}

		case "follow":
		{
			// "!takip" follows the one who wrote it; again (or "!takip kapat") stops.
			var stop = arg.ToLowerInvariant() is "off" or "stop" or "kapat" or "kapalı" or "kapali" or "bırak" or "birak" or "0";
			if (stop || following == msg.InvokerId)
			{
				if (following is null) return Strings.Get(Lang, "not-following");
				Unfollow();
				return Confirm(Strings.Get(Lang, "follow-off"));
			}
			if (!c.Book.Clients.TryGetValue(msg.InvokerId, out var invoker)) return Strings.Get(Lang, "join-failed");
			following = msg.InvokerId;
			followingName = who;
			StatusChanged?.Invoke();
			await FollowAsync(c, invoker.Channel);
			return Confirm(Strings.Get(Lang, "follow-on", Escape(who), Prefix));
		}

		case "join":
		{
			if (!c.Book.Clients.TryGetValue(msg.InvokerId, out var invoker)) return Strings.Get(Lang, "join-failed");
			var r = await c.ClientMove(c.ClientId, invoker.Channel);
			StatusChanged?.Invoke();
			return r.Ok ? Confirm(Strings.Get(Lang, "joined")) : Strings.Get(Lang, "join-failed");
		}

		case "help":
			return Strings.Get(Lang, "help", Prefix);
		}
		return null;
	}

	// ---- identity ----

	IdentityData LoadOrCreateIdentity()
	{
		if (File.Exists(identityPath))
		{
			try
			{
				if (!OperatingSystem.IsWindows())
					File.SetUnixFileMode(identityPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
				var saved = JsonSerializer.Deserialize<SavedIdentity>(File.ReadAllText(identityPath));
				if (saved != null)
				{
					var loaded = TsCrypt.LoadIdentityDynamic(saved.Key, saved.Offset);
					if (loaded.Ok) return loaded.Value;
				}
			}
			catch (Exception ex)
			{
				log.LogWarning("Could not read {Path}: {Error}; creating a new identity", identityPath, ex.Message);
			}
		}
		log.LogInformation("Creating a new TeamSpeak identity");
		var id = TsCrypt.GenerateNewIdentity(8);
		SaveIdentity(id);
		return id;
	}

	void SaveIdentity(IdentityData id) =>
		SafeFile.Write(identityPath, JsonSerializer.Serialize(new SavedIdentity(id.PrivateKeyString, id.ValidKeyOffset)), ownerOnly: true);

	/// <summary>
	/// Raises the identity to the security level the server asked for, once; the key and so the unique
	/// ID stay the same. False when that isn't possible or Panda is stopping.
	/// </summary>
	async Task<bool> RaiseIdentityAsync(CancellationToken ct)
	{
		var level = neededLevel;
		neededLevel = 0;
		var id = identity!;
		if (IdentityLevel.Of(id.PublicKeyString, id.ValidKeyOffset) >= level) return false;
		if (level > IdentityLevel.Max)
		{
			log.LogWarning("The server needs identity security level {Level}; Panda goes up to {Max}", level, IdentityLevel.Max);
			return false;
		}

		log.LogInformation("The server needs identity security level {Level}; raising it (only once, can take a minute)", level);
		raisingTo = level;
		SetState("connecting", null);
		var time = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			var offset = await Task.Run(() => IdentityLevel.Find(id.PublicKeyString, level, ct));
			if (offset is null || IdentityLevel.Of(id.PublicKeyString, offset.Value) < level) return false;
			id.ValidKeyOffset = id.LastCheckedKeyOffset = offset.Value;
			SaveIdentity(id);
			log.LogInformation("Identity security level is now {Level} ({Seconds:0} s)",
				IdentityLevel.Of(id.PublicKeyString, id.ValidKeyOffset), time.Elapsed.TotalSeconds);
			return true;
		}
		finally { raisingTo = 0; }
	}

	sealed record SavedIdentity(string Key, ulong Offset);

	void SetState(string newState, string? error)
	{
		state = newState;
		lastError = error;
		StatusChanged?.Invoke();
	}

	public override void Dispose()
	{
		if (disposed) return;
		disposed = true;
		base.Dispose();
		client?.Dispose();
		scheduler.Dispose();
	}
}

/// <summary>Command names and their aliases (English + Turkish, with and without Turkish letters).</summary>
public static class Commands
{
	static readonly Dictionary<string, string> Aliases = new()
	{
		["play"] = "play", ["p"] = "play", ["çal"] = "play", ["cal"] = "play", ["oynat"] = "play", ["jouer"] = "play", ["joue"] = "play",
		["playnow"] = "playnow", ["şimdiçal"] = "playnow", ["simdical"] = "playnow", ["jouermaintenant"] = "playnow",
		["skip"] = "skip", ["next"] = "skip", ["s"] = "skip", ["geç"] = "skip", ["gec"] = "skip", ["atla"] = "skip", ["passer"] = "skip", ["suivant"] = "skip",
		["stop"] = "stop", ["dur"] = "stop", ["arrêt"] = "stop", ["arret"] = "stop", ["arrêter"] = "stop", ["arreter"] = "stop",
		["pause"] = "pause", ["duraklat"] = "pause",
		["resume"] = "resume", ["unpause"] = "resume", ["devam"] = "resume", ["reprendre"] = "resume",
		["queue"] = "queue", ["q"] = "queue", ["list"] = "queue", ["sıra"] = "queue", ["sira"] = "queue", ["liste"] = "queue", ["file"] = "queue",
		["np"] = "np", ["now"] = "np", ["nowplaying"] = "np", ["şimdi"] = "np", ["simdi"] = "np", ["çalan"] = "np", ["encours"] = "np",
		["vol"] = "vol", ["volume"] = "vol", ["v"] = "vol", ["ses"] = "vol",
		["remove"] = "remove", ["rm"] = "remove", ["sil"] = "remove", ["retirer"] = "remove", ["supprimer"] = "remove",
		["clear"] = "clear", ["temizle"] = "clear", ["vider"] = "clear",
		["shuffle"] = "shuffle", ["karıştır"] = "shuffle", ["karistir"] = "shuffle", ["mélanger"] = "shuffle", ["melanger"] = "shuffle",
		["loop"] = "loop", ["repeat"] = "loop", ["tekrar"] = "loop", ["boucle"] = "loop", ["répéter"] = "loop", ["repeter"] = "loop",
		["join"] = "join", ["come"] = "join", ["gel"] = "join", ["viens"] = "join", ["rejoindre"] = "join",
		["radio"] = "radio", ["radyo"] = "radio",
		["follow"] = "follow", ["followme"] = "follow", ["takip"] = "follow", ["takipet"] = "follow", ["suivre"] = "follow",
		["playlist"] = "playlist", ["pl"] = "playlist", ["çalmalistesi"] = "playlist", ["calmalistesi"] = "playlist",
		["playlists"] = "playlists", ["lists"] = "playlists", ["listeler"] = "playlists",
		["fav"] = "fav", ["favorite"] = "fav", ["favori"] = "fav",
		["help"] = "help", ["h"] = "help", ["yardım"] = "help", ["yardim"] = "help", ["komutlar"] = "help", ["aide"] = "help",
	};

	public static string? Find(string name) => Aliases.GetValueOrDefault(name);
}
