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
	/// <summary>Nothing said or typed for that long (when the server tells). Off by default: people who only
	/// listen to the music say nothing either (and in the bot's channel while it plays they never count).</summary>
	public bool Idle { get; set; }
	/// <summary>Set as away in TeamSpeak.</summary>
	public bool Away { get; set; } = true;
	/// <summary>Speakers (sound) turned off.</summary>
	public bool Muted { get; set; } = true;
	/// <summary>Back to the channel they were in when they come back.</summary>
	public bool MoveBack { get; set; } = true;
	/// <summary>Server groups never moved (admins, other music bots).</summary>
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
	// Kept by the person's identity, not their connection's number: TeamSpeak gives a freed number to the next one.
	sealed class AfkPerson
	{
		public DateTime? AwaySince;          // seen away or muted since
		public ChannelId? MovedFrom;         // moved by Panda to the AFK channel, from there
		public DateTime MovedAt;
		public bool MovedForIdle;
		public ChannelId LastChannel;
		public DateTime AskedAt = DateTime.MinValue;    // idle time last asked for
		public DateTime FailedUntil = DateTime.MinValue; // a move the server refused: not tried again for a while
	}

	readonly Dictionary<string, AfkPerson> afkPeople = [];
	ChannelId? afkChannelId;
	string? afkChannelSpec;
	bool afkChannelWarned, afkMoveWarned, afkMovedOnce, afkSubscribeWarned;
	DateTime afkSubscribedAt = DateTime.MinValue;   // to every channel (again every few minutes: new channels)
	DateTime afkLookedAt = DateTime.MinValue;
	int afkRunning;

	/// <summary>Called by the scheduler every few seconds; looks at the server every 10 s at most, one look at a time.</summary>
	public void LookForAfk()
	{
		if (disposed || DateTime.UtcNow - afkLookedAt < TimeSpan.FromSeconds(9)) return;
		if (Interlocked.Exchange(ref afkRunning, 1) == 1) return;
		afkLookedAt = DateTime.UtcNow;
		try
		{
			_ = scheduler.InvokeAsync(async () =>
			{
				try { await LookForAfkAsync(); }
				catch (Exception ex) { log.LogWarning("AFK move failed: {Error}", ex.Message); }
				finally { Volatile.Write(ref afkRunning, 0); }
			});
		}
		catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or TaskSchedulerException)
		{
			Volatile.Write(ref afkRunning, 0);   // the bot is going away
		}
	}

	/// <summary>The channel people are moved to (the bot doesn't follow anyone there).</summary>
	bool IsAfkChannel(ChannelId channel) => Bot.Afk.Enabled && afkChannelId == channel;

	async Task LookForAfkAsync()
	{
		var a = Bot.Afk;
		if (!a.Enabled || client is not { Connected: true } c || c.Book.Self() is not { } self)
		{
			afkPeople.Clear();
			return;
		}
		var now = DateTime.UtcNow;
		// A client only sees the people in channels it subscribed to (TeamSpeak's own client subscribes to all):
		// without it the people elsewhere, and those moved to the AFK channel, would be out of sight.
		if (now - afkSubscribedAt > TimeSpan.FromMinutes(5))
		{
			afkSubscribedAt = now;
			var sub = await c.SendVoid(new TSLib.Commands.TsCommand("channelsubscribeall"));
			if (!sub.Ok && !afkSubscribeWarned)
			{
				afkSubscribeWarned = true;
				log.LogWarning("AFK: could not subscribe to every channel ({Error}); only the people in the bot's channels are seen", sub.Error.ErrorFormat());
			}
		}
		var spec = a.Channel;
		var afk = afkChannelSpec == spec && afkChannelId is { } known && c.Book.Channels.ContainsKey(known) ? known : FindChannel(c, spec);
		afkChannelSpec = spec;
		afkChannelId = afk;
		if (afk is null)
		{
			if (!afkChannelWarned) log.LogWarning("The AFK channel ({Channel}) isn't on the server", spec);
			afkChannelWarned = true;
			return;
		}
		// Found by its name: kept by its id from now on (a renamed channel stays the AFK one). Not when new
		// settings came meanwhile.
		if (!spec.StartsWith('/') && ReferenceEquals(Bot.Afk, a))
		{
			a.Channel = "/" + afk.Value.Value;
			afkChannelSpec = a.Channel;
			config.Save();
		}
		var wait = TimeSpan.FromMinutes(a.Minutes);
		// Idle counts from a minute at least: everyone is "idle" for a few seconds now and then.
		var idleWait = wait < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : wait;
		ChannelId? playingHere = player.Current != null ? self.Channel : null;
		var people = c.Book.Clients.Values
			.Where(x => x.Id != self.Id && x.ClientType == ClientType.Full && x.Uid is { } u && !PandaUids.ContainsKey(u.Value))
			.ToList();
		var here = people.Select(x => x.Uid!.Value.Value).ToHashSet();
		foreach (var gone in afkPeople.Keys.Where(k => !here.Contains(k)).ToList()) afkPeople.Remove(gone);

		var asks = new List<(TSLib.Full.Book.Client Who, AfkPerson State)>();
		foreach (var p in people)
		{
			var uid = p.Uid!.Value.Value;
			if (p.ServerGroups.Any(g => a.ExemptGroups.Contains(g.Value)))
			{
				afkPeople.Remove(uid);
				continue;
			}
			if (!afkPeople.TryGetValue(uid, out var st)) afkPeople[uid] = st = new AfkPerson { LastChannel = p.Channel };
			var awayNow = (a.Away && p.AwayMessage != null) || (a.Muted && (p.OutputMuted || !p.OutputHardwareEnabled));
			// Out of the AFK channel while still away (an admin pulled them out, or they went themselves):
			// their time counts again from now, and Panda no longer owes them a way back.
			if (st.LastChannel == afk.Value && p.Channel != afk.Value)
			{
				st.MovedFrom = null;
				if (awayNow) st.AwaySince = now;
			}
			st.LastChannel = p.Channel;
			if (!awayNow) st.AwaySince = null;
			else st.AwaySince ??= now;
			// Listening to the music says nothing either: idle doesn't count in the bot's channel while it plays.
			var idle = a.Idle && p.Channel != playingHere ? p.ConnectionData?.IdleTime : null;

			if (p.Channel == afk.Value)
			{
				if (st.MovedFrom is not { } from) continue;   // went there themselves: left alone
				// Back when they're no longer away, and (moved for being idle) active since the move, by an
				// answer asked for after it.
				var active = !st.MovedForIdle || (st.AskedAt > st.MovedAt.AddSeconds(2) && p.ConnectionData?.IdleTime is { } i0 && i0 < now - st.MovedAt);
				if (a.MoveBack && !awayNow && active)
				{
					st.MovedFrom = null;
					if (c.Book.Channels.ContainsKey(from))
					{
						var back = await c.ClientMove(p.Id, from);
						if (back.Ok) log.LogInformation("AFK: {Name} is back, moved to their channel again", p.Name);
						else log.LogInformation("AFK: could not move {Name} back: {Error}", p.Name, back.Error.ErrorFormat());
					}
				}
				else if (a.MoveBack && st.MovedForIdle && !awayNow) asks.Add((p, st));
				continue;
			}
			if (now < st.FailedUntil) continue;
			var awayDue = st.AwaySince is { } since && now - since >= wait;
			var idleDue = idle is { } i && i >= idleWait;
			if (awayDue || idleDue)
			{
				// Where they are now: once moved, the client already says the AFK channel.
				var from = p.Channel;
				var r = await c.ClientMove(p.Id, afk.Value);
				if (r.Ok)
				{
					st.MovedFrom = from;
					st.MovedAt = now;
					st.MovedForIdle = !awayDue;
					st.LastChannel = afk.Value;
					afkMovedOnce = true;
					Note("afk", r, "i_client_move_power");
					log.LogInformation("AFK: moved {Name} to the AFK channel", p.Name);
				}
				else
				{
					st.FailedUntil = now + TimeSpan.FromMinutes(5);
					// One person the bot can't move (an admin above it) isn't a missing permission.
					if (!afkMovedOnce) Note("afk", r, "i_client_move_power");
					if (!afkMoveWarned)
					{
						afkMoveWarned = true;
						log.LogWarning("AFK: could not move {Name}: {Error}. The bot needs i_client_move_power on the server (as much as the people it moves need)", p.Name, r.Error.ErrorFormat());
					}
				}
			}
			// How long they've been idle: asked only when it's unknown or could be due within the next minute.
			else if (a.Idle && p.Channel != playingHere
				&& (idle is null || idle.Value + (now - st.AskedAt) >= idleWait - TimeSpan.FromMinutes(1)))
				asks.Add((p, st));
		}
		// The server answers each ask by updating the person (the next look sees it); a few at a time, each at
		// most once a minute, the longest waiting first.
		foreach (var (who, st) in asks.Where(x => now - x.State.AskedAt >= TimeSpan.FromMinutes(1)).OrderBy(x => x.State.AskedAt).Take(10))
		{
			st.AskedAt = now;
			await c.SendVoid(new TSLib.Commands.TsCommand("getconnectioninfo") { { "clid", who.Id } });
		}
	}

	/// <summary>New AFK settings: the channel is looked for again, warnings may come again.</summary>
	void AfkSettingsChanged()
	{
		afkChannelId = null;
		afkChannelWarned = afkMoveWarned = false;
		foreach (var st in afkPeople.Values) st.FailedUntil = DateTime.MinValue;   // maybe it may now
	}
}
