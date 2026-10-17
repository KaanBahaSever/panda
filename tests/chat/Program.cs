// Chat test: joins a TeamSpeak test server next to Panda, runs scenario files and checks Panda's replies.
// usage: chattest <ts server> <panel url> <scenario>...    (panel login: admin / $PANDA_TEST_PASSWORD, default testpass123)
//
// Scenario lines (each file starts from the default settings below and a !stop):
//   # text            heading, printed
//   c <text>          send to the channel, collect replies          p <text>   same, as a private message
//   e <text>          one of the replies to the last message contains <text>
//   x                 the last message got no reply           n <text>   none of its replies contains <text>
//   w <seconds>       wait (replies that come meanwhile count for the last message)
//   s key=value ...   change bot 1's settings through the panel (values: true/false, numbers, text)
//   d                 print Panda's description        d <text>  it contains <text>        d !  it is empty
//                     (read from the server's HTTP query when $PANDA_TEST_QUERY is "<url> <api key>", else from the client)
//   L <name> <count>  replace playlist <name> with <count> filler songs
//   y <mode> <count>  the yt-dlp stand-in fails its next <count> calls of that kind (see tests/wsl/fake-yt-dlp.sh)
//   Y <text>          the yt-dlp stand-in was called with arguments containing <text> since the scenario started
//   + <name>          another client joins Panda's channel, until the scenario ends
//   o <text>          the first of those clients sends <text> to the channel (replies count like after "c")
//   P <song>          a panel admin adds a song (it isn't the test client's own song)
// Exit code: the number of failed checks.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using TSLib;
using TSLib.Full;
using TSLib.Helper;
using TSLib.Scheduler;

if (args.Length < 3)
{
	Console.Error.WriteLine("usage: chattest <ts server> <panel url> <scenario>...");
	return 2;
}

var defaults = new Dictionary<string, string>
{
	["announce"] = "true", ["confirmCommands"] = "true", ["showSongInDescription"] = "true", ["language"] = "en",
	["maxQueueLength"] = "100", ["maxDurationMinutes"] = "60", ["maxSongsPerUser"] = "0", ["voteSkip"] = "false",
};
var panel = new Panel(args[1], Environment.GetEnvironmentVariable("PANDA_TEST_PASSWORD") ?? "testpass123");
await panel.LoginAsync();

var scheduler = new DedicatedTaskScheduler(Id.Null);
var c = new TsFullClient(scheduler);
var replies = new List<string>();
c.OnEachTextMessage += (_, m) =>
{
	if (m.InvokerId == c.ClientId) return;
	var line = $"[{m.Target}] {m.InvokerName}: {m.Message.Replace("\n", " / ")}";
	lock (replies) replies.Add(line);
	Console.WriteLine("   <- " + line);
};

// How long replies to a message are collected; they come within a few hundred ms.
var ReplyWait = TimeSpan.FromSeconds(1.8);
int failed = 0, passed = 0;
void Check(bool ok, string what)
{
	if (ok) { passed++; Console.WriteLine($"   PASS {what}"); }
	else { failed++; Console.WriteLine($"   FAIL {what}"); }
}

await scheduler.InvokeAsync(async () =>
{
	var r = await c.Connect(new ConnectionDataFull(args[0], TsCrypt.GenerateNewIdentity(8), username: "Tester"));
	if (!r.Ok) { Console.WriteLine("connect failed: " + r.Error.ErrorFormat()); failed++; return; }
	await Task.Delay(1500);
	var panda = c.Book.Clients.Values.FirstOrDefault(x => x.Name.ToString().Contains("Panda"));
	if (panda is null) { Console.WriteLine("Panda is not on the server"); failed++; return; }
	if (c.Book.Self()!.Channel != panda.Channel) await c.ClientMove(c.ClientId, panda.Channel);

	async Task Send(bool priv, string text)
	{
		lock (replies) replies.Clear();
		Console.WriteLine($"-> [{(priv ? "private" : "channel")}] {text}");
		if (priv) await c.SendPrivateMessage(text, panda.Id); else await c.SendChannelMessage(text);
		await Task.Delay(ReplyWait);
	}
	string[] Got() { lock (replies) return replies.ToArray(); }
	var extras = new List<(DedicatedTaskScheduler Scheduler, TsFullClient Client)>();
	async Task AddListener(string name)
	{
		var sch = new DedicatedTaskScheduler(Id.Null);
		var x = new TsFullClient(sch);
		var channel = panda.Channel;
		await sch.InvokeAsync(async () =>
		{
			var r = await x.Connect(new ConnectionDataFull(args[0], TsCrypt.GenerateNewIdentity(8), username: name));
			if (!r.Ok) { Console.WriteLine($"   {name} could not connect: {r.Error.ErrorFormat()}"); return; }
			await Task.Delay(1000);
			await x.ClientMove(x.ClientId, channel);
		});
		extras.Add((sch, x));
		await Task.Delay(1000);
	}
	async Task DropListeners()
	{
		foreach (var (sch, x) in extras) await Task.WhenAny(sch.InvokeAsync(() => x.Disconnect()), Task.Delay(2000));
		extras.Clear();
	}
	async Task<string> Description() => await Query.DescriptionAsync(panda.Name.ToString())
		?? c.Book.Clients.GetValueOrDefault(panda.Id)?.Description ?? "";

	foreach (var file in args[2..])
	{
		Console.WriteLine($"\n=== {Path.GetFileName(file)}");
		await DropListeners();
		await panel.SetAsync(defaults);
		FakeYtDlp.Reset();
		await c.SendChannelMessage("!stop");
		await Task.Delay(1500);
		foreach (var raw in File.ReadAllLines(file))
		{
			var line = raw.TrimEnd();
			if (line.Length == 0) continue;
			var arg = line.Length > 2 ? line[2..] : "";
			// A line that throws (the panel refused, a client is missing) fails, the rest still runs.
			try
			{
				switch (line[0])
				{
				case '#': Console.WriteLine(line); break;
				case 'c': await Send(false, arg); break;
				case 'p': await Send(true, arg); break;
				case 'w': await Task.Delay(TimeSpan.FromSeconds(double.Parse(arg, CultureInfo.InvariantCulture))); break;
				case 'e': Check(Got().Any(g => g.Contains(arg)), $"reply has \"{arg}\""); break;
				case 'x': Check(Got().Length == 0, "no reply"); break;
				case 'n': Check(!Got().Any(g => g.Contains(arg)), $"no reply has \"{arg}\""); break;
				case 's':
					await panel.SetAsync(arg.Split(' ', StringSplitOptions.RemoveEmptyEntries)
						.Select(kv => kv.Split('=', 2)).ToDictionary(kv => kv[0], kv => kv[1]));
					await Task.Delay(1000);
					break;
				case 'd':
					var desc = await Description();
					if (arg.Length == 0) Console.WriteLine($"   description: \"{desc}\"");
					else if (arg == "!") Check(desc.Length == 0, $"description is empty (\"{desc}\")");
					else Check(desc.Contains(arg), $"description has \"{arg}\" (\"{desc}\")");
					break;
				case 'L':
					var cut = arg.LastIndexOf(' ');   // the name may have spaces; the count comes last
					await panel.FillPlaylistAsync(arg[..cut], int.Parse(arg[(cut + 1)..]));
					break;
				case 'y': FakeYtDlp.Fail(arg); break;
				case '+': await AddListener(arg); break;
				case 'o':
					lock (replies) replies.Clear();
					Console.WriteLine($"-> [channel, from {extras[0].Client.Book.Self()?.Name}] {arg}");
					await extras[0].Scheduler.InvokeAsync(() => extras[0].Client.SendChannelMessage(arg));
					await Task.Delay(ReplyWait);
					break;
				case 'P': Console.WriteLine($"-> [panel] play {arg}"); await panel.PlayAsync(arg); await Task.Delay(1500); break;
				case 'Y': Check(FakeYtDlp.Calls().Any(l => l.Contains(arg)), $"yt-dlp was called with \"{arg}\""); break;
				default: Console.WriteLine($"?? unknown line: {line}"); failed++; break;
				}			}
			catch (Exception ex) { Check(false, $"{line}: {ex.GetType().Name}: {ex.Message}"); }

		}
	}
	await DropListeners();
	await Task.WhenAny(c.Disconnect(), Task.Delay(3000));
});

Console.WriteLine($"\n{passed} passed, {failed} failed");
// The scheduler's thread would keep the process alive.
Environment.Exit(failed);
return failed;

sealed class Panel(string url, string password)
{
	readonly HttpClient http = new(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(url) };

	public async Task LoginAsync() =>
		(await http.PostAsJsonAsync("/api/login", new { name = "admin", password })).EnsureSuccessStatusCode();

	public async Task SetAsync(IReadOnlyDictionary<string, string> values)
	{
		var s = await http.GetFromJsonAsync<JsonObject>("/api/bots/1/settings") ?? throw new InvalidOperationException("no settings");
		foreach (var (k, v) in values)
			s[k] = bool.TryParse(v, out var b) ? JsonValue.Create(b)
				: int.TryParse(v, out var i) ? JsonValue.Create(i)
				: JsonValue.Create(v);
		(await http.PutAsJsonAsync("/api/bots/1/settings", s)).EnsureSuccessStatusCode();
	}

	public async Task PlayAsync(string query) =>
		(await http.PostAsJsonAsync("/api/bots/1/play", new { query })).EnsureSuccessStatusCode();

	public async Task FillPlaylistAsync(string name, int count)
	{
		await http.DeleteAsync($"/api/playlists/{Uri.EscapeDataString(name)}");
		var songs = Enumerable.Range(0, count).Select(i => new { id = $"fill{i:D7}", title = $"Filler {i}", channel = "test", duration = 60 });
		(await http.PostAsJsonAsync("/api/playlists", new { name, songs })).EnsureSuccessStatusCode();
	}
}

static class Query
{
	static readonly string[]? Conf = Environment.GetEnvironmentVariable("PANDA_TEST_QUERY")?.Split(' ', 2);
	static readonly HttpClient Http = new();

	/// <summary>The description of the client with that nickname, as the server has it; null without a query setup.</summary>
	public static async Task<string?> DescriptionAsync(string nickname)
	{
		if (Conf is not [var url, var key]) return null;
		async Task<JsonArray> Get(string cmd)
		{
			using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/1/{cmd}");
			req.Headers.Add("x-api-key", key);
			using var res = await Http.SendAsync(req);
			return (await res.Content.ReadFromJsonAsync<JsonObject>())?["body"]?.AsArray() ?? [];
		}
		var clid = (await Get("clientlist")).FirstOrDefault(n => (string?)n?["client_nickname"] == nickname)?["clid"]?.ToString();
		if (clid is null) return "";
		return (string?)(await Get($"clientinfo?clid={clid}")).FirstOrDefault()?["client_description"] ?? "";
	}
}

static class FakeYtDlp
{
	static readonly string? Dir = Environment.GetEnvironmentVariable("FAKE_YTDLP_DIR");

	public static void Fail(string modeAndCount) => File.WriteAllText(Path.Combine(Dir!, "fail"), modeAndCount + "\n");

	public static void Reset()
	{
		if (Dir is null) return;
		File.WriteAllText(Path.Combine(Dir, "fail"), "");
		File.WriteAllText(Path.Combine(Dir, "calls"), "");
	}

	public static string[] Calls() => Dir is null ? [] : File.ReadAllLines(Path.Combine(Dir, "calls"));
}
