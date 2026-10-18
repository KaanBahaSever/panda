using System.Text.Json;
using System.Text.RegularExpressions;

namespace Panda;

/// <summary>
/// What a link or a search turned into: the song to play now and, for an album or a playlist of a music
/// service, the rest as "artist - title" to look up one by one after the first one started.
/// </summary>
/// <param name="SearchedFor">The words looked up for a link that can't be played itself (a Spotify song, a page).</param>
public sealed record Resolved(Track Track, IReadOnlyList<string>? More = null, string? ListName = null, string? SearchedFor = null);

/// <summary>
/// Turns what people type into songs, from wherever it is: YouTube (the main source), the library, other
/// sites through yt-dlp, music service links (looked up on YouTube), radio streams, any other page (its
/// title looked up on YouTube). Every source but YouTube can be turned off (<see cref="SourcesSettings"/>).
/// </summary>
public sealed partial class Sources(PandaConfig config, YouTube youtube, Library library, ILogger<Sources> log)
{
	SourcesSettings S => config.Sources;
	const int MaxListSongs = 200;
	// Lists being added share these: many pasted playlists don't start many yt-dlp at once.
	readonly SemaphoreSlim listSearches = new(2);
	readonly SemaphoreSlim imports = new(1);

	// "sc:words", "ytm:words", "yt:words": that search only.
	[GeneratedRegex(@"^(sc|soundcloud|ytm|yt|youtube)\s*:(?!//)\s*(.*)$", RegexOptions.IgnoreCase)]
	private static partial Regex PrefixRegex();

	public static string? PrefixSearch(string prefix) => prefix.ToLowerInvariant() switch
	{
		"sc" or "soundcloud" => "soundcloud",
		"ytm" => "youtubeMusic",
		"yt" or "youtube" => "youtube",
		_ => null,
	};

	public async Task<Resolved> ResolveAsync(string input, string requestedBy, CancellationToken ct = default)
	{
		var clean = YouTube.CleanInput(input);
		if (clean.Length == 0) throw new YouTubeException("empty");

		if (PrefixRegex().Match(clean) is { Success: true } p && PrefixSearch(p.Groups[1].Value) is { } only)
		{
			var words = p.Groups[2].Value.Trim();
			if (words.Length == 0) throw new YouTubeException("empty");
			if (!S.Enabled(only)) throw new YouTubeException("source-off");
			var found = await SearchInAsync(only, words, 1, requestedBy, ct);
			return new(Checked(found.FirstOrDefault() ?? throw new YouTubeException("not-found")));
		}
		if (YouTube.ParseVideoId(clean) is { } id)
			return new(library.Find(id, requestedBy) is { } saved ? Checked(saved) : await youtube.ResolveAsync(clean, requestedBy, ct));
		if (YouTube.IsYouTubeLink(clean))
			return new(await youtube.ResolveAsync(clean, requestedBy, ct));
		if (YouTube.LooksLikeUrl(clean))
			return await ResolveLinkAsync(clean, requestedBy, ct);
		return new(await FindAsync(clean, requestedBy, ct));
	}

	/// <summary>Words: a saved song that matches, else the first search result.</summary>
	public async Task<Track> FindAsync(string words, string requestedBy, CancellationToken ct = default)
	{
		if (library.Search(words, requestedBy) is { } saved) return Checked(saved);
		var results = await SearchAsync(words, 1, requestedBy, ct);
		return Checked(results.FirstOrDefault() ?? throw new YouTubeException("not-found"));
	}

	/// <summary>A search where the settings say (YouTube first unless chosen otherwise), then the others.</summary>
	public async Task<List<Track>> SearchAsync(string query, int count, string requestedBy, CancellationToken ct = default)
	{
		YouTubeException? first = null;
		foreach (var source in S.SearchOrder())
		{
			try
			{
				var found = await SearchInAsync(source, query, count, requestedBy, ct);
				if (found.Count > 0) return found;
			}
			catch (YouTubeException ex) when (ex.Message != "empty")
			{
				first ??= ex;
				log.LogInformation("Search on {Source} failed: {Error}", source, ex.Message);
			}
		}
		if (first != null) throw first;
		return [];
	}

	/// <summary>A search for !search and the panel: "sc:words" searches SoundCloud only, and so on.</summary>
	public async Task<List<Track>> SearchAnyAsync(string input, int count, string requestedBy, CancellationToken ct = default)
	{
		var clean = YouTube.CleanInput(input);
		if (PrefixRegex().Match(clean) is { Success: true } p && PrefixSearch(p.Groups[1].Value) is { } only)
		{
			if (!S.Enabled(only)) throw new YouTubeException("source-off");
			return await SearchInAsync(only, p.Groups[2].Value.Trim(), count, requestedBy, ct);
		}
		return await SearchAsync(clean, count, requestedBy, ct);
	}

	public Task<List<Track>> SearchInAsync(string source, string query, int count, string requestedBy, CancellationToken ct = default) => source switch
	{
		"youtubeMusic" => youtube.MusicSearchAsync(query, count, requestedBy, ct),
		"soundcloud" => youtube.SiteSearchAsync("scsearch", query, count, requestedBy, ct),
		_ => youtube.SearchAsync(query, count, requestedBy, ct),
	};

	Track Checked(Track t)
	{
		youtube.CheckLength(t);   // saved before the limit was lowered, or another site
		return t;
	}

	// ---- links ----

	// Sites yt-dlp plays: no need to look at what the link is first.
	static readonly string[] KnownSites = ["soundcloud.com", "bandcamp.com", "mixcloud.com", "vimeo.com", "twitch.tv", "dailymotion.com", "audiomack.com"];

	static bool HostIs(Uri u, string host) =>
		u.Host.Equals(host, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase);

	async Task<Resolved> ResolveLinkAsync(string text, string requestedBy, CancellationToken ct)
	{
		var url = text.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text : "https://" + text;
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
			throw new YouTubeException("not-found");
		try
		{
			if (MusicLinks.IsService(uri))
			{
				if (!S.Services) throw new YouTubeException("source-off");
				return await ServiceAsync(uri, requestedBy, ct);
			}
			await Web.CheckAsync(uri, ct);
			var known = KnownSites.Any(h => HostIs(uri, h));
			if (!known && await ProbeAsync(uri, requestedBy, ct) is { } direct) return new(direct);
			YouTubeException? siteError = null;
			if (S.Sites)
			{
				try { return new(await youtube.ResolveSiteAsync(uri.AbsoluteUri, requestedBy, ct)); }
				catch (YouTubeException ex) when (!known || ex.Message.StartsWith("Unsupported URL", StringComparison.OrdinalIgnoreCase))
				{
					// yt-dlp doesn't know the page: look at its title.
					siteError = ex;
				}
			}
			else if (known) throw new YouTubeException("source-off");
			if (S.Pages && await PageTitleAsync(uri, ct) is { Length: > 0 } title)
				return new(await FindAsync(title, requestedBy, ct), SearchedFor: title);
			if (siteError != null && known && !siteError.Message.StartsWith("Unsupported URL", StringComparison.OrdinalIgnoreCase)) throw siteError;
			throw new YouTubeException(S.Sites || S.Pages ? "link-unplayable" : "source-off");
		}
		catch (PrivateLinkException) { throw new YouTubeException("link-private"); }
		catch (HttpRequestException ex)
		{
			log.LogInformation("Could not open {Url}: {Error}", uri, ex.Message);
			throw new YouTubeException("link-unreachable");
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new YouTubeException("timeout"); }
		catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException)
		{
			log.LogInformation("Could not open {Url}: {Error}", uri, ex.Message);
			throw new YouTubeException("link-unreachable");
		}
		catch (System.Text.Json.JsonException) { throw new YouTubeException("not-found"); }
	}

	/// <summary>
	/// What is behind a link: a radio stream (live, through Panda), an HLS stream or an audio file (ffmpeg
	/// plays it), or null for a page (yt-dlp or the page title next).
	/// </summary>
	async Task<Track?> ProbeAsync(Uri uri, string requestedBy, CancellationToken ct)
	{
		HttpStream http;
		try { http = await HttpStream.OpenAsync(uri.AbsoluteUri, ct); }
		catch (Exception ex) when (ex is HttpRequestException or IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException
			|| (ex is OperationCanceledException && !ct.IsCancellationRequested))
		{
			log.LogInformation("Could not look at {Url}: {Error}", uri, ex.Message);
			return null;   // yt-dlp may still know it
		}
		var final = http.Url;
		using (http)
		{
			var type = http.Type;
			if (RadioLinks.IsPlaylist(type, http.Url))
			{
				var text = await http.ReadTextAsync(64 * 1024, ct);
				if (RadioLinks.IsHls(text)) return Stream(http.Url, Media.Direct, null, null);
				if (RadioLinks.FirstStream(text) is { } inner && Uri.TryCreate(inner, UriKind.Absolute, out var innerUri))
				{
					await Web.CheckAsync(innerUri, ct);
					// The stream itself names the station.
					using var s = await HttpStream.OpenAsync(inner, ct);
					return Stream(s.Url, Media.Radio, s.Headers.GetValueOrDefault("icy-name"), s.Headers.GetValueOrDefault("icy-genre"));
				}
				return null;
			}
			var audio = type.StartsWith("audio/") || type.StartsWith("video/") || type == "application/ogg"
				|| (type == "application/octet-stream" && http.IsIcy);
			if (!audio && !http.IsIcy) return null;
			// A station sends forever; a file says how long it is.
			if (http.IsIcy || http.Length is null)
				return Stream(http.Url, Media.Radio, http.Headers.GetValueOrDefault("icy-name"), http.Headers.GetValueOrDefault("icy-genre"));
		}
		if (!S.Sites) throw new YouTubeException("source-off");
		return await youtube.ProbeFileAsync(final.AbsoluteUri, requestedBy, ct);

		Track Stream(Uri url, string source, string? name, string? genre)
		{
			if (!S.Radio) throw new YouTubeException("source-off");
			var host = url.Host;
			return new Station("link-" + YouTube.Hash(url.AbsoluteUri), Short(name) ?? host, url.AbsoluteUri, Tags: Short(genre) ?? "", Hls: source == Media.Direct)
				.ToTrack(requestedBy) with { Channel = Short(genre) ?? host };
		}
	}

	static string? Short(string? s) => s?.Trim() is { Length: > 0 } t ? (t.Length > 100 ? t[..100] : t) : null;

	async Task<string> PageTitleAsync(Uri uri, CancellationToken ct)
	{
		var (final, type, html) = await Web.GetPageAsync(uri.AbsoluteUri, ct);
		return type.Contains("html") ? CleanTitle(Web.PageTitle(html), final.Host) : "";
	}

	// "Song - Artist | Site", "Song by Artist on Site": the site's part goes.
	static string CleanTitle(string title, string host)
	{
		// The site's own name at the end ("... - Wikipedia" on en.wikipedia.org).
		var labels = host.Split('.');
		var site = labels.Length >= 2 ? labels[^2] : host;
		var m = Regex.Match(title, @"^(.+?)\s+[|\-–—·:]\s+([^|\-–—·:]{1,40})$");
		if (m.Success && site.Length >= 3 && Library.Normalize(m.Groups[2].Value).Replace(" ", "").Contains(Library.Normalize(site)))
			title = m.Groups[1].Value;
		title = Regex.Replace(title, @"\s+(?:on|en|auf|sur)\s+(?:Apple Music|TIDAL|Deezer|Spotify|SoundCloud)\s*$", "", RegexOptions.IgnoreCase);
		title = Regex.Replace(title, @"\s*[|\-–—]\s*(?:Apple Music|TIDAL|Deezer|Spotify|SoundCloud|YouTube)\s*$", "", RegexOptions.IgnoreCase);
		return title.Length > 200 ? title[..200] : title;
	}

	// ---- music services: their audio is protected, so the song is looked up on YouTube ----

	async Task<Resolved> ServiceAsync(Uri uri, string requestedBy, CancellationToken ct)
	{
		// Short links (spotify.link, deezer.page.link...) lead to the real one.
		if (MusicLinks.IsShortLink(uri))
		{
			await Web.CheckAsync(uri, ct);
			uri = (await Web.GetPageAsync(uri.AbsoluteUri, ct)).Url;
			if (!MusicLinks.IsService(uri) || MusicLinks.IsShortLink(uri)) throw new YouTubeException("not-found");
		}
		var (name, songs) = await MusicLinks.ReadAsync(uri, ct);
		if (songs.Count == 0) throw new YouTubeException("not-found");
		// The first song that is found plays; the ones after it come later.
		for (var i = 0; i < songs.Count && i < 5; i++)
		{
			Track track;
			try { track = await FindAsync(songs[i], requestedBy, ct); }
			catch (YouTubeException ex) when (ex.Message == "not-found" && i + 1 < songs.Count) { continue; }
			var more = songs.Skip(i + 1).Take(MaxListSongs - 1).ToList();
			return new(track, more.Count > 0 ? more : null, name, songs[i]);
		}
		throw new YouTubeException("not-found");
	}

	/// <summary>
	/// A playlist from the web into a saved list: a YouTube list (or another site's set) at once, a Spotify
	/// or Deezer list song by song in the background (each looked up on YouTube). Returns the list's name,
	/// how many songs went in now and how many are still being looked up.
	/// </summary>
	public async Task<(string Name, int Added, int Coming)> ImportAsync(string input, string? name, Playlists lists, CancellationToken ct = default)
	{
		var url = YouTube.CleanInput(input);
		if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new YouTubeException("not-found");
		if (name is { Length: > 0 } given && !Playlists.IsValidName(given)) throw new YouTubeException("bad-playlist-name");
		// A given name adds to that list; the list's own title gets a number when it's taken ("Mix 2").
		string Named(string? title)
		{
			if (name is { Length: > 0 } n) return n.Trim();
			var t = (title ?? "").Replace('/', '-').Replace('\\', '-').Trim();
			if (t.Length > 36) t = t[..36].Trim();
			if (!Playlists.IsValidName(t)) t = "Import " + DateTime.Now.ToString("MM-dd HH:mm:ss");
			var candidate = t;
			for (var i = 2; lists.Exact(candidate) != null; i++) candidate = $"{t} {i}";
			return candidate;
		}
		try
		{
			if (MusicLinks.IsService(uri))
			{
				if (!S.Services) throw new YouTubeException("source-off");
				if (MusicLinks.IsShortLink(uri))
				{
					await Web.CheckAsync(uri, ct);
					uri = (await Web.GetPageAsync(uri.AbsoluteUri, ct)).Url;
					if (!MusicLinks.IsService(uri) || MusicLinks.IsShortLink(uri)) throw new YouTubeException("not-found");
				}
				var (title, queries) = await MusicLinks.ReadAsync(uri, ct);
				if (queries.Count == 0) throw new YouTubeException("not-found");
				var listName = lists.Save(Named(title), []).Name;
				// One import at a time; it stops when the list is deleted or renamed meanwhile, or full.
				_ = Task.Run(async () =>
				{
					await imports.WaitAsync();
					try
					{
						var (added, missed) = await AddAllAsync(queries.Take(Playlists.MaxSongs).ToList(), "", t =>
							lists.AddIfExists(listName, PlaylistSong.From(t), out var left) && left == 0);
						log.LogInformation("Imported {Added} songs into {List} ({Missed} not found)", added, listName, missed);
					}
					catch (Exception ex) { log.LogWarning("Import into {List} failed: {Error}", listName, ex.Message); }
					finally { imports.Release(); }
				});
				return (listName, 0, Math.Min(queries.Count, Playlists.MaxSongs));
			}
			var isYouTube = YouTube.IsYouTubeLink(url);
			if (!isYouTube)
			{
				if (!S.Sites) throw new YouTubeException("source-off");
				await Web.CheckAsync(uri, ct);
			}
			var (listTitle, songs) = await youtube.ReadListAsync(uri.AbsoluteUri, Playlists.MaxSongs, isYouTube, ct);
			if (songs.Count == 0) throw new YouTubeException("playlist-empty");
			var target = Named(listTitle);
			var before = lists.Exact(target)?.Songs.Count ?? 0;
			var saved = lists.Save(target, songs.Select(PlaylistSong.From), out _);
			return (saved.Name, saved.Songs.Count - before, 0);
		}
		catch (PrivateLinkException) { throw new YouTubeException("link-private"); }
		catch (HttpRequestException) { throw new YouTubeException("link-unreachable"); }
		catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException) { throw new YouTubeException("link-unreachable"); }
		catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new YouTubeException("timeout"); }
		catch (System.Text.Json.JsonException) { throw new YouTubeException("not-found"); }
	}

	/// <summary>
	/// Looks the rest of a list up and hands each song to <paramref name="add"/> (false: stop, the queue is
	/// full, or it was cleared). Returns how many were added and how many weren't found.
	/// </summary>
	public async Task<(int Added, int Missed)> AddAllAsync(IReadOnlyList<string> songs, string requestedBy, Func<Track, bool> add, CancellationToken ct = default)
	{
		int added = 0, missed = 0;
		foreach (var words in songs)
		{
			if (ct.IsCancellationRequested) break;
			Track t;
			await listSearches.WaitAsync(ct);
			try { t = await FindAsync(words, requestedBy, ct); }
			catch (YouTubeException ex)
			{
				missed++;
				log.LogInformation("Not found for a list: {Words} ({Error})", words, ex.Message);
				continue;
			}
			finally { listSearches.Release(); }
			if (!add(t)) break;
			added++;
		}
		return (added, missed);
	}
}

/// <summary>Spotify, Deezer, Apple Music and Tidal links: what song (or songs) they are.</summary>
public static partial class MusicLinks
{
	[GeneratedRegex(@"^/(?:intl-[a-z-]+/|embed/)?(track|album|playlist)/([A-Za-z0-9]{10,40})", RegexOptions.IgnoreCase)]
	private static partial Regex SpotifyPath();

	[GeneratedRegex(@"^/(?:[a-z]{2}(?:-[a-z]{2})?/)?(track|album|playlist)/(\d{1,20})", RegexOptions.IgnoreCase)]
	private static partial Regex DeezerPath();

	static bool Is(Uri u, params string[] hosts) =>
		hosts.Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

	public static bool IsShortLink(Uri u) => Is(u, "spotify.link", "spoti.fi", "deezer.page.link", "link.deezer.com");

	public static bool IsService(Uri u) => IsShortLink(u)
		|| Is(u, "open.spotify.com", "play.spotify.com", "music.apple.com", "tidal.com", "listen.tidal.com")
		|| (Is(u, "deezer.com") && !u.Host.StartsWith("api.", StringComparison.OrdinalIgnoreCase));

	/// <summary>The list's name (null for one song) and its songs as "artist - title".</summary>
	public static async Task<(string? Name, List<string> Songs)> ReadAsync(Uri u, CancellationToken ct)
	{
		if (Is(u, "spotify.com") && SpotifyPath().Match(u.AbsolutePath) is { Success: true } sp)
			return await SpotifyAsync(sp.Groups[1].Value.ToLowerInvariant(), sp.Groups[2].Value, ct);
		if (Is(u, "deezer.com") && DeezerPath().Match(u.AbsolutePath) is { Success: true } dz)
			return await DeezerAsync(dz.Groups[1].Value.ToLowerInvariant(), dz.Groups[2].Value, ct);
		// Apple Music and Tidal: the page's title ("Song by Artist on Apple Music"), read like any linked page.
		var (_, _, html) = await Web.GetPageAsync(u.AbsoluteUri, ct);
		var title = Regex.Replace(Web.PageTitle(html), @"\s+(?:on|en|auf|sur)\s+(?:Apple Music|TIDAL)\s*$|\s*[|\-–—]\s*(?:Apple Music|TIDAL)\s*$", "", RegexOptions.IgnoreCase);
		if (title.Length == 0 || title.StartsWith("Not Found", StringComparison.OrdinalIgnoreCase)) return (null, []);
		return (null, [title]);
	}

	// The embed page carries the song or the list (up to 100 songs) without an API key.
	static async Task<(string?, List<string>)> SpotifyAsync(string kind, string id, CancellationToken ct)
	{
		var html = await Web.GetApiAsync($"https://open.spotify.com/embed/{kind}/{id}", ct, browser: true);
		var m = Regex.Match(html, @"<script id=""__NEXT_DATA__"" type=""application/json"">(.*?)</script>", RegexOptions.Singleline);
		if (!m.Success) return (null, []);
		using var doc = JsonDocument.Parse(m.Groups[1].Value);
		if (!doc.RootElement.TryGetProperty("props", out var props) || !props.TryGetProperty("pageProps", out var page)
			|| !page.TryGetProperty("state", out var state) || !state.TryGetProperty("data", out var data) || !data.TryGetProperty("entity", out var e))
			return (null, []);
		string Str(JsonElement x, string n) => x.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()!.Replace(' ', ' ') : "";
		if (kind == "track")
		{
			var artists = e.TryGetProperty("artists", out var a) && a.ValueKind == JsonValueKind.Array
				? string.Join(", ", a.EnumerateArray().Select(x => Str(x, "name")).Where(s => s.Length > 0)) : Str(e, "subtitle");
			var name = Str(e, "name") is { Length: > 0 } n ? n : Str(e, "title");
			return (null, name.Length > 0 ? [Join(artists, name)] : []);
		}
		var songs = e.TryGetProperty("trackList", out var list) && list.ValueKind == JsonValueKind.Array
			? list.EnumerateArray().Select(x => Join(Str(x, "subtitle"), Str(x, "title"))).Where(s => s.Length > 0).ToList() : [];
		return (Str(e, "name") is { Length: > 0 } ln ? ln : Str(e, "title"), songs);
	}

	// Deezer's public API needs no key.
	static async Task<(string?, List<string>)> DeezerAsync(string kind, string id, CancellationToken ct)
	{
		using var doc = JsonDocument.Parse(await Web.GetApiAsync($"https://api.deezer.com/{kind}/{id}", ct));
		var e = doc.RootElement;
		if (e.TryGetProperty("error", out _)) return (null, []);
		static string Song(JsonElement t) =>
			Join(t.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
				t.TryGetProperty("title", out var ti) ? ti.GetString() ?? "" : "");
		if (kind == "track") return (null, [Song(e)]);
		var songs = e.TryGetProperty("tracks", out var tr) && tr.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array
			? d.EnumerateArray().Select(Song).Where(s => s.Length > 0).ToList() : [];
		return (e.TryGetProperty("title", out var t) ? t.GetString() : null, songs);
	}

	static string Join(string artist, string title) =>
		title.Trim().Length == 0 ? "" : artist.Trim().Length == 0 ? title.Trim() : $"{artist.Trim()} - {title.Trim()}";
}
