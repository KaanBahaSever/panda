using System.Text.Json;
using System.Text.Json.Serialization;

namespace Panda;

/// <summary>An internet radio station (from Radio Browser, or a favorite).</summary>
public sealed record Station(
	string Uuid, string Name, string Url,
	string Homepage = "", string Favicon = "", string Tags = "", string Country = "", string CountryCode = "",
	string Codec = "", int Bitrate = 0, int Votes = 0, bool Hls = false)
{
	/// <summary>Plays the station: a live stream, never saved to the library.</summary>
	public Track ToTrack(string requestedBy) => new("radio:" + Uuid, Name, Subtitle, 0, requestedBy)
	{
		// HLS (short pieces) is read by ffmpeg; Icecast/Shoutcast by Panda, for the song titles.
		Source = Hls || Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ? Media.Direct : Media.Radio,
		Link = Url,
		Art = Media.IsWebLink(Favicon) ? Favicon : null,
	};

	string Subtitle => string.Join(" · ", new[] { Country, string.Join(", ", Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(3)) }
		.Where(s => s.Length > 0));
}

/// <summary>What radio links look like: playlist files that name the stream, and the song titles in the stream.</summary>
public static class RadioLinks
{
	static readonly string[] PlaylistTypes =
		["audio/x-mpegurl", "audio/mpegurl", "application/x-mpegurl", "application/vnd.apple.mpegurl", "audio/x-scpls", "audio/scpls", "application/pls+xml"];

	public static bool IsPlaylist(string type, Uri url) =>
		PlaylistTypes.Contains(type)
		|| url.AbsolutePath.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase)
		|| url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
		|| url.AbsolutePath.EndsWith(".pls", StringComparison.OrdinalIgnoreCase);

	/// <summary>An HLS playlist (made of short pieces) rather than a list of streams.</summary>
	public static bool IsHls(string text) => text.Contains("#EXT-X-");

	/// <summary>The first stream a .pls ("File1=...") or .m3u (a line of its own) names.</summary>
	public static string? FirstStream(string text)
	{
		foreach (var raw in text.Split('\n'))
		{
			var line = raw.Trim();
			if (line.StartsWith("File", StringComparison.OrdinalIgnoreCase) && line.IndexOf('=') is > 4 and var eq)
				line = line[(eq + 1)..].Trim();
			if (!line.StartsWith('#') && Media.IsWebLink(line)) return line;
		}
		return null;
	}

	/// <summary>"StreamTitle='Artist - Song';" from a metadata block; null when it has none (or it's empty).</summary>
	public static string? StreamTitle(byte[] block)
	{
		var end = Array.IndexOf(block, (byte)0);
		var bytes = end < 0 ? block : block[..end];
		string text;
		try { text = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
		catch (ArgumentException) { text = System.Text.Encoding.Latin1.GetString(bytes); }   // older stations
		var start = text.IndexOf("StreamTitle='", StringComparison.Ordinal);
		if (start < 0) return null;
		start += "StreamTitle='".Length;
		var stop = text.IndexOf("';", start, StringComparison.Ordinal);
		var title = (stop < 0 ? text[start..] : text[start..stop]).Trim();
		title = new string(title.Where(c => !char.IsControl(c)).ToArray());
		return title.Length is > 0 and <= 300 && title != "-" ? title : null;
	}
}

public static class Stations
{
	/// <summary>The station a playing track is, for the favorites; null for a song.</summary>
	public static Station? FromTrack(Track t) =>
		t.IsLive && t.Source is Media.Radio or Media.Direct && t.Id.StartsWith("radio:") && Media.IsWebLink(t.Link)
			? new Station(t.Id["radio:".Length..], t.Title, t.Link!, Favicon: t.Art ?? "", Hls: t.Source == Media.Direct)
			: null;
}

public sealed record RadioCountry(string Code, string Name, int Stations);
public sealed record RadioTag(string Name, int Stations);

public sealed class RadioException(string message) : Exception(message);

/// <summary>
/// Radio Browser (radio-browser.info): a free, open list of radio stations. Panda only finds stations
/// there; the audio comes from each station's own stream.
/// </summary>
public sealed class RadioBrowser(PandaConfig config, ILogger<RadioBrowser> log)
{
	static readonly string[] KnownServers = ["https://de1.api.radio-browser.info", "https://de2.api.radio-browser.info", "https://fi1.api.radio-browser.info"];
	string? server;
	(DateTime At, List<RadioCountry> List)? countries;
	(DateTime At, List<RadioTag> List)? tags;

	/// <summary>Stations by name, country (ISO code) and tag, most listened first.</summary>
	public async Task<List<Station>> SearchAsync(string? name, string? country, string? tag, int limit, CancellationToken ct)
	{
		var q = new List<string> { "hidebroken=true", "order=clickcount", "reverse=true", $"limit={Math.Clamp(limit, 1, 100)}" };
		if (!string.IsNullOrWhiteSpace(name)) q.Add("name=" + Uri.EscapeDataString(name.Trim()));
		if (!string.IsNullOrWhiteSpace(country)) q.Add("countrycode=" + Uri.EscapeDataString(country.Trim()));
		if (!string.IsNullOrWhiteSpace(tag)) q.Add("tag=" + Uri.EscapeDataString(tag.Trim().ToLowerInvariant()));
		using var doc = JsonDocument.Parse(await GetAsync("/json/stations/search?" + string.Join('&', q), ct));
		return doc.RootElement.EnumerateArray().Select(FromJson).OfType<Station>().ToList();
	}

	public async Task<Station?> FindAsync(string uuid, CancellationToken ct)
	{
		using var doc = JsonDocument.Parse(await GetAsync("/json/stations/byuuid/" + Uri.EscapeDataString(uuid), ct));
		return doc.RootElement.EnumerateArray().Select(FromJson).OfType<Station>().FirstOrDefault();
	}

	public async Task<List<RadioCountry>> CountriesAsync(CancellationToken ct)
	{
		if (countries is { } c && DateTime.UtcNow - c.At < TimeSpan.FromDays(1)) return c.List;
		using var doc = JsonDocument.Parse(await GetAsync("/json/countries?order=stationcount&reverse=true&hidebroken=true", ct));
		var list = doc.RootElement.EnumerateArray()
			.Select(e => new RadioCountry(Str(e, "iso_3166_1"), Str(e, "name"), Int(e, "stationcount")))
			.Where(x => x.Code.Length == 2 && x.Stations > 0).ToList();
		countries = (DateTime.UtcNow, list);
		return list;
	}

	public async Task<List<RadioTag>> TagsAsync(CancellationToken ct)
	{
		if (tags is { } t && DateTime.UtcNow - t.At < TimeSpan.FromDays(1)) return t.List;
		using var doc = JsonDocument.Parse(await GetAsync("/json/tags?order=stationcount&reverse=true&hidebroken=true&limit=100", ct));
		var list = doc.RootElement.EnumerateArray()
			.Select(e => new RadioTag(Str(e, "name"), Int(e, "stationcount")))
			.Where(x => x.Name.Length is > 0 and <= 40).ToList();
		tags = (DateTime.UtcNow, list);
		return list;
	}

	/// <summary>Tells Radio Browser a station was played (its "popular" order counts these).</summary>
	public void Played(Station s)
	{
		if (s.Uuid.Length == 0 || s.Uuid.StartsWith("link-")) return;
		_ = Task.Run(async () =>
		{
			try { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await GetAsync("/json/url/" + Uri.EscapeDataString(s.Uuid), cts.Token); }
			catch { }
		});
	}

	async Task<string> GetAsync(string path, CancellationToken ct)
	{
		if (config.Sources.RadioBrowserUrl.Trim().TrimEnd('/') is { Length: > 0 } own)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(TimeSpan.FromSeconds(15));
			return await Wrap(async () => Json(await Web.GetApiAsync(own + path, cts.Token)));
		}
		// One of the servers (they mirror each other); another one when it doesn't answer.
		var servers = (server is null ? [] : new[] { server }).Concat(KnownServers.OrderBy(_ => Random.Shared.Next())).Distinct();
		Exception? last = null;
		foreach (var s in servers)
		{
			try
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				cts.CancelAfter(TimeSpan.FromSeconds(8));
				var text = Json(await Web.GetApiAsync(s + path, cts.Token));
				server = s;
				return text;
			}
			catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !ct.IsCancellationRequested)
			{
				last = ex;
				log.LogInformation("Radio Browser {Server} didn't answer: {Error}", s, ex.Message);
			}
		}
		throw new RadioException("radio-list-down: " + last?.Message);
	}

	// A mirror that answers with an error page.
	static string Json(string text)
	{
		using var _ = JsonDocument.Parse(text);
		return text;
	}

	static async Task<string> Wrap(Func<Task<string>> f)
	{
		try { return await f(); }
		catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException) { throw new RadioException("radio-list-down: " + ex.Message); }
	}

	static Station? FromJson(JsonElement e)
	{
		var url = Str(e, "url_resolved") is { Length: > 0 } r ? r : Str(e, "url");
		var uuid = Str(e, "stationuuid");
		if (uuid.Length is 0 or > 64 || !Media.IsWebLink(url)) return null;
		return new Station(uuid, Trim(Str(e, "name").Trim(), 100), url, Str(e, "homepage"), Str(e, "favicon"), Trim(Str(e, "tags"), 200),
			Str(e, "country"), Str(e, "countrycode"), Str(e, "codec"), Int(e, "bitrate"), Int(e, "votes"), Int(e, "hls") == 1);
	}

	static string Trim(string s, int max) => s.Length > max ? s[..max] : s;
	static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
	static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}

/// <summary>Favorite radio stations, shared by all bots, in radios.json in the data folder.</summary>
public sealed class RadioFavorites
{
	public const int Max = 200;

	readonly string path;
	readonly ILogger<RadioFavorites> log;
	readonly object gate = new();
	List<Station> list = [];

	public event Action? Changed;

	static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};

	public RadioFavorites(DataPaths paths, ILogger<RadioFavorites> log)
	{
		this.log = log;
		path = Path.Combine(paths.Data, "radios.json");
		try
		{
			if (File.Exists(path))
				list = (JsonSerializer.Deserialize<List<Station>>(File.ReadAllText(path), JsonOpts) ?? [])
					.Where(s => s != null && s.Uuid is { Length: > 0 } && Media.IsWebLink(s.Url)).ToList();
		}
		catch (Exception ex) when (ex is IOException or JsonException)
		{
			log.LogWarning("Could not read {Path}: {Error}", path, ex.Message);
		}
	}

	public IReadOnlyList<Station> All() { lock (gate) return list.ToArray(); }

	/// <summary>Adds (or updates) a station; false when the list is full.</summary>
	public bool Add(Station s)
	{
		lock (gate)
		{
			var i = list.FindIndex(x => x.Uuid == s.Uuid);
			if (i >= 0) list[i] = s;
			else if (list.Count >= Max) return false;
			else list.Add(s);
			SaveLocked();
		}
		Changed?.Invoke();
		return true;
	}

	public bool Remove(string uuid)
	{
		lock (gate)
		{
			if (list.RemoveAll(x => x.Uuid == uuid) == 0) return false;
			SaveLocked();
		}
		Changed?.Invoke();
		return true;
	}

	/// <summary>The favorite whose name contains every word, best match first.</summary>
	public Station? Find(string query)
	{
		var words = Library.Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0) return null;
		lock (gate)
			return list.Where(s => { var n = Library.Normalize(s.Name); return words.All(n.Contains); })
				.OrderBy(s => s.Name.Length).FirstOrDefault();
	}

	void SaveLocked()
	{
		try { SafeFile.Write(path, JsonSerializer.Serialize(list, JsonOpts)); }
		catch (IOException ex) { log.LogWarning("Could not save {Path}: {Error}", path, ex.Message); }
	}
}
