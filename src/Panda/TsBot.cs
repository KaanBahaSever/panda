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
	int Listeners);

/// <summary>
/// Keeps the bot connected to TeamSpeak (reconnecting when needed) and handles chat commands.
/// Every TSLib call runs on the client's own scheduler thread.
/// </summary>
public sealed class TsBot : BackgroundService
{
	readonly PandaConfig config;
	readonly Player player;
	readonly YouTube youtube;
	readonly ILogger<TsBot> log;
	readonly string identityPath;
	readonly DedicatedTaskScheduler scheduler = new(Id.Null);

	TsFullClient? client;
	IdentityData? identity;
	string state = "connecting";
	string? lastError;
	TaskCompletionSource reconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public event Action? StatusChanged;

	string Lang => config.Bot.Language;
	string Prefix => config.Bot.CommandPrefix;

	public TsBot(PandaConfig config, Player player, YouTube youtube, DataPaths paths, ILogger<TsBot> log)
	{
		this.config = config;
		this.player = player;
		this.youtube = youtube;
		this.log = log;
		identityPath = Path.Combine(paths.Data, "identity.json");

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

	public Task<BotStatus> GetStatusAsync() => scheduler.Invoke(() =>
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
		return new BotStatus(state, lastError, book?.Server?.Name, channelName, self?.Name ?? config.Bot.Nickname, listeners);
	});

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		identity = LoadOrCreateIdentity();
		var delay = TimeSpan.FromSeconds(3);
		while (!ct.IsCancellationRequested)
		{
			var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			reconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
			SetState("connecting", null);

			var error = await scheduler.InvokeAsync(() => ConnectAsync(disconnected));
			if (error is null)
			{
				delay = TimeSpan.FromSeconds(3);
				SetState("connected", null);
				var stopped = Task.Delay(Timeout.Infinite, ct);
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
			else
			{
				log.LogWarning("Could not connect to {Address}: {Error}", config.Server.Address, error);
				SetState("disconnected", error);
			}

			// After a crash the old connection lingers on the server for a bit; back off and retry.
			try { await Task.WhenAny(Task.Delay(delay, ct), reconnect.Task); }
			catch (OperationCanceledException) { break; }
			delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
		}
	}

	async Task<string?> ConnectAsync(TaskCompletionSource<string> disconnected)
	{
		client?.Dispose();
		var c = new TsFullClient(scheduler);
		client = c;
		c.OnDisconnected += (_, e) => disconnected.TrySetResult(e.Error?.ErrorFormat() ?? e.ExitReason.ToString());
		c.OnEachTextMessage += (_, msg) => _ = HandleMessageAsync(c, msg);
		c.OnEachClientMoved += (_, _) => StatusChanged?.Invoke();
		c.OnEachClientEnterView += (_, _) => StatusChanged?.Invoke();
		c.OnEachClientLeftView += (_, _) => StatusChanged?.Invoke();

		var s = config.Server;
		var data = new ConnectionDataFull(
			s.Address, identity!,
			username: config.Bot.Nickname,
			serverPassword: s.Password.Length > 0 ? Password.FromPlain(s.Password) : (Password?)null,
			defaultChannel: s.Channel,
			defaultChannelPassword: s.ChannelPassword.Length > 0 ? Password.FromPlain(s.ChannelPassword) : (Password?)null);

		log.LogInformation("Connecting to {Address} as {Nickname}", s.Address, config.Bot.Nickname);
		var result = await c.Connect(data);
		if (!result.Ok)
			return result.Error.ErrorFormat();

		log.LogInformation("Connected to {Server}", c.Book.Server?.Name);
		player.AttachOutput(c);
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
		if (!config.Bot.ShowSongInDescription || client is not { Connected: true } c) return;
		if (text.Length > 80) text = text[..79] + "…";
		if (c.Book.Self()?.Description == text) return;
		await c.ChangeDescription(text);
	}

	async Task OnTrackStarted(Track t)
	{
		if (client is not { Connected: true } c) return;
		await SetDescription("♪ " + t.Title);
		if (config.Bot.Announce)
			await c.SendChannelMessage(Strings.Get(Lang, "now-playing-by", Link(t), Strings.Duration(Lang, t), t.RequestedBy));
	}

	async Task OnTrackFailed(Track t, string reason)
	{
		if (client is not { Connected: true } c) return;
		await c.SendChannelMessage(Strings.Get(Lang, "play-failed", t.Title, Strings.ShortError(Lang, reason)));
	}

	static string Link(Track t) => $"[url={t.Url}]{Escape(t.Title)}[/url]";
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
		var users = config.Bot.AllowedUsers;
		var groups = config.Bot.AllowedServerGroups;
		if (users.Count == 0 && groups.Count == 0) return true;
		if (msg.InvokerUid is { } uid && users.Contains(uid.Value)) return true;
		if (c.Book.Clients.TryGetValue(msg.InvokerId, out var who))
			return who.ServerGroups.Any(g => groups.Contains(g.Value));
		return false;
	}

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
				var track = await youtube.ResolveAsync(arg, who);
				if (command == "playnow")
				{
					player.PlayNow(track);
					return null;
				}
				var pos = player.Enqueue(track);
				// Position 0 started playing; the "now playing" announcement covers it.
				return pos == 0 && config.Bot.Announce ? null
					: pos == 0 ? Strings.Get(Lang, "now-playing", Link(track), Strings.Duration(Lang, track))
					: Strings.Get(Lang, "added", Link(track), pos, Strings.Duration(Lang, track));
			}
			catch (YouTubeException ex) { return Strings.Error(Lang, ex.Message); }
			catch (QueueFullException) { return Strings.Get(Lang, "queue-full"); }

		case "skip":
			return player.Skip() ? Strings.Get(Lang, "skipped") : Strings.Get(Lang, "nothing-playing");

		case "stop":
			player.Stop();
			return Strings.Get(Lang, "stopped");

		case "pause":
			return player.Pause() ? Strings.Get(Lang, "paused") : Strings.Get(Lang, "nothing-playing");

		case "resume":
			return player.Resume() ? Strings.Get(Lang, "resumed") : Strings.Get(Lang, "nothing-playing");

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
			return Strings.Get(Lang, "volume-set", player.Volume);

		case "remove":
		{
			if (!int.TryParse(arg, out var n)) return Strings.Get(Lang, "bad-index", arg);
			var removed = player.Remove(n - 1);
			return removed is null ? Strings.Get(Lang, "bad-index", n) : Strings.Get(Lang, "removed", Escape(removed.Title));
		}

		case "clear":
			player.ClearQueue();
			return Strings.Get(Lang, "cleared");

		case "shuffle":
			player.Shuffle();
			return Strings.Get(Lang, "shuffled");

		case "loop":
		{
			var mode = arg.ToLowerInvariant() switch
			{
				"off" or "kapalı" or "kapali" or "0" => LoopMode.Off,
				"one" or "song" or "şarkı" or "sarki" or "1" => LoopMode.One,
				"all" or "queue" or "hepsi" or "tümü" => LoopMode.All,
				_ => player.Loop switch { LoopMode.Off => LoopMode.All, LoopMode.All => LoopMode.One, _ => LoopMode.Off },
			};
			player.SetLoop(mode);
			return Strings.Get(Lang, mode switch { LoopMode.One => "loop-one", LoopMode.All => "loop-all", _ => "loop-off" });
		}

		case "join":
		{
			if (!c.Book.Clients.TryGetValue(msg.InvokerId, out var invoker)) return Strings.Get(Lang, "join-failed");
			var r = await c.ClientMove(c.ClientId, invoker.Channel);
			StatusChanged?.Invoke();
			return r.Ok ? Strings.Get(Lang, "joined") : Strings.Get(Lang, "join-failed");
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
		PandaConfig.WritePrivate(identityPath, JsonSerializer.Serialize(new SavedIdentity(id.PrivateKeyString, id.ValidKeyOffset)));
		return id;
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
		["play"] = "play", ["p"] = "play", ["çal"] = "play", ["cal"] = "play", ["oynat"] = "play",
		["playnow"] = "playnow", ["şimdiçal"] = "playnow", ["simdical"] = "playnow",
		["skip"] = "skip", ["next"] = "skip", ["s"] = "skip", ["geç"] = "skip", ["gec"] = "skip", ["atla"] = "skip",
		["stop"] = "stop", ["dur"] = "stop",
		["pause"] = "pause", ["duraklat"] = "pause",
		["resume"] = "resume", ["unpause"] = "resume", ["devam"] = "resume",
		["queue"] = "queue", ["q"] = "queue", ["list"] = "queue", ["sıra"] = "queue", ["sira"] = "queue", ["liste"] = "queue",
		["np"] = "np", ["now"] = "np", ["nowplaying"] = "np", ["şimdi"] = "np", ["simdi"] = "np", ["çalan"] = "np",
		["vol"] = "vol", ["volume"] = "vol", ["v"] = "vol", ["ses"] = "vol",
		["remove"] = "remove", ["rm"] = "remove", ["sil"] = "remove",
		["clear"] = "clear", ["temizle"] = "clear",
		["shuffle"] = "shuffle", ["karıştır"] = "shuffle", ["karistir"] = "shuffle",
		["loop"] = "loop", ["repeat"] = "loop", ["tekrar"] = "loop",
		["join"] = "join", ["come"] = "join", ["gel"] = "join",
		["help"] = "help", ["h"] = "help", ["yardım"] = "help", ["yardim"] = "help", ["komutlar"] = "help",
	};

	public static string? Find(string name) => Aliases.GetValueOrDefault(name);
}
