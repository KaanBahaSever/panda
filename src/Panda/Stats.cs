namespace Panda;

public sealed record StatsSong(string Id, string Title, string Channel, int Duration, int Plays, long Listened)
{
	public string? Source { get; init; }
	public string? Link { get; init; }
	public string? Art { get; init; }
	public string Url => Media.Url(Id, Source, Link);
	public string? Thumbnail => Media.Thumbnail(Id, Source, Art);
	public bool IsLive => Duration <= 0;
}

public sealed record StatsPerson(string Name, int Plays, long Listened);

/// <param name="Key">"2026-10-12" for a day, "2026-10" for a month.</param>
public sealed record StatsBucket(string Key, int Plays, long Listened);

/// <param name="Listened">Seconds actually heard.</param>
/// <param name="Bucket">"day" or "month": what one entry of <paramref name="Timeline"/> covers.</param>
/// <param name="Hours">Plays per hour of the day (0–23), in the viewer's time zone.</param>
public sealed record StatsInfo(
	int Plays, long Listened, int Songs, int People,
	string Bucket, IReadOnlyList<StatsBucket> Timeline, int[] Hours,
	IReadOnlyList<StatsSong> TopSongs, IReadOnlyList<StatsPerson> TopPeople);

/// <summary>Statistics from the bots' play histories, worked out when asked (the panel asks rarely).</summary>
public sealed class Stats(BotManager bots)
{
	const int Top = 10;
	const int MaxDayBuckets = 90;
	const int MaxMonthBuckets = 24;

	/// <param name="bot">A bot id, or null/"all" for every bot.</param>
	/// <param name="days">The last N days including today; 0 = everything.</param>
	/// <param name="tzOffset">The viewer's JavaScript getTimezoneOffset(): minutes from local time to UTC.</param>
	public StatsInfo Get(string? bot, int days, int tzOffset)
	{
		days = Math.Clamp(days, 0, 36500);
		var offset = TimeSpan.FromMinutes(-Math.Clamp(tzOffset, -14 * 60, 14 * 60));
		var today = DateTimeOffset.UtcNow.ToOffset(offset).Date;
		DateTimeOffset? from = days > 0 ? new DateTimeOffset(today.AddDays(1 - days), offset) : null;

		var sources = bot is null or "" or "all" ? bots.All : bots.Get(bot) is { } b ? [b] : [];
		var plays = sources.SelectMany(s => s.History.ReadAll()).Where(p => from is null || p.PlayedAt >= from).ToList();
		DateTime Local(PlayedTrack p) => p.PlayedAt.ToOffset(offset).DateTime;

		// Days for short ranges, months for long ones.
		var byDay = days is > 0 and <= MaxDayBuckets;
		var timeline = new List<StatsBucket>();
		if (byDay)
		{
			var perDay = plays.GroupBy(p => Local(p).Date).ToDictionary(g => g.Key, g => (Plays: g.Count(), Listened: g.Sum(p => (long)p.Listened)));
			for (var d = today.AddDays(1 - days); d <= today; d = d.AddDays(1))
			{
				var v = perDay.GetValueOrDefault(d);
				timeline.Add(new(d.ToString("yyyy-MM-dd"), v.Plays, v.Listened));
			}
		}
		else if (plays.Count > 0)
		{
			var perMonth = plays.GroupBy(p => new DateTime(Local(p).Year, Local(p).Month, 1))
				.ToDictionary(g => g.Key, g => (Plays: g.Count(), Listened: g.Sum(p => (long)p.Listened)));
			var last = new DateTime(today.Year, today.Month, 1);
			var first = from is { } f ? new DateTime(f.Year, f.Month, 1) : perMonth.Keys.Min();
			if (first < last.AddMonths(1 - MaxMonthBuckets)) first = last.AddMonths(1 - MaxMonthBuckets);
			for (var m = first; m <= last; m = m.AddMonths(1))
			{
				var v = perMonth.GetValueOrDefault(m);
				timeline.Add(new(m.ToString("yyyy-MM"), v.Plays, v.Listened));
			}
		}

		var hours = new int[24];
		foreach (var p in plays) hours[Local(p).Hour]++;

		var topSongs = plays.GroupBy(p => p.Id)
			.Select(g => { var latest = g.MaxBy(p => p.PlayedAt)!; return new StatsSong(g.Key, latest.Title, latest.Channel, latest.Duration, g.Count(), g.Sum(p => (long)p.Listened)) { Source = latest.Source, Link = latest.Link, Art = latest.Art }; })
			.OrderByDescending(s => s.Plays).ThenByDescending(s => s.Listened)
			.Take(Top).ToList();
		// Autoplay picks ("✨ Autoplay"; up to 0.5 "📻 Radio", "📻 Radyo"...) are nobody's request.
		var people = plays.Where(p => !p.RequestedBy.StartsWith("📻 ") && !p.RequestedBy.StartsWith("✨ ")).ToList();
		var topPeople = people.GroupBy(p => p.RequestedBy)
			.Select(g => new StatsPerson(g.Key, g.Count(), g.Sum(p => (long)p.Listened)))
			.OrderByDescending(s => s.Plays).ThenByDescending(s => s.Listened)
			.Take(Top).ToList();

		return new StatsInfo(
			plays.Count, plays.Sum(p => (long)p.Listened),
			plays.Select(p => p.Id).Distinct().Count(), people.Select(p => p.RequestedBy).Distinct().Count(),
			byDay ? "day" : "month", timeline, hours, topSongs, topPeople);
	}
}
