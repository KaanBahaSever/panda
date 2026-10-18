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
		if (date != today) { today = date; done.Clear(); }
		var minute = now.ToString("HH:mm");
		foreach (var b in bots.All)
		{
			if (b.Player.SleepAt is { } sleep && sleep <= now)
			{
				log.LogInformation("Sleep timer: stopping bot {Bot}", b.Id);
				b.Player.SetSleep(null);
				b.Player.Stop();
			}
			foreach (var (e, i) in b.Config.Bot.Schedule.Select((e, i) => (e, i)))
			{
				if (!e.Enabled || e.Time != minute || (e.Days is { Count: > 0 } days && !days.Contains((int)now.DayOfWeek))) continue;
				if (!done.Add($"{b.Id}/{i}/{e.Time}/{e.Action}/{e.Target}")) continue;
				await RunAsync(b, e, ct);
			}
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
			if (playlists.Find(e.Target) is not { Songs.Count: > 0 } list)
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
			if (station is null)
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
