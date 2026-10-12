using System.Text;
using System.Text.Json;

namespace Panda;

/// <summary>One finished play: the song, who asked for it, when it started and how long it was heard.</summary>
public sealed record PlayedTrack(
	string Id, string Title, string Channel, int Duration, string RequestedBy,
	DateTimeOffset PlayedAt,
	/// <summary>Seconds actually played (pauses and skipped parts don't count).</summary>
	int Listened)
{
	public string Url => $"https://www.youtube.com/watch?v={Id}";
	public string Thumbnail => $"https://i.ytimg.com/vi/{Id}/hqdefault.jpg";
	public bool IsLive => Duration <= 0;

	public Track ToTrack() => new(Id, Title, Channel, Duration, RequestedBy);
}

/// <summary>
/// Every play, appended to history.jsonl (one JSON object per line) so it survives restarts.
/// The panel shows the latest ones; statistics read the whole file.
/// </summary>
public sealed class PlayHistory
{
	public const int RecentSize = 30;

	readonly string path;
	readonly ILogger<PlayHistory> log;
	readonly object gate = new();
	readonly List<PlayedTrack> recent = []; // newest first

	public PlayHistory(DataPaths paths, ILogger<PlayHistory> log)
	{
		this.log = log;
		path = Path.Combine(paths.Data, "history.jsonl");
		var all = ReadAll();
		for (int i = all.Count - 1; i >= 0 && recent.Count < RecentSize; i--)
			recent.Add(all[i]);
	}

	public IReadOnlyList<PlayedTrack> Recent()
	{
		lock (gate) return recent.ToArray();
	}

	public void Add(PlayedTrack played)
	{
		lock (gate)
		{
			recent.Insert(0, played);
			if (recent.Count > RecentSize) recent.RemoveAt(recent.Count - 1);
			try
			{
				File.AppendAllText(path, JsonSerializer.Serialize(played, JsonOpts) + "\n", Utf8NoBom);
			}
			catch (Exception ex)
			{
				log.LogWarning("Could not write {Path}: {Error}", path, ex.Message);
			}
		}
	}

	/// <summary>All plays, oldest first. A line cut off by a crash is skipped.</summary>
	public List<PlayedTrack> ReadAll()
	{
		var list = new List<PlayedTrack>();
		string[] lines;
		try
		{
			lock (gate) lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8) : [];
		}
		catch (Exception ex)
		{
			log.LogWarning("Could not read {Path}: {Error}", path, ex.Message);
			return list;
		}
		foreach (var line in lines)
		{
			if (line.Length == 0) continue;
			try
			{
				if (JsonSerializer.Deserialize<PlayedTrack>(line, JsonOpts) is { Id.Length: > 0 } p)
					list.Add(p);
			}
			catch (JsonException) { }
		}
		return list;
	}

	static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

	static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		IgnoreReadOnlyProperties = true,
	};
}
