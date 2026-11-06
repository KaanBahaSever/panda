using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Panda;

/// <summary>
/// Song names from radio.net, for stations whose stream doesn't name its songs: RTL2 sends nothing, the NRJ
/// group's stations (Nostalgie…) only their own track numbers, HLS streams have no titles at all. radio.net's
/// own site asks the same two things: its search (which lists each station's streams), then what the stations
/// play now (many in one ask). Only the station's name and radio.net's id for it are sent; nothing about who listens.
/// <para>It isn't an official API. When radio.net doesn't answer, or answers in a shape Panda doesn't know,
/// Panda goes on without the names (the stream's numbers stay hidden), rests longer each time (up to 30 min)
/// and tries again by itself; the names come back as soon as it answers again.</para>
/// </summary>
public sealed class RadioNames(PandaConfig config, ILogger<RadioNames> log)
{
	public const string DefaultServer = "https://prod.radio-api.net";
	static readonly TimeSpan Found = TimeSpan.FromHours(24), NotFound = TimeSpan.FromHours(6), Fresh = TimeSpan.FromSeconds(20);
	static readonly TimeSpan FirstRest = TimeSpan.FromMinutes(1), LongestRest = TimeSpan.FromMinutes(30), SayAgain = TimeSpan.FromHours(1);
	const int PerAsk = 40;

	// Our stream (its whole address) → radio.net's id for the station, or null when it has none.
	readonly ConcurrentDictionary<string, (DateTime At, string? Id)> ids = new();
	// radio.net id → what it plays (as radio.net says it).
	readonly ConcurrentDictionary<string, (DateTime At, string? Title)> titles = new();
	readonly object gate = new();
	DateTime restUntil = DateTime.MinValue;
	TimeSpan rest = TimeSpan.Zero;
	DateTime saidAt = DateTime.MinValue;
	bool said;
	// The stations waiting for the next now-playing ask: asks that come within a moment go together.
	Dictionary<string, TaskCompletionSource<string?>>? waiting;
	// Two asks at a time at most: the panel's radio list may hold 200 stations.
	readonly SemaphoreSlim asking = new(2);

	public bool Enabled => config.Sources.RadioNames;

	string Server => config.Sources.RadioNamesUrl.Trim().TrimEnd('/') is { Length: > 0 } own ? own : DefaultServer;

	bool Resting { get { lock (gate) return DateTime.UtcNow < restUntil; } }

	sealed class RestingException() : Exception("radio.net is resting");

	/// <summary>What the station plays now by radio.net, or null (not there, nothing known, or radio.net is resting).
	/// Never throws.</summary>
	/// <param name="country">The station's country (from Radio Browser), when known: it tells stations of the same name apart.</param>
	public async Task<string?> TitleAsync(string name, string stream, string? country, CancellationToken ct = default)
	{
		if (!Enabled || !Uri.TryCreate(stream, UriKind.Absolute, out var url) || Resting) return null;
		try
		{
			var id = await IdAsync(name, url, country, ct);
			var title = id is null ? null : Clean(await NowAsync(id, ct), name);
			Answered();
			return title;
		}
		catch (RestingException) { return null; }
		catch (OperationCanceledException) when (ct.IsCancellationRequested) { return null; }
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			Failed(ex);
			return null;
		}
	}

	async Task<string?> IdAsync(string name, Uri stream, string? country, CancellationToken ct)
	{
		var key = stream.AbsoluteUri;
		if (ids.TryGetValue(key, out var known) && DateTime.UtcNow - known.At < (known.Id is null ? NotFound : Found)) return known.Id;
		var query = Plain(name);
		string? id = null;
		if (query.Length > 0)
		{
			using var doc = JsonDocument.Parse(await GetAsync("/stations/search?count=20&query=" + Uri.EscapeDataString(query), ct));
			var list = Items(doc.RootElement, "playables", "stations", "items", "results", "data")?.ToList()
				?? throw new FormatException("the search answer has no list of stations");
			var candidates = list.Select(Candidate.From).OfType<Candidate>().ToList();
			// Stations, but none Panda can read: radio.net changed how it says them.
			if (list.Count > 0 && candidates.Count == 0) throw new FormatException("the search answer's stations have no id");
			id = Pick(candidates, name, stream, country);
		}
		ids[key] = (DateTime.UtcNow, id);
		if (id != null) log.LogInformation("{Station}: song names from radio.net ({Id})", name, id);
		return id;
	}

	Task<string?> NowAsync(string id, CancellationToken ct)
	{
		if (titles.TryGetValue(id, out var c) && DateTime.UtcNow - c.At < Fresh) return Task.FromResult(c.Title);
		lock (gate)
		{
			if (waiting is null)
			{
				waiting = [];
				_ = Task.Run(AskWaitingAsync);
			}
			if (!waiting.TryGetValue(id, out var tcs))
				waiting[id] = tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
			return tcs.Task.WaitAsync(ct);
		}
	}

	// What the waiting stations play: up to 40 in one ask.
	async Task AskWaitingAsync()
	{
		await Task.Delay(200);
		Dictionary<string, TaskCompletionSource<string?>> these;
		lock (gate)
		{
			these = waiting!;
			waiting = null;
		}
		foreach (var part in these.Chunk(PerAsk))
		{
			try
			{
				using var doc = JsonDocument.Parse(await GetAsync("/stations/now-playing?stationIds=" + string.Join(',', part.Select(p => Uri.EscapeDataString(p.Key))), default));
				var list = Items(doc.RootElement, "playables", "stations", "items", "results", "data")?.ToList()
					?? throw new FormatException("the now-playing answer has no list");
				foreach (var (id, tcs) in part)
				{
					// None for a station: radio.net knows nothing now (between songs, a show, one it doesn't follow).
					var its = list.Where(e => e.ValueKind == JsonValueKind.Object && Str(e, "stationId", "id") == id).ToList();
					if (its.Count > 0 && !its.Any(HasTitle)) throw new FormatException("the now-playing answer has no titles");
					var title = its.Select(Title).FirstOrDefault(t => t != null);
					titles[id] = (DateTime.UtcNow, title);
					tcs.TrySetResult(title);
				}
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				foreach (var (_, tcs) in part) tcs.TrySetException(ex);
			}
		}
	}

	async Task<string> GetAsync(string path, CancellationToken ct)
	{
		await asking.WaitAsync(ct);
		try
		{
			// Those that waited for their turn while radio.net failed don't go.
			if (Resting) throw new RestingException();
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(TimeSpan.FromSeconds(10));
			return await Web.GetApiAsync(Server + path, cts.Token);
		}
		finally { asking.Release(); }
	}

	void Answered()
	{
		lock (gate)
		{
			if (rest == TimeSpan.Zero) return;
			rest = TimeSpan.Zero;
			restUntil = DateTime.MinValue;
			if (!said) return;
			said = false;
		}
		log.LogInformation("radio.net answers again: radio song names are back");
	}

	void Failed(Exception ex)
	{
		TimeSpan wait;
		bool say;
		lock (gate)
		{
			// Many asks fail together (a batch, several bots): one rest for all of them.
			if (DateTime.UtcNow < restUntil) return;
			rest = rest == TimeSpan.Zero ? FirstRest : TimeSpan.FromTicks(Math.Min(rest.Ticks * 2, LongestRest.Ticks));
			restUntil = DateTime.UtcNow + rest;
			wait = rest;
			// Said once (again after an hour); meanwhile only in the debug log.
			say = !said || DateTime.UtcNow - saidAt > SayAgain;
			if (say) { said = true; saidAt = DateTime.UtcNow; }
		}
		if (say) log.LogInformation("radio.net didn't answer as expected ({Error}); radio song names from it wait, trying again in {Minutes} min, then less and less often",
			ex.Message, (int)wait.TotalMinutes);
		else log.LogDebug("radio.net still doesn't answer ({Error}); next try in {Minutes} min", ex.Message, (int)wait.TotalMinutes);
	}

	internal sealed record Candidate(string Id, string Name, string Country, List<Uri> Streams)
	{
		public static Candidate? From(JsonElement e)
		{
			if (e.ValueKind != JsonValueKind.Object || Str(e, "id", "stationId") is not { Length: > 0 and <= 200 } id) return null;
			var streams = new List<Uri>();
			if (e.TryGetProperty("streams", out var list) && list.ValueKind == JsonValueKind.Array)
				foreach (var s in list.EnumerateArray())
					if ((s.ValueKind == JsonValueKind.String ? s.GetString() : s.ValueKind == JsonValueKind.Object ? Str(s, "url", "streamUrl") : null) is { } u
						&& Uri.TryCreate(u, UriKind.Absolute, out var uri))
						streams.Add(uri);
			if (Str(e, "streamUrl", "url") is { } one && Uri.TryCreate(one, UriKind.Absolute, out var single)) streams.Add(single);
			return new Candidate(id, Str(e, "name", "title") ?? "", Str(e, "country") ?? "", streams);
		}
	}

	/// <summary>
	/// radio.net's station for our stream. The same stream (host, port and path) is sure. Otherwise the name has to
	/// be the same (without "HQ", "MP3", "128k"…: one station has many streams) and the country too (big hosts like
	/// StreamTheWorld or Zeno carry stations of the same name from many countries), and the stream close: on the
	/// same host or site, or the same last part of the path (icecast.rtl2.fr/rtl2-1-44-128 and
	/// icecast.rtl.fr/rtl2-1-44-128). Without a country, only a stream on the same host with the same last part.
	/// Last, the only one of that name in the station's country. Nothing else: a wrong station's songs would be
	/// worse than none.
	/// </summary>
	internal static string? Pick(IReadOnlyList<Candidate> list, string name, Uri stream, string? country)
	{
		var key = Key(stream);
		if (list.FirstOrDefault(c => c.Streams.Any(s => Key(s) == key)) is { } same) return same.Id;
		var plain = Plain(name);
		if (plain.Length == 0) return null;
		var named = list.Where(c => Plain(c.Name) == plain).ToList();
		var last = LastPart(stream);
		bool SameHost(Uri s) => s.Host.Equals(stream.Host, StringComparison.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(country))
			return named.FirstOrDefault(c => last != null && c.Streams.Any(s => SameHost(s) && LastPart(s) == last))?.Id;
		var here = named.Where(c => SameCountry(c.Country, country)).ToList();
		var site = Site(stream.Host);
		if (here.FirstOrDefault(c => c.Streams.Any(s => SameHost(s) || Site(s.Host) == site || last != null && LastPart(s) == last)) is { } near)
			return near.Id;
		return here.Count == 1 ? here[0].Id : null;
	}

	// Radio Browser says "The United Kingdom Of Great Britain And Northern Ireland" and "Türkiye", radio.net "United Kingdom" and "Turkey".
	internal static bool SameCountry(string a, string b)
	{
		static string N(string s) => string.Join(' ', Library.Normalize(s).Split(' ', StringSplitOptions.RemoveEmptyEntries)) switch
		{
			"turkiye" => "turkey", "usa" or "us" or "the united states of america" or "united states of america" => "united states",
			"uk" or "the united kingdom of great britain and northern ireland" => "united kingdom",
			"the russian federation" => "russia", "the netherlands" => "netherlands", var x => x,
		};
		var (x, y) = (N(a), N(b));
		return x.Length > 0 && x == y;
	}

	// Host, port and path: the query changes from one listing to another (Nostalgie's "?origine=…").
	internal static string Key(Uri u) => u.Authority.ToLowerInvariant() + u.AbsolutePath.TrimEnd('/').ToLowerInvariant();

	// "rtl2-1-44-128" of icecast.rtl2.fr/rtl2-1-44-128, without the file type; null when it says little ("/stream", "/live").
	static string? LastPart(Uri u)
	{
		var part = u.AbsolutePath.TrimEnd('/');
		part = part[(part.LastIndexOf('/') + 1)..];
		if (part.LastIndexOf('.') is > 0 and var dot) part = part[..dot];
		part = part.ToLowerInvariant();
		return part.Length >= 5 && !Common.Contains(part) ? part : null;
	}

	static readonly HashSet<string> Common = ["stream", "listen", "live", "radio", "audio", "index", "playlist", "master", "high", "low", "mobile"];

	// streaming.nrjaudio.fm → nrjaudio.fm; a.files.bbci.co.uk → bbci.co.uk; stream.rfm.fr → rfm.fr.
	internal static string Site(string host)
	{
		var parts = host.ToLowerInvariant().Split('.');
		if (parts.Length <= 2) return string.Join('.', parts);
		var take = SecondLevel.Contains(parts[^2]) && parts[^1].Length == 2 ? 3 : 2;
		return string.Join('.', parts[^take..]);
	}

	static readonly HashSet<string> SecondLevel = ["co", "com", "net", "org", "ac", "gov", "edu", "ne", "or", "gen", "web", "info"];

	/// <summary>A station's name to compare: lowercase, no accents or signs, nothing in brackets, without the
	/// words that only say how it's streamed ("Nostalgie HQ" is "nostalgie", "FIP (no pub)" is "fip").</summary>
	internal static string Plain(string name)
	{
		var text = Regex.Replace(name, @"\([^)]*\)|\[[^\]]*\]", " ");
		var words = Library.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Where(w => !Quality.Contains(w) && !Regex.IsMatch(w, @"^\d+(k|kb|kbps|kbit)$"));
		return string.Join(' ', words);
	}

	static readonly HashSet<string> Quality = ["hq", "hd", "lq", "sd", "uhd", "mp3", "aac", "aacp", "ogg", "opus", "flac", "hifi", "kbps"];

	static readonly string[] TitleFields = ["title", "nowPlaying", "song"];

	static bool HasTitle(JsonElement e) => TitleFields.Any(f => e.TryGetProperty(f, out _));

	// What a station's now-playing says: "Artist - Song", or the two apart.
	static string? Title(JsonElement e)
	{
		if (Str(e, TitleFields) is { Length: > 0 } t)
			return Str(e, "artist") is { Length: > 0 } a && !t.Contains(a, StringComparison.OrdinalIgnoreCase) ? $"{a} - {t}" : t;
		return null;
	}

	/// <summary>A song's name fit to show, or null: not the station's own slogan ("Nostalgie - Nostalgie, tous les
	/// tubes 60 70 !"), not a number ("637 - 619", as the stream itself), not too long; without radio.net's number.</summary>
	internal static string? Clean(string? title, string station)
	{
		if (title is null) return null;
		title = new string(title.Where(c => !char.IsControl(c)).ToArray()).Trim();
		// RTL2's and Skyrock's names end in radio.net's own number: "Yvette Michele - I'm Not Feeling You §7713783".
		title = Regex.Replace(title, @"\s*§\d+$", "");
		if (title.Length is 0 or > 300 || !title.Any(char.IsLetter)) return null;
		var plain = Plain(station);
		var artist = title.Split(" - ", 2)[0];
		if (plain.Length > 0 && (Plain(artist) == plain || Plain(title) == plain)) return null;
		return title;
	}

	// The list in an answer: the answer itself, or the first of these fields that is one.
	static IEnumerable<JsonElement>? Items(JsonElement root, params string[] names)
	{
		if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray();
		if (root.ValueKind != JsonValueKind.Object) return null;
		foreach (var n in names)
			if (root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array) return v.EnumerateArray();
		return null;
	}

	static string? Str(JsonElement e, params string[] names)
	{
		foreach (var n in names)
			if (e.TryGetProperty(n, out var v))
			{
				if (v.ValueKind == JsonValueKind.String) return v.GetString();
				if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
			}
		return null;
	}
}
