using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Panda;

/// <summary>A link that may not be opened: it points into the local network (see <see cref="Web.CheckAsync"/>).</summary>
public sealed class PrivateLinkException() : Exception("link-private");

/// <summary>
/// Reading web pages and APIs for links, music services and radio: short timeouts, a size limit.
/// Links people send are checked first: they may not point into Panda's own network (a router's page,
/// a cloud metadata service), unless <see cref="SourcesSettings.LocalLinks"/> allows it.
/// </summary>
public static class Web
{
	// Pages answer browsers best (Apple Music sends little to anything else); APIs ask to be told who asks.
	const string BrowserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
	public static readonly string PandaAgent = $"Panda/{PanelApi.Version} (+https://github.com/KaanBahaSever/panda)";
	const int MaxBody = 2 * 1024 * 1024;

	/// <summary>Tests point services elsewhere: "https://open.spotify.com/=http://127.0.0.1:8098/spotify/;...".</summary>
	static readonly (string From, string To)[] Rewrites = (Environment.GetEnvironmentVariable("PANDA_TEST_REWRITE") ?? "")
		.Split(';', StringSplitOptions.RemoveEmptyEntries)
		.Select(r => r.Split('=', 2)).Where(p => p.Length == 2).Select(p => (p[0], p[1])).ToArray();

	static string Rewrite(string url)
	{
		foreach (var (from, to) in Rewrites)
			if (url.StartsWith(from, StringComparison.OrdinalIgnoreCase)) return to + url[from.Length..];
		return url;
	}

	/// <summary>Whether links may point into the local network (<see cref="SourcesSettings.LocalLinks"/>).</summary>
	public static Func<bool> AllowLocalLinks { get; set; } = () => false;
	static bool AllowLocal => AllowLocalLinks();

	static readonly HttpClient Api = Create(guarded: false);
	static readonly HttpClient Links = Create(guarded: true);

	static HttpClient Create(bool guarded)
	{
		var handler = new SocketsHttpHandler
		{
			AutomaticDecompression = DecompressionMethods.All,
			ConnectTimeout = TimeSpan.FromSeconds(10),
			MaxAutomaticRedirections = 5,
		};
		// Every connection, redirects too, goes to an address that was checked.
		if (guarded) handler.ConnectCallback = async (ctx, ct) => await ConnectAsync(ctx.DnsEndPoint.Host, ctx.DnsEndPoint.Port, ct);
		return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
	}

	/// <summary>A JSON API or a service's page (fixed hosts Panda knows).</summary>
	public static Task<string> GetApiAsync(string url, CancellationToken ct, bool browser = false) =>
		ReadAsync(Api, Rewrite(url), browser ? BrowserAgent : PandaAgent, ct);

	/// <summary>Set by the tests only; says so in the log.</summary>
	public static bool Rewriting => Rewrites.Length > 0;

	// The whole answer, not just its head, has to come in time (a page may send a byte now and then).
	static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

	/// <summary>A page someone linked: its final address (after redirects), type and text.</summary>
	public static async Task<(Uri Url, string Type, string Text)> GetPageAsync(string url, CancellationToken ct)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
		deadline.CancelAfter(Deadline);
		using var req = new HttpRequestMessage(HttpMethod.Get, url);
		req.Headers.UserAgent.ParseAdd(BrowserAgent);
		req.Headers.AcceptLanguage.ParseAdd("en;q=0.9,*;q=0.5");
		using var res = await Links.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
		res.EnsureSuccessStatusCode();
		var type = res.Content.Headers.ContentType?.MediaType ?? "";
		var text = type.StartsWith("text/") || type.Contains("html") || type.Contains("xml") || type.Contains("json")
			? await ReadLimitedAsync(res, deadline.Token) : "";
		return (res.RequestMessage?.RequestUri ?? new Uri(url), type, text);
	}

	static async Task<string> ReadAsync(HttpClient http, string url, string agent, CancellationToken ct)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
		deadline.CancelAfter(Deadline);
		using var req = new HttpRequestMessage(HttpMethod.Get, url);
		req.Headers.UserAgent.ParseAdd(agent);
		using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
		res.EnsureSuccessStatusCode();
		return await ReadLimitedAsync(res, deadline.Token);
	}

	static async Task<string> ReadLimitedAsync(HttpResponseMessage res, CancellationToken ct)
	{
		await using var body = await res.Content.ReadAsStreamAsync(ct);
		var buffer = new MemoryStream();
		var chunk = new byte[16 * 1024];
		int n;
		while ((n = await body.ReadAsync(chunk, ct)) > 0 && buffer.Length < MaxBody)
			buffer.Write(chunk, 0, n);
		var charset = res.Content.Headers.ContentType?.CharSet;
		Encoding enc;
		try { enc = charset is { Length: > 0 } ? Encoding.GetEncoding(charset.Trim('"')) : Encoding.UTF8; }
		catch (ArgumentException) { enc = Encoding.UTF8; }
		return enc.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
	}

	/// <summary>Throws <see cref="PrivateLinkException"/> when the link's host is in the local network.</summary>
	public static async Task CheckAsync(Uri url, CancellationToken ct)
	{
		if (AllowLocal) return;
		foreach (var a in await ResolveAsync(url.Host, ct))
			if (IsPrivate(a)) throw new PrivateLinkException();
	}

	static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
	{
		if (IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return [ip];
		try { return await Dns.GetHostAddressesAsync(host, ct); }
		catch (SocketException) { throw new HttpRequestException($"{host}: no such host"); }
	}

	/// <summary>Opens a TCP connection to the first allowed address of the host.</summary>
	public static async Task<Stream> ConnectAsync(string host, int port, CancellationToken ct)
	{
		var addresses = await ResolveAsync(host, ct);
		if (!AllowLocal && addresses.Any(IsPrivate)) throw new PrivateLinkException();
		var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
			timeout.CancelAfter(TimeSpan.FromSeconds(10));
			await socket.ConnectAsync(addresses, port, timeout.Token);
			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	/// <summary>Anything but a public address: loopback, private, link-local, carrier-grade NAT, unique-local,
	/// multicast and reserved ranges; IPv4 inside IPv6 (mapped, compatible, NAT64, 6to4) is looked at as IPv4.</summary>
	public static bool IsPrivate(IPAddress a)
	{
		if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
		if (IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any)) return true;
		var b = a.GetAddressBytes();
		if (a.AddressFamily == AddressFamily.InterNetworkV6)
		{
			if (a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6UniqueLocal || a.IsIPv6Multicast) return true;
			// ::a.b.c.d (compatible) and 64:ff9b::a.b.c.d (NAT64) carry IPv4 in the last four bytes, 2002:a.b.c.d:: (6to4) after the first two.
			if (b.Take(12).All(x => x == 0) || (b[0] == 0 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b && b.Skip(4).Take(8).All(x => x == 0)))
				return IsPrivate(new IPAddress(b[12..]));
			if (b[0] == 0x20 && b[1] == 0x02) return IsPrivate(new IPAddress(b[2..6]));
			return false;
		}
		return b[0] is 10 or 0 or >= 224 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168)
			|| (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] is >= 64 and <= 127)
			|| (b[0] == 192 && b[1] == 0 && b[2] == 0) || (b[0] == 198 && b[1] is 18 or 19);
	}

	/// <summary>og:title, twitter:title or the page's &lt;title&gt;, unescaped; empty when there is none.</summary>
	public static string PageTitle(string html)
	{
		foreach (var name in new[] { "og:title", "twitter:title" })
			if (Meta(html, name) is { Length: > 0 } t) return t;
		var m = System.Text.RegularExpressions.Regex.Match(html, @"<title[^>]*>([^<]{1,500})</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		return m.Success ? Clean(m.Groups[1].Value) : "";
	}

	/// <summary>The content of &lt;meta property|name="name" content="..."&gt;, either attribute order.</summary>
	public static string? Meta(string html, string name)
	{
		var n = System.Text.RegularExpressions.Regex.Escape(name);
		var m = System.Text.RegularExpressions.Regex.Match(html,
			$@"<meta[^>]+(?:property|name)\s*=\s*[""']{n}[""'][^>]*?content\s*=\s*(?:""([^""]*)""|'([^']*)')"
			+ $@"|<meta[^>]+content\s*=\s*(?:""([^""]*)""|'([^']*)')[^>]*?(?:property|name)\s*=\s*[""']{n}[""']",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		if (!m.Success) return null;
		var v = m.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value ?? "";
		return Clean(v);
	}

	static string Clean(string s) =>
		System.Text.RegularExpressions.Regex.Replace(WebUtility.HtmlDecode(s), @"\s+", " ").Trim().Trim('‎', '‏').Trim();
}

/// <summary>
/// A small HTTP proxy on 127.0.0.1 that yt-dlp and ffmpeg use for links people send: every connection
/// they make (redirects, HLS pieces, a name that resolves differently the second time) goes through
/// <see cref="Web.ConnectAsync"/>, so none of them reaches the local network. CONNECT for https, plain
/// GET with a full URL for http. Not used when local links are allowed.
/// </summary>
public static class LinkProxy
{
	static readonly Lazy<string?> Address = new(Start);

	/// <summary>"http://127.0.0.1:port", or null when links may go anywhere. When the proxy couldn't start,
	/// an address where nothing listens: links fail rather than go unguarded.</summary>
	public static string? Url => Web.AllowLocalLinks() ? null : Address.Value ?? "http://127.0.0.1:9";

	static string? Start()
	{
		try
		{
			var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			_ = Task.Run(async () =>
			{
				while (true)
				{
					try
					{
						var client = await listener.AcceptTcpClientAsync();
						_ = Task.Run(() => ServeAsync(client));
					}
					catch (SocketException) { await Task.Delay(100); }
				}
			});
			return $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
		}
		catch (SocketException) { return null; }
	}

	static async Task ServeAsync(TcpClient client)
	{
		using var _ = client;
		var local = client.GetStream();
		Stream? remote = null;
		try
		{
			using var head = new CancellationTokenSource(TimeSpan.FromSeconds(15));
			var request = await ReadHeadAsync(local, head.Token);
			var lines = request.Split("\r\n");
			var parts = lines[0].Split(' ');
			if (parts.Length != 3) return;
			byte[] forward;
			if (parts[0] == "CONNECT")
			{
				var hp = parts[1].Split(':');
				if (hp.Length != 2 || !int.TryParse(hp[1], out var port) || port is not (443 or 80 or > 1023)) { await Reply(local, "400 Bad Request"); return; }
				remote = await Web.ConnectAsync(hp[0], port, head.Token);
				// Nothing but the status: ffmpeg reads a Content-Length here as the end of the tunnel.
				await local.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"), head.Token);
				forward = [];
			}
			else
			{
				if (!Uri.TryCreate(parts[1], UriKind.Absolute, out var url) || url.Scheme != "http") { await Reply(local, "400 Bad Request"); return; }
				remote = await Web.ConnectAsync(url.IdnHost, url.Port, head.Token);
				// The request as the server wants it: its own path, no proxy headers, one request per connection.
				var kept = lines.Skip(1).Where(l => l.Length > 0 && !l.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase)
					&& !l.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase));
				forward = Encoding.Latin1.GetBytes($"{parts[0]} {url.PathAndQuery} {parts[2]}\r\n" + string.Join("\r\n", kept) + "\r\nConnection: close\r\n\r\n");
			}
			await remote.WriteAsync(forward);
			// Both ways until either side closes.
			var up = local.CopyToAsync(remote);
			var down = remote.CopyToAsync(local);
			await Task.WhenAny(up, down);
		}
		catch (PrivateLinkException) { try { await Reply(local, "403 Forbidden"); } catch { } }
		catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or HttpRequestException)
		{
			try { await Reply(local, "502 Bad Gateway"); } catch { }
		}
		finally { remote?.Dispose(); }
	}

	static Task Reply(Stream s, string status) =>
		s.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")).AsTask();

	static async Task<string> ReadHeadAsync(Stream s, CancellationToken ct)
	{
		var head = new List<byte>();
		var one = new byte[1];
		while (head.Count < 32 * 1024 && await s.ReadAsync(one, ct) == 1)
		{
			head.Add(one[0]);
			var n = head.Count;
			if (n >= 4 && head[n - 1] == '\n' && head[n - 2] == '\r' && head[n - 3] == '\n' && head[n - 4] == '\r') break;
		}
		return Encoding.Latin1.GetString(head.ToArray()).TrimEnd('\r', '\n');
	}
}

/// <summary>
/// One HTTP GET, done by hand: radio servers (old Shoutcast) answer "ICY 200 OK", which HttpClient
/// refuses, and their song titles come mixed into the audio (Icy-MetaData). Follows up to five redirects.
/// </summary>
public sealed class HttpStream : IDisposable
{
	public Uri Url { get; private set; }
	public int Status { get; private set; }
	public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Stream Body { get; private set; } = Stream.Null;

	HttpStream(Uri url) { Url = url; }

	public string Type => Headers.TryGetValue("content-type", out var t) ? t.Split(';')[0].Trim().ToLowerInvariant() : "";
	public long? Length => Headers.TryGetValue("content-length", out var l) && long.TryParse(l, out var n) ? n : null;
	public int MetaInterval => Headers.TryGetValue("icy-metaint", out var m) && int.TryParse(m, out var n) && n is > 0 and <= 1_000_000 ? n : 0;
	public bool IsIcy => Headers.Keys.Any(k => k.StartsWith("icy-", StringComparison.OrdinalIgnoreCase));

	public static async Task<HttpStream> OpenAsync(string url, CancellationToken ct, bool icyMeta = true)
	{
		var uri = new Uri(url);
		for (var redirects = 0; ; redirects++)
		{
			var s = new HttpStream(uri);
			try { await s.ConnectAsync(icyMeta, ct); }
			catch { s.Dispose(); throw; }
			if (s.Status is >= 300 and < 400 && s.Headers.TryGetValue("location", out var to) && redirects < 5
				&& Uri.TryCreate(uri, to, out var next) && next.Scheme is "http" or "https")
			{
				s.Dispose();
				uri = next;
				continue;
			}
			if (s.Status is < 200 or >= 300)
			{
				var status = s.Status;
				s.Dispose();
				throw new HttpRequestException($"HTTP {status}");
			}
			return s;
		}
	}

	async Task ConnectAsync(bool icyMeta, CancellationToken ct)
	{
		if (Url.Scheme is not ("http" or "https")) throw new HttpRequestException("not a web link");
		Stream stream = await Web.ConnectAsync(Url.IdnHost, Url.Port, ct);
		Body = stream;
		if (Url.Scheme == "https")
		{
			var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
			Body = ssl;
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
			timeout.CancelAfter(TimeSpan.FromSeconds(10));
			await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = Url.IdnHost }, timeout.Token);
		}
		var host = Url.IsDefaultPort ? Url.IdnHost : $"{Url.IdnHost}:{Url.Port}";
		var request = $"GET {Url.PathAndQuery} HTTP/1.0\r\nHost: {host}\r\nUser-Agent: {Web.PandaAgent}\r\nAccept: */*\r\n"
			+ (icyMeta ? "Icy-MetaData: 1\r\n" : "") + "Connection: close\r\n\r\n";
		using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
		{
			timeout.CancelAfter(TimeSpan.FromSeconds(15));
			await Body.WriteAsync(Encoding.ASCII.GetBytes(request), timeout.Token);
			await Body.FlushAsync(timeout.Token);
			ParseHead(await ReadHeadAsync(timeout.Token));
		}
	}

	// Up to the empty line; one byte at a time, so the audio after it stays in the stream.
	async Task<string> ReadHeadAsync(CancellationToken ct)
	{
		var head = new List<byte>();
		var one = new byte[1];
		while (head.Count < 16 * 1024)
		{
			if (await Body.ReadAsync(one, ct) == 0) break;
			head.Add(one[0]);
			var n = head.Count;
			if (n >= 2 && head[n - 1] == '\n' && (head[n - 2] == '\n' || (n >= 4 && head[n - 2] == '\r' && head[n - 3] == '\n')))
				break;
		}
		return Encoding.Latin1.GetString(head.ToArray());
	}

	void ParseHead(string head)
	{
		var lines = head.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
		var status = lines.FirstOrDefault()?.Split(' ', 3) ?? [];
		// "HTTP/1.1 200 OK" or "ICY 200 OK"
		if (status.Length < 2 || !(status[0].StartsWith("HTTP/") || status[0] == "ICY") || !int.TryParse(status[1], out var code))
			throw new HttpRequestException("not an HTTP answer");
		Status = code;
		foreach (var line in lines.Skip(1))
		{
			var colon = line.IndexOf(':');
			if (colon > 0) Headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
		}
	}

	/// <summary>Reads up to <paramref name="max"/> bytes of the body as text (a playlist file).</summary>
	public async Task<string> ReadTextAsync(int max, CancellationToken ct)
	{
		var buffer = new byte[max];
		var total = 0;
		int n;
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(10));
		while (total < max && (n = await Body.ReadAsync(buffer.AsMemory(total), timeout.Token)) > 0) total += n;
		return Encoding.UTF8.GetString(buffer, 0, total);
	}

	public void Dispose()
	{
		try { Body.Dispose(); } catch { }
	}
}
