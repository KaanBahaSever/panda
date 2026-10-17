using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Panda;

/// <summary>A part of a video to skip, in seconds.</summary>
public sealed record Segment(double Start, double End, string Category);

/// <summary>
/// The parts of YouTube videos that aren't music (a talk before the song, an ad), as people marked them on
/// SponsorBlock. Asked by the first four characters of the id's SHA-256, so the server doesn't learn
/// which video plays. Kept for a few hours; a song asked for again doesn't ask again.
/// </summary>
public sealed class SponsorBlock(PandaConfig config, ILogger<SponsorBlock> log)
{
	static readonly TimeSpan Keep = TimeSpan.FromHours(6);
	const int MaxKept = 1000;

	readonly object gate = new();
	readonly Dictionary<string, (Segment[] Segments, DateTime At)> cache = [];
	readonly Dictionary<string, Task<Segment[]>> asking = [];

	public bool Enabled => config.YouTube.SponsorBlock && config.YouTube.SponsorBlockCategories.Count > 0;

	/// <summary>The segments if they are known by now (null: not asked yet, or still on the way).</summary>
	public Segment[]? Known(Track t)
	{
		if (!Enabled || !t.IsYouTube || t.IsLive) return null;
		lock (gate)
			return cache.TryGetValue(t.Id, out var c) && DateTime.UtcNow - c.At < Keep ? Filter(c.Segments) : null;
	}

	/// <summary>Asks for a song's segments ahead of time (songs in the queue), so they're known when it starts.</summary>
	public void Prefetch(Track t) { if (Known(t) is null) _ = GetAsync(t); }

	public async Task<Segment[]> GetAsync(Track t)
	{
		if (!Enabled || !t.IsYouTube || t.IsLive) return [];
		if (Known(t) is { } known) return known;
		Task<Segment[]> task;
		lock (gate)
		{
			if (!asking.TryGetValue(t.Id, out task!))
				asking[t.Id] = task = AskAsync(t.Id);
		}
		try { return Filter(await task); }
		finally { lock (gate) asking.Remove(t.Id); }
	}

	// Every category is asked and kept; which ones are skipped is decided when playing, so a change
	// in the settings works at once.
	async Task<Segment[]> AskAsync(string id)
	{
		Segment[] found = [];
		try
		{
			var prefix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..4].ToLowerInvariant();
			var categories = Uri.EscapeDataString(JsonSerializer.Serialize(YouTubeSettings.SponsorCategories));
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			var url = $"{config.YouTube.SponsorBlockUrl.TrimEnd('/')}/api/skipSegments/{prefix}?categories={categories}";
			string text;
			try { text = await Web.GetApiAsync(url, cts.Token); }
			catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { text = "[]"; }   // nothing for this prefix
			using var doc = JsonDocument.Parse(text);
			foreach (var v in doc.RootElement.EnumerateArray())
			{
				if (v.GetProperty("videoID").GetString() != id) continue;
				found = v.GetProperty("segments").EnumerateArray()
					.Where(s => !s.TryGetProperty("actionType", out var a) || a.GetString() == "skip")
					.Select(s =>
					{
						var seg = s.GetProperty("segment");
						return new Segment(seg[0].GetDouble(), seg[1].GetDouble(), s.GetProperty("category").GetString() ?? "");
					})
					.Where(s => s.End > s.Start).OrderBy(s => s.Start).ToArray();
			}
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
		{
			log.LogInformation("SponsorBlock didn't answer for {Id}: {Error}", id, ex.Message);
			return [];   // not kept: asked again next time
		}
		lock (gate)
		{
			if (cache.Count >= MaxKept)
				foreach (var old in cache.OrderBy(c => c.Value.At).Take(MaxKept / 4).Select(c => c.Key).ToList())
					cache.Remove(old);
			cache[id] = (found, DateTime.UtcNow);
		}
		return found;
	}

	// The chosen categories; overlapping parts are merged into one.
	Segment[] Filter(Segment[] all)
	{
		var wanted = config.YouTube.SponsorBlockCategories;
		var merged = new List<Segment>();
		foreach (var s in all.Where(s => wanted.Contains(s.Category)))
		{
			if (merged.Count > 0 && s.Start <= merged[^1].End + 0.5)
				merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, s.End) };
			else merged.Add(s);
		}
		return [.. merged];
	}
}
