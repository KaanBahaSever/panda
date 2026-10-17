// Chat test: joins a TeamSpeak test server next to Panda, runs scenario files and checks Panda's replies.
// usage: chattest <ts server> <panel url> <scenario>...    (panel login: admin / $PANDA_TEST_PASSWORD, default testpass123)
//
// Scenario lines (each file starts from the default settings below and a !stop):
//   # text            heading, printed
//   c <text>          send to the channel, collect replies          p <text>   same, as a private message
//   e <text>          one of the replies to the last message contains <text>
//   x                 the last message got no reply
//   w <seconds>       wait (replies that come meanwhile count for the last message)
//   s key=value ...   change bot 1's settings through the panel (values: true/false, numbers, text)
//   d                 print Panda's description        d <text>  it contains <text>        d !  it is empty
//                     (read from the server's HTTP query when $PANDA_TEST_QUERY is "<url> <api key>", else from the client)
//   L <name> <count>  replace playlist <name> with <count> filler songs
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
	["maxQueueLength"] = "100", ["maxDurationMinutes"] = "60",
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
		await Task.Delay(2500);
	}
	string[] Got() { lock (replies) return replies.ToArray(); }
	async Task<string> Description() => await Query.DescriptionAsync(panda.Name.ToString())
		?? c.Book.Clients.GetValueOrDefault(panda.Id)?.Description ?? "";

	foreach (var file in args[2..])
	{
		Console.WriteLine($"\n=== {Path.GetFileName(file)}");
		await panel.SetAsync(defaults);
		await c.SendChannelMessage("!stop");
		await Task.Delay(1500);
		foreach (var raw in File.ReadAllLines(file))
		{
			var line = raw.TrimEnd();
			if (line.Length == 0) continue;
			var arg = line.Length > 2 ? line[2..] : "";
			switch (line[0])
			{
			case '#': Console.WriteLine(line); break;
			case 'c': await Send(false, arg); break;
			case 'p': await Send(true, arg); break;
			case 'w': await Task.Delay(TimeSpan.FromSeconds(double.Parse(arg, CultureInfo.InvariantCulture))); break;
			case 'e': Check(Got().Any(g => g.Contains(arg)), $"reply has \"{arg}\""); break;
			case 'x': Check(Got().Length == 0, "no reply"); break;
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
				var parts = arg.Split(' ');
				await panel.FillPlaylistAsync(parts[0], int.Parse(parts[1]));
				break;
			default: Console.WriteLine($"?? unknown line: {line}"); failed++; break;
			}
		}
	}
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
