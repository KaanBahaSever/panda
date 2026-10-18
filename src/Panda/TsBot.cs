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

/// <summary>Someone on the TeamSpeak server, for picking DJs and admins in the panel.</summary>
public sealed record TsPerson(string Uid, string Name, string Channel, IReadOnlyList<ulong> Groups);
public sealed record TsGroup(ulong Id, string Name);
public sealed record TsPeople(bool Connected, IReadOnlyList<TsPerson> People, IReadOnlyList<TsGroup> Groups);

/// <summary>
/// Whether the bot has what a feature needs on the server. Ok is null while it's unknown (not tried
/// yet on this connection). Permission is what to grant when it's missing.
/// </summary>
public sealed record PermissionCheck(string Key, bool? Ok, string Permission, string? Detail);

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
	readonly PandaConfig config;
	readonly Library library;
	readonly Sources sources;
	readonly RadioBrowser radios;
	readonly RadioFavorites favorites;
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
	/// <summary>"!create bot": makes a new bot in that channel (set by BotManager).</summary>
	public Func<ulong, Task<(BotInstance? Bot, string? Error)>>? CreateBot { get; set; }

	/// <summary>People (not bots) in the bot's channel; -1 when not connected.</summary>
	public async Task<int> CountListenersAsync()
	{
		try
		{
			return await scheduler.Invoke(() =>
			{
				if (client is not { Connected: true } c || c.Book.Self() is not { } self) return -1;
				return c.Book.Clients.Values.Count(x => x.Channel == self.Channel && x.Id != self.Id && x.ClientType == ClientType.Full);
			}).WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (Exception ex) when (ex is TimeoutException or ObjectDisposedException or TaskCanceledException) { return -1; }
	}

	/// <summary>"connecting", "connected" or "disconnected".</summary>
	public string State => state;

	BotSettings Bot => settings.Bot;
	ServerSettings Server => settings.Server;
	string Lang => Bot.Language;
	string Prefix => Bot.CommandPrefix;

	/// <param name="dir">The bot's folder, where its identity is kept.</param>
	public TsBot(BotConfig settings, PandaConfig config, Player player, Library library, Sources sources, RadioBrowser radios,
		RadioFavorites favorites, Playlists playlists, string dir, ILogger log)
	{
		this.settings = settings;
		this.config = config;
		this.player = player;
		this.library = library;
		this.sources = sources;
		this.radios = radios;
		this.favorites = favorites;
		this.playlists = playlists;
		this.log = log;
		identityPath = Path.Combine(dir, "identity.json");

		player.TrackStarted += t => _ = scheduler.InvokeAsync(() => OnTrackStarted(t));
		player.TrackFailed += (t, reason) => _ = scheduler.InvokeAsync(() => OnTrackFailed(t, reason));
		player.StreamTitleChanged += (t, song) => _ = scheduler.InvokeAsync(() => SetDescription(Describe(t)));
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
		lock (checks) checks.Clear();
		_ = LoadServerGroups(c);
		player.AttachOutput(c);
		descriptionWarned = false;
		description = null;
		await UploadAvatarIfNeeded(c);
		var now = player.Current;
		await SetDescription(now is null ? "" : Describe(now));
		return null;
	}

	// ---- the server's groups and people, and what the bot may do there (for the panel) ----

	readonly Dictionary<ulong, string> serverGroups = [];
	readonly Dictionary<string, PermissionCheck> checks = [];

	async Task LoadServerGroups(TsFullClient c)
	{
		try { await LoadServerGroupsCore(c); }
		catch (Exception ex) { log.LogInformation("Could not read the server groups: {Error}", ex.Message); }
	}

	async Task LoadServerGroupsCore(TsFullClient c)
	{
		var r = await c.SendHybrid<TSLib.Messages.ServerGroupList>(new TSLib.Commands.TsCommand("servergrouplist"), TSLib.Messages.NotificationType.ServerGroupList);
		lock (serverGroups)
		{
			serverGroups.Clear();
			if (r.Ok)
				foreach (var g in r.Value.Where(g => g.GroupType == GroupType.Regular))
					serverGroups[g.ServerGroupId.Value] = g.Name;
		}
		Note("groups", r.Ok ? null : r.Error, "b_virtualserver_servergroup_list");
	}

	void Note<T>(string key, R<T, CommandError> result, string permission) where T : notnull =>
		Note(key, result.Ok ? null : result.Error, permission);

	/// <summary>Remembers whether the last try of a feature worked; only a missing permission counts as a no.</summary>
	void Note(string key, CommandError? error, string permission)
	{
		bool? ok = error is null ? true : error.Id == TsErrorCode.permissions_client_insufficient ? false : null;
		if (ok is null) return;
		lock (checks) checks[key] = new PermissionCheck(key, ok, permission, error?.ErrorFormat());
	}

	void Note(string key, E<CommandError> result, string permission) =>
		Note(key, result.Ok ? null : result.Error, permission);

	void NoteMove(E<CommandError> r) => Note("move", r, "i_channel_join_power");

	/// <summary>People online (not the bot) and the server's groups.</summary>
	public async Task<TsPeople> GetPeopleAsync()
	{
		try
		{
			return await scheduler.Invoke(() =>
			{
				if (client is not { Connected: true } c) return new TsPeople(false, [], []);
				var self = c.Book.Self();
				var people = c.Book.Clients.Values
					.Where(x => x.Id != self?.Id && x.ClientType == ClientType.Full && x.Uid is not null)
					.Select(x => new TsPerson(x.Uid!.Value.Value, x.Name, c.Book.Channels.GetValueOrDefault(x.Channel)?.Name ?? "",
						x.ServerGroups.Select(g => g.Value).ToList()))
					.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
				List<TsGroup> groups;
				lock (serverGroups) groups = serverGroups.Select(g => new TsGroup(g.Key, g.Value)).OrderBy(g => g.Name).ToList();
				return new TsPeople(true, people, groups);
			}).WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException)
		{
			return new TsPeople(false, [], []);
		}
	}

	/// <summary>
	/// What the bot can do on the server: be heard (talk power against the channel's), write in the chat,
	/// show the song in its description, change channels (!join, !follow) and see the server groups.
	/// </summary>
	public async Task<IReadOnlyList<PermissionCheck>> GetPermissionsAsync()
	{
		try
		{
			return await scheduler.Invoke(() =>
			{
				var list = new List<PermissionCheck>();
				if (client is not { Connected: true } c) return list;
				var self = c.Book.Self();
				var need = c.Book.CurrentChannel()?.NeededTalkPower ?? 0;
				var have = self?.TalkPower ?? 0;
				list.Add(new PermissionCheck("talk", need <= 0 || have >= need || self?.TalkPowerGranted == true, "i_client_talk_power", $"{have}/{need}"));
				lock (checks)
					foreach (var key in new[] { "chat", "description", "move", "groups" })
						list.Add(checks.TryGetValue(key, out var ch) ? ch : new PermissionCheck(key, null, key switch
						{
							"chat" => "b_client_channel_textmessage_send",
							"description" => "b_client_modify_own_description",
							"move" => "i_channel_join_power",
							_ => "b_virtualserver_servergroup_list",
						}, null));
				return list;
			}).WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException)
		{
			return [];
		}
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
		Note("description", r, "b_client_modify_own_description");
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
		_ = scheduler.InvokeAsync(() => SetDescription(player.Current is { } t ? Describe(t) : ""));

	// A radio station with the song it plays, when it says.
	string Describe(Track t) =>
		t.Source == Media.Radio && player.StreamTitle is { } song && player.Current == t ? $"📻 {t.Title}: {song}" : "♪ " + t.Title;

	async Task OnTrackStarted(Track t)
	{
		lock (votes) { voteTrack = null; votes.Clear(); }
		if (client is not { Connected: true } c) return;
		await SetDescription(Describe(t));
		if (Bot.Announce)
			Note("chat", await c.SendChannelMessage(Strings.Get(Lang, "now-playing-by", Link(t), Strings.Duration(Lang, t), t.RequestedBy)), "b_client_channel_textmessage_send");
	}

	async Task OnTrackFailed(Track t, string reason)
	{
		if (client is not { Connected: true } c) return;
		// Titles from other sites (a station's name) and the tools' error text come from outside: no BBCode, and for a
		// link no inner details (a scanner would learn which local addresses answer).
		var why = t.IsYouTube || Strings.IsKnownError(reason) ? Strings.ShortError(Lang, reason) : Strings.ShortError(Lang, "link-unplayable");
		Note("chat", await c.SendChannelMessage(Strings.Get(Lang, "play-failed", Escape(t.Title), Escape(why))), "b_client_channel_textmessage_send");
	}

	async Task FollowAsync(TsFullClient c, ChannelId target)
	{
		if (!c.Connected || c.Book.Self()?.Channel == target) return;
		var r = await c.ClientMove(c.ClientId, target);
		NoteMove(r);
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

	// Links from other sites come from people: no brackets or spaces that could end the BBCode early.
	static string Link(Track t) =>
		$"[url={t.Url.Replace("[", "%5B").Replace("]", "%5D").Replace(" ", "%20")}]{Escape(t.Title)}[/url]";

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
				foreach (var part in SplitMessage(reply))
				{
					var r = msg.Target == TextMessageTargetMode.Private
						? await c.SendPrivateMessage(part, msg.InvokerId)
						: await c.SendChannelMessage(part);
					if (msg.Target != TextMessageTargetMode.Private) Note("chat", r, "b_client_channel_textmessage_send");
				}
			}

			var level = LevelOf(c, msg);
			if (msg.InvokerUid?.Value is { } blockedUid && Bot.Blocked.Any(p => p.Uid == blockedUid)) return;
			var command = Commands.Find(cmd);
			if (command is null && cmd.Length is > 0 and <= 2 && cmd.All(char.IsAsciiDigit)
				&& (msg.Target == TextMessageTargetMode.Private || HasSearch(msg)))
			{
				command = "pick";
				arg = cmd;
			}
			if (command is null)
			{
				// In the channel it may be meant for another bot ("!!play" for one on "!!"); stay quiet there.
				if (msg.Target == TextMessageTargetMode.Private)
					await Reply(Strings.Get(Lang, "unknown-command", Prefix));
				return;
			}
			if (level == ChatLevel.None)
			{
				await Reply(Strings.Get(Lang, "no-permission"));
				return;
			}
			// Up to 0.5 !radio was autoplay: alone (or with on/off) it still is, with a name it plays a station.
			var oldRadio = command == "radio";
			if (oldRadio) command = IsSwitch(arg) ? "autoplay" : "fm";
			// "!queue rock" plays a playlist and "!playlist" alone lists them: the level goes by what happens.
			var acting = command switch
			{
				"queue" when arg.Length > 0 => "playlist", "playlist" when arg.Length == 0 => "playlists", "fm" when arg.Length == 0 => "stations",
				_ => command,
			};
			var asking = acting is "np" or "queue" or "help" or "playlists" or "stations";
			if (Bot.OnlyFromChannel && level < ChatLevel.Admin && !asking && !InBotChannel(c, msg))
			{
				await Reply(Strings.Get(Lang, "only-in-channel"));
				return;
			}
			var need = asking ? ChatLevel.Everyone : ChatPermissions.Required(Bot, acting);
			if (level < need)
			{
				await Reply(Strings.Get(Lang, "need-level", Prefix + cmd, LevelName(need)));
				return;
			}
			var reply = await RunCommand(c, command, arg, msg, level);
			if (oldRadio && command == "autoplay")
				reply = (reply is null ? "" : reply + "\n") + Strings.Get(Lang, "radio-renamed", Prefix);
			if (reply != null)
				await Reply(reply);
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Command failed: {Message}", msg.Message);
		}
	}

	/// <summary>
	/// TeamSpeak turns down messages over 1024 bytes; a long reply (the help, a search) goes out in
	/// parts, cut between lines.
	/// </summary>
	internal static IEnumerable<string> SplitMessage(string text, int maxBytes = 1000)
	{
		var part = new StringBuilder();
		foreach (var line in text.Split('\n').SelectMany(l => CutLine(l, maxBytes)))
		{
			if (part.Length > 0 && Encoding.UTF8.GetByteCount(part.ToString()) + 1 + Encoding.UTF8.GetByteCount(line) > maxBytes)
			{
				yield return part.ToString();
				part.Clear();
			}
			if (part.Length > 0) part.Append('\n');
			part.Append(line);
		}
		if (part.Length > 0) yield return part.ToString();
	}

	// A line too long for one message, in pieces of whole characters.
	static IEnumerable<string> CutLine(string line, int maxBytes)
	{
		if (Encoding.UTF8.GetByteCount(line) <= maxBytes) { yield return line; yield break; }
		var piece = new StringBuilder();
		var bytes = 0;
		foreach (var rune in line.EnumerateRunes())
		{
			var n = rune.Utf8SequenceLength;
			if (bytes + n > maxBytes) { yield return piece.ToString(); piece.Clear(); bytes = 0; }
			piece.Append(rune.ToString());
			bytes += n;
		}
		if (piece.Length > 0) yield return piece.ToString();
	}

	ChatLevel LevelOf(TsFullClient c, TextMessage msg)
	{
		var groups = c.Book.Clients.TryGetValue(msg.InvokerId, out var who) ? who.ServerGroups.Select(g => g.Value) : [];
		return ChatPermissions.LevelOf(Bot, msg.InvokerUid?.Value, groups);
	}

	static bool InBotChannel(TsFullClient c, TextMessage msg) =>
		c.Book.Self() is { } self && c.Book.Clients.TryGetValue(msg.InvokerId, out var who) && who.Channel == self.Channel;

	string LevelName(ChatLevel level) => Strings.Get(Lang, level == ChatLevel.Admin ? "level-admin" : "level-dj");

	// ---- other people's songs ----

	/// <summary>A song is someone's own when they asked for it; autoplay's picks are nobody's.</summary>
	static bool Own(Track t, TextMessage msg) => t.Autoplay || (msg.InvokerUid?.Value is { } uid && t.RequesterId == uid);

	bool MayChangeOthers(ChatLevel level) => level >= ChatPermissions.Parse(Bot.OthersSongs);

	/// <summary>The answer to touching someone else's song without the level. Cutting the song playing
	/// short can be voted on (when that's on); changes to the queue can't.</summary>
	string OthersRefusal(bool cutsSong = true) => Bot.VoteSkip && cutsSong
		? Strings.Get(Lang, "vote-needed", Prefix)
		: Strings.Get(Lang, "others-song", LevelName(ChatPermissions.Parse(Bot.OthersSongs)));

	// A song that starts now is announced in the bot's channel; that answers a command written there,
	// not one sent privately (maybe from another channel).
	// "on"/"off" in the bot's languages; null for anything else.
	static bool? Switch(string arg) => arg.ToLowerInvariant() switch
	{
		"on" or "aç" or "ac" or "açık" or "acik" or "oui" or "activer" or "an" or "1" => true,
		"off" or "kapat" or "kapalı" or "kapali" or "non" or "désactiver" or "desactiver" or "aus" or "0" => false,
		_ => null,
	};

	static bool IsSwitch(string arg) => arg.Length == 0 || Switch(arg) != null;

	int MaxBotsHint() => Math.Max(1, config.Panel.MaxBots);

	string EqLabel(string eq) => Strings.Get(Lang, "eqn-" + eq);

	string EffectsNow(Sound s)
	{
		var on = new List<string>();
		if (s.Effect != "off") on.Add(Strings.Get(Lang, "fx-" + s.Effect));
		if (s.EightD) on.Add("8D");
		if (Math.Abs(s.Speed - 1) >= 0.01) on.Add(FormatSpeed(s.Speed));
		if (s.Equalizer != "off") on.Add(EqLabel(s.Equalizer));
		return on.Count == 0 ? Strings.Get(Lang, "fx-none") : string.Join(", ", on);
	}

	static string FormatSpeed(double speed) => "×" + speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

	/// <summary>"1.25", "1,25", "x1.5", "125%"; null when it isn't a speed between 0.5 and 2.</summary>
	internal static double? ParseSpeed(string arg)
	{
		var t = arg.Trim().TrimStart('x', 'X', '×').Replace(',', '.');
		var percent = t.EndsWith('%');
		if (!double.TryParse(percent ? t[..^1] : t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return null;
		if (percent) v /= 100;
		return v is >= 0.5 and <= 2 ? Math.Round(v, 2) : null;
	}

	/// <summary>The rest of an album or playlist is looked up in the background and added as it's found;
	/// a !stop or !clear (or the queue filling up) ends it. Says how it went when it's done.</summary>
	void AddListLater(TextMessage msg, Resolved r, bool exempt)
	{
		var target = msg.Target;
		var invoker = msg.InvokerId;
		var uid = msg.InvokerUid?.Value;
		var epoch = player.Epoch;
		_ = Task.Run(async () =>
		{
			var (added, missed) = await sources.AddAllAsync(r.More!, msg.InvokerName.ToString(), t =>
			{
				if (disposed || player.Epoch != epoch) return false;
				try { player.Enqueue(t with { RequesterId = uid }, exempt); return true; }
				catch (Exception ex) when (ex is QueueFullException or UserLimitException) { return false; }
			});
			if (disposed || !Bot.ConfirmCommands) return;
			var text = Strings.Get(Lang, "list-added", added + 1, Escape(r.ListName ?? ""))
				+ (missed > 0 ? " " + Strings.Get(Lang, "list-missed", missed) : "");
			await scheduler.InvokeAsync(async () =>
			{
				if (client is not { Connected: true } c) return;
				if (target == TextMessageTargetMode.Private) await c.SendPrivateMessage(text, invoker);
				else Note("chat", await c.SendChannelMessage(text), "b_client_channel_textmessage_send");
			});
		});
	}

	string? Queue(Track track, bool now, TextMessage msg, bool exempt)
	{
		var announced = Bot.Announce && msg.Target == TextMessageTargetMode.Channel;
		if (now)
		{
			player.PlayNow(track, exempt);
			return announced ? null : Confirm(Strings.Get(Lang, "now-playing", Link(track), Strings.Duration(Lang, track)));
		}
		var pos = player.Enqueue(track, exempt);
		return pos == 0 && announced ? null
			: Confirm(pos == 0 ? Strings.Get(Lang, "now-playing", Link(track), Strings.Duration(Lang, track))
				: Strings.Get(Lang, "added", Link(track), pos, Strings.Duration(Lang, track)));
	}

	// !search results per person, picked with !1, !2...; they go stale after a few minutes.
	int SearchResults => Math.Clamp(config.YouTube.SearchResults, 1, 10);
	static readonly TimeSpan SearchKeep = TimeSpan.FromMinutes(5);
	readonly Dictionary<string, (List<Track> Results, DateTime At)> searches = [];
	static string SearchKey(TextMessage msg) => msg.InvokerUid?.Value ?? "client:" + msg.InvokerId;

	List<Track>? FindSearch(TextMessage msg)
	{
		lock (searches)
			return searches.TryGetValue(SearchKey(msg), out var s) && DateTime.UtcNow - s.At <= SearchKeep ? s.Results : null;
	}

	bool HasSearch(TextMessage msg) => FindSearch(msg) != null;

	void KeepSearch(TextMessage msg, List<Track> results)
	{
		lock (searches)
		{
			foreach (var stale in searches.Where(s => DateTime.UtcNow - s.Value.At > SearchKeep).Select(s => s.Key).ToList())
				searches.Remove(stale);
			searches[SearchKey(msg)] = (results, DateTime.UtcNow);
		}
	}

	/// <summary>"1:30", "90", "1:02:03", or "+10" / "-10" from <paramref name="position"/>; null if it isn't a time.</summary>
	internal static int? ParseSeek(string arg, int position)
	{
		arg = arg.Trim();
		var sign = arg.StartsWith('+') ? 1 : arg.StartsWith('-') ? -1 : 0;
		var parts = (sign == 0 ? arg : arg[1..]).Split(':');
		if (parts.Length is < 1 or > 3) return null;
		var seconds = 0;
		foreach (var p in parts)
		{
			if (!int.TryParse(p, System.Globalization.NumberStyles.None, null, out var v) || v > 100_000) return null;
			seconds = seconds * 60 + v;
		}
		return sign == 0 ? seconds : Math.Max(0, position + sign * seconds);
	}

	// Votes to skip the song playing: the people (unique ids) who voted for it.
	Track? voteTrack;
	readonly HashSet<string> votes = [];

	/// <summary>A vote to skip: half of the people in the bot's channel (rounded up) skip the song.</summary>
	string SkipVote(TsFullClient c, TextMessage msg, Track now)
	{
		var self = c.Book.Self();
		if (self is null) return Strings.Get(Lang, "nothing-playing");
		static string Key(TSLib.Full.Book.Client x) => x.Uid?.Value ?? "client:" + x.Id;
		var listeners = c.Book.Clients.Values
			.Where(x => x.Channel == self.Channel && x.Id != self.Id && x.ClientType == ClientType.Full)
			.Select(Key).ToHashSet();
		if (!c.Book.Clients.TryGetValue(msg.InvokerId, out var voter) || !listeners.Contains(Key(voter)))
			return Strings.Get(Lang, "vote-not-here");
		int have, need;
		lock (votes)
		{
			if (!ReferenceEquals(voteTrack, now)) { voteTrack = now; votes.Clear(); }
			votes.Add(Key(voter));
			// Only the votes of people still in the channel count.
			have = votes.Count(listeners.Contains);
			need = Math.Max(1, (listeners.Count + 1) / 2);
			if (have < need) return Strings.Get(Lang, "vote-progress", have, need);
			voteTrack = null;
			votes.Clear();
		}
		player.Skip(now);
		return Strings.Get(Lang, "vote-skipped", have, need);
	}

	/// <summary>A reply that only confirms what the command did; left out when the bot is set not to.</summary>
	string? Confirm(string text) => Bot.ConfirmCommands ? text : null;

	async Task<string?> RunCommand(TsFullClient c, string command, string arg, TextMessage msg, ChatLevel level)
	{
		var who = msg.InvokerName.ToString();
		var others = !MayChangeOthers(level);
		// Commands that cut the song playing short, or throw away queued songs, of someone else.
		// (!skipto checks its number first: a wrong number is answered as one.)
		// A sleep timer stops the bot later: the same as !stop.
		var stopping = command == "stop" || (command == "sleep" && arg.Length > 0 && Switch(arg) != false);
		if (others && (command is "playnow" or "back" || stopping) && player.Current is { } playing && !Own(playing, msg))
			return OthersRefusal();
		if (others && (command is "clear" or "shuffle" || stopping) && player.GetState().Queue.Any(t => !Own(t, msg)))
			return OthersRefusal(cutsSong: false);
		if (others && command == "skipto" && int.TryParse(arg, out var upTo) && player.GetState().Queue is var q && upTo >= 1 && upTo <= q.Count
			&& q.Take(upTo - 1).Any(t => !Own(t, msg)))
			return OthersRefusal(cutsSong: false);
		// Winding someone else's song (to its end, say) is cutting it short too.
		if (others && command == "seek" && player.Current is { } wound && !Own(wound, msg))
			return OthersRefusal();
		// DJs and admins aren't held to the songs-per-person limit.
		var exempt = level >= ChatLevel.Dj;

		switch (command)
		{
		case "play":
		case "playnow":
			if (arg.Length == 0) return Strings.Get(Lang, "usage-play", Prefix);
			try
			{
				var r = await sources.ResolveAsync(arg, who);
				var reply = Queue(r.Track with { RequesterId = msg.InvokerUid?.Value }, command == "playnow", msg, exempt);
				// An album or a playlist: the rest is looked up and added one by one.
				if (r.More is { Count: > 0 } more)
				{
					AddListLater(msg, r, exempt);
					reply = (reply is null ? "" : reply + "\n") + Strings.Get(Lang, "list-adding", more.Count, Escape(r.ListName ?? ""));
				}
				return reply;
			}
			catch (YouTubeException ex) { return Strings.Error(Lang, ex.Message); }
			catch (QueueFullException) { return Strings.Get(Lang, "queue-full"); }
			catch (UserLimitException ex) { return Strings.Get(Lang, "user-limit", ex.Max); }

		case "search":
		{
			if (arg.Length == 0) return Strings.Get(Lang, "usage-search", Prefix);
			List<Track> results;
			try { results = await sources.SearchAnyAsync(arg, SearchResults, who); }
			catch (YouTubeException ex) { return Strings.Error(Lang, ex.Message); }
			if (results.Count == 0) return Strings.Get(Lang, "err-not-found");
			KeepSearch(msg, results);
			var sb = new System.Text.StringBuilder(Strings.Get(Lang, "search-results", Prefix, results.Count));
			for (int i = 0; i < results.Count; i++)
				sb.Append($"\n[b]{i + 1}.[/b] {Link(results[i])} ({Strings.Duration(Lang, results[i])})");
			return sb.ToString();
		}

		case "pick":
		{
			if (FindSearch(msg) is not { } found)
				return Strings.Get(Lang, "pick-first", Prefix);
			if (!int.TryParse(arg, out var n) || n < 1 || n > found.Count)
				return Strings.Get(Lang, "pick-bad", Escape(arg));
			try
			{
				// The search only saw the list; this looks the song itself up (length, live, saved copy).
				var track = (await sources.ResolveAsync(found[n - 1].Url, who)).Track with { RequesterId = msg.InvokerUid?.Value };
				return Queue(track, now: false, msg, exempt);
			}
			catch (YouTubeException ex) { return Strings.Error(Lang, ex.Message); }
			catch (QueueFullException) { return Strings.Get(Lang, "queue-full"); }
			catch (UserLimitException ex) { return Strings.Get(Lang, "user-limit", ex.Max); }
		}

		case "seek":
		{
			if (player.Current is not { } now) return Strings.Get(Lang, "nothing-playing");
			if (now.IsLive) return Strings.Get(Lang, "cant-seek");
			if (ParseSeek(arg, (int)(player.GetState().Current?.Position ?? 0)) is not { } to) return Strings.Get(Lang, "usage-seek", Prefix);
			player.Seek(to);
			return Confirm(Strings.Get(Lang, "seeked", Strings.FormatTime(Math.Clamp(to, 0, Math.Max(0, now.Duration - 1)))));
		}

		case "back":
		{
			if (player.Back() is not { } prev) return Strings.Get(Lang, "back-none");
			var announced = Bot.Announce && msg.Target == TextMessageTargetMode.Channel;
			return announced ? null : Confirm(Strings.Get(Lang, "back", Link(prev)));
		}

		case "skipto":
		{
			var waiting = player.GetState().Queue;
			if (!int.TryParse(arg, out var n) || n < 1 || n > waiting.Count) return Strings.Get(Lang, "bad-index", Escape(arg));
			if (others && player.Current is { } cut && !Own(cut, msg)) return OthersRefusal();
			if (player.SkipTo(n - 1, waiting[n - 1].Id) is not { } to) return Strings.Get(Lang, "bad-index", Escape(arg));
			var announced = Bot.Announce && msg.Target == TextMessageTargetMode.Channel;
			return announced ? null : Confirm(Strings.Get(Lang, "now-playing", Link(to), Strings.Duration(Lang, to)));
		}

		case "move":
		{
			var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 2 || !int.TryParse(parts[0], out var from) || !int.TryParse(parts[1], out var to))
				return Strings.Get(Lang, "usage-move", Prefix);
			var queue = player.GetState().Queue;
			if (from < 1 || from > queue.Count) return Strings.Get(Lang, "bad-index", from);
			if (others && !Own(queue[from - 1], msg)) return OthersRefusal(cutsSong: false);
			to = Math.Clamp(to, 1, queue.Count);
			return player.Move(from - 1, to - 1, queue[from - 1].Id)
				? Confirm(Strings.Get(Lang, "moved", Escape(queue[from - 1].Title), to))
				: Strings.Get(Lang, "bad-index", from);
		}

		case "skip":
		{
			if (player.Current is not { } now) return Strings.Get(Lang, "nothing-playing");
			// Whoever asked for the song may always skip it.
			if (!others || Own(now, msg))
				return player.Skip(now) ? Confirm(Strings.Get(Lang, "skipped")) : Strings.Get(Lang, "nothing-playing");
			return Bot.VoteSkip ? SkipVote(c, msg, now) : OthersRefusal();
		}

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
			if (!int.TryParse(arg, out var n)) return Strings.Get(Lang, "bad-index", Escape(arg));
			var queued = player.GetState().Queue;
			if (others && n >= 1 && n <= queued.Count && !Own(queued[n - 1], msg)) return OthersRefusal(cutsSong: false);
			var removed = player.Remove(n - 1, n >= 1 && n <= queued.Count ? queued[n - 1].Id : null);
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
				"off" or "kapalı" or "kapali" or "non" or "désactivé" or "desactive" or "aus" or "0" => LoopMode.Off,
				"one" or "song" or "şarkı" or "sarki" or "un" or "morceau" or "eins" or "1" => LoopMode.One,
				"all" or "queue" or "hepsi" or "tümü" or "tout" or "file" or "alle" => LoopMode.All,
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
			var uid = msg.InvokerUid?.Value;
			var added = player.EnqueueMany(list.Songs.Select(s => s.ToTrack(who) with { RequesterId = uid }), exempt, out var limited);
			if (added == 0 && list.Songs.Count > 0)
				return limited ? Strings.Get(Lang, "user-limit", Bot.MaxSongsPerUser) : Strings.Get(Lang, "queue-full");
			if (limited) return Strings.Get(Lang, "playlist-added", added, Escape(list.Name)) + "\n" + Strings.Get(Lang, "user-limit", Bot.MaxSongsPerUser);
			return Confirm(Strings.Get(Lang, "playlist-added", added, Escape(list.Name)));
		}

		case "playlists":
			return playlists.All().Count == 0 ? Strings.Get(Lang, "playlists-none") : Strings.Get(Lang, "playlists", PlaylistNames());

		case "fav":
		{
			if (player.Current is not { } now) return Strings.Get(Lang, "nothing-playing");
			// A radio station goes to the favorite stations (!fm).
			if (Stations.FromTrack(now) is { } station)
				return favorites.Add(station) ? Confirm(Strings.Get(Lang, "fm-fav-added", Escape(station.Name), Prefix))
					: Strings.Get(Lang, "fm-fav-full", RadioFavorites.Max);
			// One shared list; it is named in the bot's chat language when it's first made.
			var name = Strings.Languages.Select(l => playlists.Find(Strings.Get(l, "favorites"))?.Name).FirstOrDefault(n => n != null)
				?? Strings.Get(Lang, "favorites");
			playlists.Save(name, [PlaylistSong.From(now)], out var left);
			if (left > 0) return Strings.Get(Lang, "playlist-full", Escape(name), Playlists.MaxSongs);
			return Confirm(Strings.Get(Lang, "fav-added", Link(now), Escape(name)));
		}

		case "create":
		{
			if (CreateBot is null || !c.Book.Clients.TryGetValue(msg.InvokerId, out var asker)) return Strings.Get(Lang, "join-failed");
			var (made, error) = await CreateBot(asker.Channel.Value);
			if (made is null) return Strings.Get(Lang, "create-full", MaxBotsHint());
			return Strings.Get(Lang, "create-done", Escape(made.Config.Bot.Nickname), made.Config.Bot.CommandPrefix);
		}

		case "sleep":
		{
			// "!sleep 30": in 30 minutes; "!sleep 02:00": at two; "!sleep off".
			if (arg.Length == 0)
				return player.SleepAt is { } at ? Strings.Get(Lang, "sleep-set", at.ToString("HH:mm")) : Strings.Get(Lang, "sleep-none", Prefix);
			if (Switch(arg) == false) { player.SetSleep(null); return Confirm(Strings.Get(Lang, "sleep-off")); }
			DateTime when;
			if (int.TryParse(arg, out var minutes) && minutes is >= 1 and <= 24 * 60) when = DateTime.Now.AddMinutes(minutes);
			else if (TimeOnly.TryParse(arg, System.Globalization.CultureInfo.InvariantCulture, out var time))
			{
				when = DateTime.Today.Add(time.ToTimeSpan());
				if (when <= DateTime.Now) when = when.AddDays(1);
			}
			else return Strings.Get(Lang, "sleep-bad", Prefix);
			player.SetSleep(when);
			return Confirm(Strings.Get(Lang, "sleep-set", when.ToString("HH:mm")));
		}

		case "myfav":
		{
			// Everyone's own list, by their unique id: !myfav saves the song playing, "play" queues the list.
			if (msg.InvokerUid?.Value is not { Length: > 0 } me) return Strings.Get(Lang, "myfav-no-id");
			var own = playlists.OfOwner(me);
			var words = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			switch (words.FirstOrDefault()?.ToLowerInvariant())
			{
			case null or "add" or "ekle" or "ajouter" or "hinzufügen":
			{
				if (player.Current is not { } now) return Strings.Get(Lang, "nothing-playing");
				if (now.IsLive) return Strings.Get(Lang, "myfav-live", Prefix);
				if (playlists.SaveOwn(me, who, [PlaylistSong.From(now)], out var left) is not { } list)
					return Strings.Get(Lang, "myfav-too-many");
				if (left > 0) return Strings.Get(Lang, "playlist-full", Escape(list.Name), Playlists.MaxSongs);
				return Confirm(Strings.Get(Lang, "myfav-added", Link(now), list.Songs.Count));
			}
			case "play" or "çal" or "cal" or "jouer" or "spielen" or "shuffle" or "karıştır" or "karistir" or "mélanger" or "melanger" or "mischen":
			{
				if (own is not { Songs.Count: > 0 }) return Strings.Get(Lang, "myfav-empty", Prefix);
				var songs = own.Songs.Select(s => s.ToTrack(who) with { RequesterId = me });
				if (words[0].ToLowerInvariant() is not ("play" or "çal" or "cal" or "jouer" or "spielen")) songs = songs.OrderBy(_ => Random.Shared.Next());
				var added = player.EnqueueMany(songs.ToList(), exempt, out var limited);
				if (added == 0) return limited ? Strings.Get(Lang, "user-limit", Bot.MaxSongsPerUser) : Strings.Get(Lang, "queue-full");
				return Confirm(Strings.Get(Lang, "playlist-added", added, Escape(own.Name)));
			}
			case "list" or "liste" or "listele":
			{
				if (own is not { Songs.Count: > 0 }) return Strings.Get(Lang, "myfav-empty", Prefix);
				var sb = new StringBuilder(Strings.Get(Lang, "myfav-list", own.Songs.Count, Prefix));
				for (int i = 0; i < own.Songs.Count && i < 20; i++) sb.Append($"\n[b]{i + 1}.[/b] {Escape(own.Songs[i].Title)}");
				return sb.ToString();
			}
			case "remove" or "sil" or "retirer" or "entfernen":
			{
				if (own is null || words.Length < 2 || !int.TryParse(words[1], out var n) || n < 1 || n > own.Songs.Count)
					return Strings.Get(Lang, "myfav-bad", Escape(words.ElementAtOrDefault(1) ?? ""));
				var song = own.Songs[n - 1];
				if (!playlists.RemoveSong(own.Name, n - 1, song.Id)) return Strings.Get(Lang, "myfav-bad", n);
				return Confirm(Strings.Get(Lang, "myfav-removed", Escape(song.Title)));
			}
			default:
				return Strings.Get(Lang, "myfav-usage", Prefix);
			}
		}

		case "eq":
		{
			var s = Sound.Of(Bot);
			var names = string.Join(", ", Sound.Equalizers.Where(e => e != "custom" || s.Bands.Any(g => g != 0)).Select(EqLabel));
			if (arg.Length == 0) return Strings.Get(Lang, "eq-now", EqLabel(s.Equalizer), names, Prefix);
			if (Sound.EqualizerName(arg) is not { } eq) return Strings.Get(Lang, "eq-unknown", Escape(arg), names);
			if (eq == "custom" && s.Bands.All(g => g == 0)) return Strings.Get(Lang, "eq-custom-empty");
			player.UpdateSound(x => x with { Equalizer = eq });
			return Confirm(eq == "off" ? Strings.Get(Lang, "eq-off") : Strings.Get(Lang, "eq-set", EqLabel(eq)));
		}

		case "fx":
		{
			var s = Sound.Of(Bot);
			if (arg.Length == 0) return Strings.Get(Lang, "fx-now", EffectsNow(s), Prefix);
			switch (Sound.EffectName(arg))
			{
			case "off":
				player.UpdateSound(x => x with { Effect = "off", EightD = false, Speed = 1 });
				return Confirm(Strings.Get(Lang, "fx-off"));
			case "8d":
				var turned = player.UpdateSound(x => x with { EightD = !x.EightD });
				return Confirm(Strings.Get(Lang, turned.EightD ? "fx-8d-on" : "fx-8d-off"));
			case { } effect:
				// Again turns it off.
				var on = player.UpdateSound(x => x with { Effect = x.Effect == effect ? "off" : effect }).Effect == effect;
				return Confirm(on ? Strings.Get(Lang, "fx-on", Strings.Get(Lang, "fx-" + effect)) : Strings.Get(Lang, "fx-effect-off", Strings.Get(Lang, "fx-" + effect)));
			default:
				return Strings.Get(Lang, "fx-unknown", Escape(arg), Prefix);
			}
		}

		case "speed":
		{
			var s = Sound.Of(Bot);
			if (arg.Length == 0) return Strings.Get(Lang, "speed-now", FormatSpeed(s.Speed), Prefix);
			if (ParseSpeed(arg) is not { } speed) return Strings.Get(Lang, "speed-bad", Prefix);
			player.UpdateSound(x => x with { Speed = speed });
			return Confirm(Strings.Get(Lang, "speed-set", FormatSpeed(speed)));
		}

		case "autoplay":
		{
			var on = Switch(arg) ?? !player.Autoplay;
			player.SetAutoplay(on);
			return Confirm(Strings.Get(Lang, on ? "autoplay-on" : "autoplay-off"));
		}

		case "fm":
		{
			if (!config.Sources.Radio) return Strings.Error(Lang, "source-off");
			var saved = favorites.All();
			if (arg.Length == 0)
			{
				if (saved.Count == 0) return Strings.Get(Lang, "fm-none", Prefix);
				var sb = new StringBuilder(Strings.Get(Lang, "fm-list", Prefix));
				for (int i = 0; i < saved.Count && i < 30; i++) sb.Append($"\n[b]{i + 1}.[/b] {Escape(saved[i].Name)}");
				return sb.ToString();
			}
			var station = int.TryParse(arg, out var n) && n >= 1 && n <= saved.Count ? saved[n - 1] : favorites.Find(arg);
			if (station is null)
			{
				try { station = (await radios.SearchAsync(arg, null, null, 1, CancellationToken.None)).FirstOrDefault(); }
				catch (RadioException) { return Strings.Get(Lang, "fm-down"); }
				if (station is null) return Strings.Get(Lang, "fm-not-found", Escape(arg));
			}
			radios.Played(station);
			try { return Queue(station.ToTrack(who) with { RequesterId = msg.InvokerUid?.Value }, now: false, msg, exempt); }
			catch (QueueFullException) { return Strings.Get(Lang, "queue-full"); }
			catch (UserLimitException ex) { return Strings.Get(Lang, "user-limit", ex.Max); }
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
			NoteMove(r);
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
		["play"] = "play", ["p"] = "play", ["çal"] = "play", ["cal"] = "play", ["oynat"] = "play", ["jouer"] = "play", ["joue"] = "play", ["spielen"] = "play", ["spiel"] = "play",
		["playnow"] = "playnow", ["şimdiçal"] = "playnow", ["simdical"] = "playnow", ["jouermaintenant"] = "playnow", ["jetztspielen"] = "playnow",
		["skip"] = "skip", ["voteskip"] = "skip", ["vs"] = "skip", ["oyla"] = "skip", ["voter"] = "skip", ["abstimmen"] = "skip", ["next"] = "skip", ["s"] = "skip", ["geç"] = "skip", ["gec"] = "skip", ["atla"] = "skip", ["passer"] = "skip", ["suivant"] = "skip", ["überspringen"] = "skip", ["ueberspringen"] = "skip", ["nächster"] = "skip", ["naechster"] = "skip",
		["stop"] = "stop", ["dur"] = "stop", ["arrêt"] = "stop", ["arret"] = "stop", ["arrêter"] = "stop", ["arreter"] = "stop", ["stopp"] = "stop",
		["pause"] = "pause", ["duraklat"] = "pause",
		["resume"] = "resume", ["unpause"] = "resume", ["devam"] = "resume", ["reprendre"] = "resume", ["fortsetzen"] = "resume",
		["queue"] = "queue", ["q"] = "queue", ["list"] = "queue", ["sıra"] = "queue", ["sira"] = "queue", ["liste"] = "queue", ["file"] = "queue", ["warteschlange"] = "queue",
		["np"] = "np", ["now"] = "np", ["nowplaying"] = "np", ["şimdi"] = "np", ["simdi"] = "np", ["çalan"] = "np", ["encours"] = "np", ["läuft"] = "np", ["laeuft"] = "np",
		["vol"] = "vol", ["volume"] = "vol", ["v"] = "vol", ["ses"] = "vol", ["lautstärke"] = "vol", ["lautstaerke"] = "vol",
		["remove"] = "remove", ["rm"] = "remove", ["sil"] = "remove", ["retirer"] = "remove", ["supprimer"] = "remove", ["entfernen"] = "remove",
		["clear"] = "clear", ["temizle"] = "clear", ["vider"] = "clear", ["leeren"] = "clear",
		["shuffle"] = "shuffle", ["karıştır"] = "shuffle", ["karistir"] = "shuffle", ["mélanger"] = "shuffle", ["melanger"] = "shuffle", ["mischen"] = "shuffle",
		["loop"] = "loop", ["repeat"] = "loop", ["tekrar"] = "loop", ["boucle"] = "loop", ["répéter"] = "loop", ["repeter"] = "loop", ["wiederholen"] = "loop",
		["join"] = "join", ["come"] = "join", ["gel"] = "join", ["viens"] = "join", ["rejoindre"] = "join", ["komm"] = "join",
		["autoplay"] = "autoplay", ["auto"] = "autoplay", ["otomatik"] = "autoplay", ["otodevam"] = "autoplay", ["lectureauto"] = "autoplay", ["automatisch"] = "autoplay",
		["fm"] = "fm", ["station"] = "fm", ["istasyon"] = "fm", ["sender"] = "fm",
		["eq"] = "eq", ["equalizer"] = "eq", ["equaliser"] = "eq", ["ekolayzer"] = "eq", ["ekolayzır"] = "eq", ["égaliseur"] = "eq", ["egaliseur"] = "eq",
		["fx"] = "fx", ["effect"] = "fx", ["effects"] = "fx", ["efekt"] = "fx", ["efektler"] = "fx", ["effet"] = "fx", ["effets"] = "fx", ["effekt"] = "fx", ["effekte"] = "fx",
		["speed"] = "speed", ["hız"] = "speed", ["hiz"] = "speed", ["vitesse"] = "speed", ["tempo"] = "speed", ["geschwindigkeit"] = "speed",
		// Up to 0.5 the name of autoplay; now it leads to autoplay or !fm (see HandleMessageAsync).
		["radio"] = "radio", ["radyo"] = "radio",
		["follow"] = "follow", ["followme"] = "follow", ["takip"] = "follow", ["takipet"] = "follow", ["suivre"] = "follow", ["folgen"] = "follow", ["folgmir"] = "follow",
		["playlist"] = "playlist", ["pl"] = "playlist", ["çalmalistesi"] = "playlist", ["calmalistesi"] = "playlist",
		["playlists"] = "playlists", ["lists"] = "playlists", ["listeler"] = "playlists",
		["fav"] = "fav", ["favorite"] = "fav", ["favori"] = "fav",
		["create"] = "create", ["newbot"] = "create", ["oluştur"] = "create", ["olustur"] = "create", ["yenibot"] = "create", ["créer"] = "create", ["creer"] = "create", ["erstellen"] = "create",
		["sleep"] = "sleep", ["uyku"] = "sleep", ["dormir"] = "sleep", ["schlafen"] = "sleep",
		["myfav"] = "myfav", ["myfavs"] = "myfav", ["mfav"] = "myfav", ["favorim"] = "myfav", ["favorilerim"] = "myfav", ["mesfavoris"] = "myfav", ["mesfavs"] = "myfav", ["meinefavs"] = "myfav", ["meinefavoriten"] = "myfav",
		["search"] = "search", ["find"] = "search", ["ara"] = "search", ["bul"] = "search", ["chercher"] = "search", ["recherche"] = "search", ["suchen"] = "search",
		["pick"] = "pick", ["seç"] = "pick", ["sec"] = "pick", ["choisir"] = "pick", ["wählen"] = "pick", ["waehlen"] = "pick",
		["seek"] = "seek", ["jump"] = "seek", ["sar"] = "seek", ["aller"] = "seek", ["spulen"] = "seek",
		["back"] = "back", ["prev"] = "back", ["previous"] = "back", ["önceki"] = "back", ["onceki"] = "back", ["geri"] = "back",
		["précédent"] = "back", ["precedent"] = "back", ["zurück"] = "back", ["zurueck"] = "back",
		["skipto"] = "skipto", ["jumpto"] = "skipto", ["git"] = "skipto", ["allerà"] = "skipto", ["allera"] = "skipto", ["springen"] = "skipto",
		["move"] = "move", ["mv"] = "move", ["taşı"] = "move", ["tasi"] = "move", ["déplacer"] = "move", ["deplacer"] = "move", ["verschieben"] = "move",
		["help"] = "help", ["h"] = "help", ["yardım"] = "help", ["yardim"] = "help", ["komutlar"] = "help", ["aide"] = "help", ["hilfe"] = "help",
	};

	public static string? Find(string name) => Aliases.GetValueOrDefault(name);
}
