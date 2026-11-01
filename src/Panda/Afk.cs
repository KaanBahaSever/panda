using TSLib;
using TSLib.Full;

namespace Panda;

/// <summary>Moving people who are away to an AFK channel (a server tool: off unless turned on).</summary>
public sealed class AfkSettings
{
	public bool Enabled { get; set; }
	/// <summary>The AFK channel: a path ("Lobby/AFK") or "/12"; kept by id once found.</summary>
	public string Channel { get; set; } = "";
	/// <summary>How long someone is away (or idle) before being moved; 0 = at the next look.</summary>
	public int Minutes { get; set; } = 15;
	/// <summary>Nothing said or typed for that long (when the server tells).</summary>
	public bool Idle { get; set; } = true;
	/// <summary>Set as away in TeamSpeak.</summary>
	public bool Away { get; set; } = true;
	/// <summary>Speakers (sound) turned off.</summary>
	public bool Muted { get; set; } = true;
	/// <summary>Back to the channel they were in when they come back.</summary>
	public bool MoveBack { get; set; } = true;
	/// <summary>Server groups never moved (admins, say).</summary>
	public List<ulong> ExemptGroups { get; set; } = [];

	public void Clean()
	{
		Channel = (Channel ?? "").Trim();
		if (Channel.Length > 200) Channel = Channel[..200];
		Minutes = Math.Clamp(Minutes, 0, 24 * 60);
		ExemptGroups = (ExemptGroups ?? []).Distinct().Take(100).ToList();
	}
}

public sealed partial class TsBot
{
	readonly Dictionary<ClientId, DateTime> afkSince = [];          // seen away or muted since
	readonly Dictionary<ClientId, ChannelId> afkMovedFrom = [];     // moved by Panda, from where
	ChannelId? afkChannelId;
	string? afkChannelSpec;
	bool afkWarned;
	bool afkSubscribed;   // to every channel, on this connection
	DateTime afkLookedAt = DateTime.MinValue;

	/// <summary>Called by the scheduler every few seconds; looks at the server every 10 s at most.</summary>
	public void LookForAfk()
	{
		if (DateTime.UtcNow - afkLookedAt < TimeSpan.FromSeconds(9)) return;
		afkLookedAt = DateTime.UtcNow;
		_ = scheduler.InvokeAsync(async () =>
		{
			try { await LookForAfkAsync(); }
			catch (Exception ex) { log.LogWarning("AFK move failed: {Error}", ex.Message); }
		});
	}

	async Task LookForAfkAsync()
	{
		var a = Bot.Afk;
		if (!a.Enabled || client is not { Connected: true } c || c.Book.Self() is not { } self)
		{
			afkSince.Clear();
			afkMovedFrom.Clear();
			return;
		}
		// A client only sees the people in channels it subscribed to (TeamSpeak's own client subscribes to all):
		// without it the people elsewhere, and those moved to the AFK channel, would be out of sight.
		if (!afkSubscribed)
		{
			var sub = await c.SendVoid(new TSLib.Commands.TsCommand("channelsubscribeall"));
			if (sub.Ok) afkSubscribed = true;
			else log.LogInformation("Could not subscribe to every channel: {Error}", sub.Error.ErrorFormat());
			return;   // the people come in over the next moments
		}
		var spec = a.Channel;
		var afk = afkChannelSpec == spec && afkChannelId is { } known && c.Book.Channels.ContainsKey(known) ? known : FindChannel(c, spec);
		afkChannelSpec = spec;
		afkChannelId = afk;
		if (afk is null)
		{
			if (!afkWarned) log.LogWarning("The AFK channel ({Channel}) isn't on the server", spec);
			afkWarned = true;
			return;
		}
		// Found by its name: kept by its id from now on (a renamed channel stays the AFK one).
		if (!spec.StartsWith('/'))
		{
			Bot.Afk.Channel = "/" + afk.Value.Value;
			afkChannelSpec = Bot.Afk.Channel;
			config.Save();
		}
		var now = DateTime.UtcNow;
		var wait = TimeSpan.FromMinutes(a.Minutes);
		var people = c.Book.Clients.Values
			.Where(x => x.Id != self.Id && x.ClientType == ClientType.Full && !(x.Uid is { } u && PandaUids.ContainsKey(u.Value)))
			.ToList();
		var here = people.Select(x => x.Id).ToHashSet();
		foreach (var gone in afkSince.Keys.Where(id => !here.Contains(id)).ToList()) afkSince.Remove(gone);
		foreach (var gone in afkMovedFrom.Keys.Where(id => !here.Contains(id)).ToList()) afkMovedFrom.Remove(gone);

		var asked = 0;
		foreach (var p in people)
		{
			if (p.ServerGroups.Any(g => a.ExemptGroups.Contains(g.Value)))
			{
				afkSince.Remove(p.Id);
				continue;
			}
			var awayNow = (a.Away && p.AwayMessage != null) || (a.Muted && (p.OutputMuted || !p.OutputHardwareEnabled));
			if (awayNow) afkSince.TryAdd(p.Id, now);
			else afkSince.Remove(p.Id);
			var idle = a.Idle ? p.ConnectionData?.IdleTime : null;
			if (p.Channel == afk.Value)
			{
				// Moved by Panda and back now (not away, and active again when the server tells): to where they were.
				if (a.MoveBack && !awayNow && afkMovedFrom.TryGetValue(p.Id, out var from) && (idle is null || idle < TimeSpan.FromSeconds(30)))
				{
					afkMovedFrom.Remove(p.Id);
					if (c.Book.Channels.ContainsKey(from))
					{
						var back = await c.ClientMove(p.Id, from);
						Note("afk", back, "i_client_move_power");
						if (back.Ok) log.LogInformation("AFK: {Name} is back, moved to their channel again", p.Name);
						else log.LogWarning("AFK: could not move {Name} back: {Error}", p.Name, back.Error.ErrorFormat());
					}
				}
				else if (a.Idle && afkMovedFrom.ContainsKey(p.Id) && asked++ < 30)
					await AskIdle(c, p.Id);
				continue;
			}
			var due = (awayNow && now - afkSince[p.Id] >= wait) || (idle is { } i && i >= wait && (wait > TimeSpan.Zero || i >= TimeSpan.FromSeconds(30)));
			if (due)
			{
				// Where they are now: once moved, the client already says the AFK channel.
				var from = p.Channel;
				var r = await c.ClientMove(p.Id, afk.Value);
				Note("afk", r, "i_client_move_power");
				if (r.Ok)
				{
					afkMovedFrom[p.Id] = from;
					log.LogInformation("AFK: moved {Name} to the AFK channel", p.Name);
				}
				else if (!afkWarned)
				{
					afkWarned = true;
					log.LogWarning("Could not move {Name} to the AFK channel: {Error}. On the TeamSpeak server, give the bot's group i_client_move_power", p.Name, r.Error.ErrorFormat());
				}
			}
			// How long they've been idle: the server says it when asked (the answer updates the client).
			else if (a.Idle && asked++ < 30)
				await AskIdle(c, p.Id);
		}
	}

	static Task AskIdle(TsFullClient c, ClientId id) =>
		c.SendVoid(new TSLib.Commands.TsCommand("getconnectioninfo") { { "clid", id } });

	/// <summary>New AFK settings: the channel is looked for again, warnings may come again.</summary>
	void AfkSettingsChanged()
	{
		afkChannelId = null;
		afkWarned = false;
	}
}
