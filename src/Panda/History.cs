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
	public string? Source { get; init; }
	public string? Link { get; init; }
	public string? Art { get; init; }
	public string Url => Media.Url(Id, Source, Link);
	public string? Thumbnail => Media.Thumbnail(Id, Source, Art);
	public bool IsLive => Duration <= 0;

	public Track ToTrack() => new(Id, Title, Channel, Duration, RequestedBy) { Source = Source, Link = Link, Art = Art };
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

	/// <param name="dir">The bot's folder.</param>
	public PlayHistory(string dir, ILogger<PlayHistory> log)
	{
		this.log = log;
		path = Path.Combine(dir, "history.jsonl");
		var all = ReadAll();
		for (int i = all.Count - 1; i >= 0 && recent.Count < RecentSize; i--)
			recent.Add(all[i]);
	}

	/// <summary>Plays added since Panda started; the panel refreshes "All bots" statistics when it moves.</summary>
	public long Added { get; private set; }

	public IReadOnlyList<PlayedTrack> Recent()
	{
		lock (gate) return recent.ToArray();
	}

	public void Add(PlayedTrack played)
	{
		lock (gate)
		{
			Added++;
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
		try
		{
			if (!File.Exists(path)) return list;
			// No lock: a play being appended right now is at most a cut-off last line, skipped below.
			using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8);
			while (reader.ReadLine() is { } line)
			{
				if (line.Length == 0) continue;
				try
				{
					if (JsonSerializer.Deserialize<PlayedTrack>(line, JsonOpts) is { Id.Length: > 0 } p)
						list.Add(p);
				}
				catch (JsonException) { }
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			log.LogWarning("Could not read {Path}: {Error}", path, ex.Message);
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
