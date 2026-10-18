namespace Panda;

/// <summary>Something a bot does at a time: stop, or play a saved list or a radio station.</summary>
/// <param name="Time">"HH:mm", the server's local time.</param>
/// <param name="Days">0 = Sunday … 6 = Saturday; empty = every day.</param>
/// <param name="Action">"stop", "playlist" or "station".</param>
/// <param name="Target">The list's name, or the station's id (Radio Browser or a favorite).</param>
public sealed record ScheduleEntry(string Time, List<int> Days, string Action, string Target = "", bool Shuffle = false, bool Enabled = true)
{
	public static readonly string[] Actions = ["stop", "playlist", "station"];

	public bool IsValid =>
		TimeOnly.TryParseExact(Time, "HH:mm", out _) && Actions.Contains(Action) && (Days ?? []).All(d => d is >= 0 and <= 6)
		&& (Action == "stop" || Target is { Length: > 0 and <= 100 });
}

/// <summary>
/// Runs the bots' schedules (Settings → Schedule) and the sleep timers (!sleep): looks every few seconds
/// whether a time has come. Times are the server's local time.
/// </summary>
public sealed class Scheduler(BotManager bots, Playlists playlists, RadioBrowser radios, RadioFavorites favorites, ILogger<Scheduler> log) : BackgroundService
{
	// What already ran today (bot, entry), so a minute that is looked at twice runs once.
	readonly HashSet<string> done = [];
	DateOnly today;
	DateTime lastTick = DateTime.Now;
	// A tick that came late (a pause, a slow radio lookup, the clock jumping) still runs what it passed; not older.
	static readonly TimeSpan CatchUp = TimeSpan.FromMinutes(5);

	protected override async Task ExecuteAsync(CancellationToken stop)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
		do
		{
			try { await TickAsync(DateTime.Now, stop); }
			catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Schedule failed"); }
		}
		while (await timer.WaitForNextTickAsync(stop));
	}

	async Task TickAsync(DateTime now, CancellationToken ct)
	{
		var date = DateOnly.FromDateTime(now);
		if (date != today) { today = date; done.RemoveWhere(k => !k.StartsWith(date.ToString("yyyy-MM-dd"))); }
		var from = now - lastTick > CatchUp || lastTick > now ? now.AddSeconds(-11) : lastTick;
		lastTick = now;
		foreach (var b in bots.All)
		{
			if (b.Player.TryFireSleep(now))
				log.LogInformation("Sleep timer: stopped bot {Bot}", b.Id);
			foreach (var e in b.Config.Bot.Schedule.ToArray())
			{
				if (!e.Enabled || !TimeOnly.TryParseExact(e.Time, "HH:mm", out var time)) continue;
				// Today's time, or yesterday's for a tick just after midnight.
				foreach (var day in new[] { date, date.AddDays(-1) })
				{
					var at = day.ToDateTime(time);
					if (at <= from || at > now || (e.Days is { Count: > 0 } days && !days.Contains((int)at.DayOfWeek))) continue;
					// Once per day and entry (by what it is, not its place in the list).
					if (!done.Add($"{day:yyyy-MM-dd}/{b.Id}/{e.Time}/{e.Action}/{e.Target}/{e.Shuffle}")) continue;
					await RunAsync(b, e, ct);
				}
			}
		}
		try { await LeaveEmptyAsync(now); }
		catch (Exception ex) { log.LogWarning("Leave-when-empty failed: {Error}", ex.Message); }
	}

	// Bots set to leave when nobody listens: alone in their channel with nothing playing for that long.
	readonly Dictionary<string, DateTime> emptySince = [];

	async Task LeaveEmptyAsync(DateTime now)
	{
		foreach (var b in bots.All)
		{
			var minutes = b.Config.Bot.LeaveWhenEmpty;
			// The first bot (the one set up first) stays, whatever its setting.
			if (minutes <= 0 || bots.All.Count <= 1 || b == bots.All[0] || b.Player.Current != null || await b.Ts.CountListenersAsync() != 0)
			{
				emptySince.Remove(b.Id);
				continue;
			}
			if (!emptySince.TryGetValue(b.Id, out var since)) emptySince[b.Id] = since = now;
			if (now - since < TimeSpan.FromMinutes(minutes)) continue;
			emptySince.Remove(b.Id);
			log.LogInformation("Bot {Bot} was alone for {Minutes} min: removing it", b.Id, minutes);
			await bots.RemoveAsync(b.Id);
		}
	}

	async Task RunAsync(BotInstance b, ScheduleEntry e, CancellationToken ct)
	{
		const string who = "⏰";
		log.LogInformation("Schedule of bot {Bot}: {Action} {Target} at {Time}", b.Id, e.Action, e.Target, e.Time);
		switch (e.Action)
		{
		case "stop":
			b.Player.Stop();
			break;
		case "playlist":
			if (playlists.Exact(e.Target) is not { Songs.Count: > 0 } list)
			{
				log.LogWarning("Schedule: there is no playlist called {Name}", e.Target);
				return;
			}
			var songs = list.Songs.Select(s => s.ToTrack(who));
			if (e.Shuffle) songs = songs.OrderBy(_ => Random.Shared.Next());
			b.Player.Stop();
			b.Player.EnqueueMany(songs.ToList(), exempt: true, out _);
			break;
		case "station":
			Station? station = favorites.All().FirstOrDefault(s => s.Uuid == e.Target);
			if (station is null && !e.Target.StartsWith("link-") && e.Target.Length <= 64)
			{
				try { station = await radios.FindAsync(e.Target, ct); }
				catch (RadioException ex) { log.LogWarning("Schedule: the radio list didn't answer: {Error}", ex.Message); }
			}
			if (station is null) return;
			b.Player.Stop();
			b.Player.Enqueue(station.ToTrack(who), exempt: true);
			break;
		}
	}
}
