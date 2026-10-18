// Chat test: joins a TeamSpeak test server next to Panda, runs scenario files and checks Panda's replies.
// usage: chattest <ts server> <panel url> <scenario>...    (panel login: admin / $PANDA_TEST_PASSWORD, default testpass123)
//
// Scenario lines (each file starts from the default settings below and a !stop):
//   # text            heading, printed
//   c <text>          send to the channel, collect replies          p <text>   same, as a private message
//   e <text>          one of the replies to the last message contains <text>
//   x                 the last message got no reply           n <text>   none of its replies contains <text>
//   w <seconds>       wait (replies that come meanwhile count for the last message)
//   s key=value ...   change bot 1's settings through the panel (values: true/false, numbers, text, or JSON
//                     without spaces like [{"uid":"$ME"}]); $ME is the test client's unique id, $GROUP its server group;
//                     "sources.radio=false" changes one value of a group (sources, youTube)
//   A / B             the test client goes to another channel (Away) / Back to Panda's
//   d                 print Panda's description        d <text>  it contains <text>        d !  it is empty
//                     (read from the server's HTTP query when $PANDA_TEST_QUERY is "<url> <api key>", else from the client)
//   L <name> <count>  replace playlist <name> with <count> filler songs
//   y <mode> <count>  the yt-dlp stand-in fails its next <count> calls of that kind (see tests/wsl/fake-yt-dlp.sh)
//   Y <text>          the yt-dlp stand-in was called with arguments containing <text> since the scenario started
//   + <name>          another client joins Panda's channel, until the scenario ends
//   o <text>          the first of those clients sends <text> to the channel (replies count like after "c")
//   P <song>          a panel admin adds a song (it isn't the test client's own song)
//   N <text>          the song playing (its title, or what the radio station says it plays) contains <text>
//   t <min> <max>     the song playing is between <min> and <max> seconds in
//   q <count>         the queue holds <count> songs
//   g <min> <max>     the song playing is leveled by <min>…<max> dB (g - : not leveled)
//   R                 remove every bot but bot 1 (made by !create bot)
//   C <text>          a channel on the server is named <text> (from the server's HTTP query)
//   S key=value ...   change bot 1's sound (equalizer, effect, speed, eightD, crossfade) through the panel
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
	["everyoneCanUse"] = "true", ["members"] = "[]", ["memberGroups"] = "[]", ["admins"] = "[]", ["adminGroups"] = "[]", ["djs"] = "[]", ["djGroups"] = "[]", ["blocked"] = "[]",
	["commandLevels"] = "{}", ["othersSongs"] = "everyone", ["onlyFromChannel"] = "false",
	// The test's own web server (radio, pages, music services) is on 127.0.0.1.
	["sources.localLinks"] = "true", ["sources.search"] = "youtube", ["sources.fallback"] = "true", ["sources.youTubeMusic"] = "true",
	["sources.soundCloud"] = "true", ["sources.sites"] = "true", ["sources.services"] = "true", ["sources.pages"] = "true", ["sources.radio"] = "true",
	["normalizeVolume"] = "true", ["youTube.sponsorBlock"] = "false", ["youTube.searchResults"] = "5", ["youTube.hideLive"] = "false", ["youTube.hideShort"] = "false",
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
		await panel.SoundAsync(new() { ["equalizer"] = "off", ["effect"] = "off", ["speed"] = "1", ["eightD"] = "false", ["crossfade"] = "0" });
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
					var me = c.Book.Self()!;
					var filled = arg.Replace("$ME", me.Uid?.Value ?? "").Replace("$GROUP", me.ServerGroups.First().Value.ToString());
					await panel.SetAsync(filled.Split(' ', StringSplitOptions.RemoveEmptyEntries)
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
				case 'A':
					var away = c.Book.Channels.Keys.First(ch => ch != panda.Channel);
					Console.WriteLine("-> (goes to another channel)");
					await c.ClientMove(c.ClientId, away);
					await Task.Delay(1000);
					break;
				case 'B':
					Console.WriteLine("-> (back to Panda's channel)");
					await c.ClientMove(c.ClientId, panda.Channel);
					await Task.Delay(1000);
					break;
				case 'o':
					lock (replies) replies.Clear();
					Console.WriteLine($"-> [channel, from {extras[0].Client.Book.Self()?.Name}] {arg}");
					await extras[0].Scheduler.InvokeAsync(() => extras[0].Client.SendChannelMessage(arg));
					await Task.Delay(ReplyWait);
					break;
				case 'P': Console.WriteLine($"-> [panel] play {arg}"); await panel.PlayAsync(arg); await Task.Delay(1500); break;
				case 'Y': Check(FakeYtDlp.Calls().Any(l => l.Contains(arg)), $"yt-dlp was called with \"{arg}\""); break;
				case 'R': await panel.RemoveOtherBotsAsync(); break;
				case 'C':
				{
					var names = await Query.ChannelNamesAsync();
					Check(names.Contains(arg), $"a channel is named \"{arg}\" ({string.Join(", ", names)})");
					break;
				}
				case 'S':
					await panel.SoundAsync(arg.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(kv => kv.Split('=', 2)).ToDictionary(kv => kv[0], kv => kv[1]));
					await Task.Delay(500);
					break;
				case 'N':
				{
					var now = (await panel.StateAsync())["player"]?["current"];
					var text = now is null ? "" : $"{now["track"]?["title"]} {now["streamTitle"]}";
					Check(text.Contains(arg), $"playing \"{arg}\" (\"{text.Trim()}\")");
					break;
				}
				case 't':
				{
					var range = arg.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
					var at = (double?)(await panel.StateAsync())["player"]?["current"]?["position"] ?? -1;
					Check(at >= range[0] && at <= range[1], $"position {range[0]}–{range[1]} s ({at:0.0})");
					break;
				}
				case 'g':
				{
					var g = (double?)(await panel.StateAsync())["player"]?["current"]?["gain"];
					if (arg == "-") Check(g is null, $"not leveled ({g})");
					else
					{
						var range = arg.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
						Check(g >= range[0] && g <= range[1], $"leveled by {range[0]}–{range[1]} dB ({g})");
					}
					break;
				}
				case 'q':
				{
					var n = (await panel.StateAsync())["player"]?["queue"]?.AsArray().Count ?? -1;
					Check(n == int.Parse(arg), $"{arg} songs in the queue ({n})");
					break;
				}
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
		{
			JsonNode? value = v.StartsWith('[') || v.StartsWith('{') ? JsonNode.Parse(v)
				: bool.TryParse(v, out var b) ? JsonValue.Create(b)
				: int.TryParse(v, out var i) ? JsonValue.Create(i)
				: JsonValue.Create(v);
			// "sources.radio": one value inside a group.
			if (k.Split('.', 2) is [var group, var inner]) s[group]![inner] = value;
			else s[k] = value;
		}
		(await http.PutAsJsonAsync("/api/bots/1/settings", s)).EnsureSuccessStatusCode();
	}

	public async Task SoundAsync(Dictionary<string, string> values)
	{
		var body = new JsonObject();
		foreach (var (k, v) in values)
			body[k] = bool.TryParse(v, out var b) ? JsonValue.Create(b)
				: double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? JsonValue.Create(d)
				: JsonValue.Create(v);
		(await http.PutAsJsonAsync("/api/bots/1/sound", body)).EnsureSuccessStatusCode();
	}

	public async Task RemoveOtherBotsAsync()
	{
		foreach (var b in (await http.GetFromJsonAsync<JsonArray>("/api/bots"))!)
			if ((string?)b?["id"] is { } id && id != "1") await http.DeleteAsync($"/api/bots/{id}");
	}

	public async Task<JsonObject> StateAsync() =>
		await http.GetFromJsonAsync<JsonObject>("/api/bots/1/state") ?? throw new InvalidOperationException("no state");

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

	public static async Task<List<string>> ChannelNamesAsync()
	{
		if (Conf is not [var url, var key]) return [];
		using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/1/channellist");
		req.Headers.Add("x-api-key", key);
		using var res = await Http.SendAsync(req);
		var body = (await res.Content.ReadFromJsonAsync<JsonObject>())?["body"]?.AsArray() ?? [];
		return body.Select(n => (string?)n?["channel_name"] ?? "").ToList();
	}

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
