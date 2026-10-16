using System.Text.Json;

namespace Panda;

public sealed record PlaylistSong(string Id, string Title, string Channel, int Duration)
{
	public string Url => $"https://www.youtube.com/watch?v={Id}";
	public string Thumbnail => $"https://i.ytimg.com/vi/{Id}/hqdefault.jpg";
	public bool IsLive => Duration <= 0;

	public static PlaylistSong From(Track t) => new(t.Id, t.Title, t.Channel, t.Duration);
	public Track ToTrack(string requestedBy) => new(Id, Title, Channel, Duration, requestedBy);
}

public sealed record Playlist(string Name, IReadOnlyList<PlaylistSong> Songs, DateTimeOffset Updated);

/// <summary>Saved playlists, shared by all bots, in playlists.json in the data folder.</summary>
public sealed class Playlists
{
	public const int MaxSongs = 500;

	readonly string path;
	readonly ILogger<Playlists> log;
	readonly object gate = new();
	readonly object saveLock = new();
	List<Playlist> lists = [];

	public event Action? Changed;

	public Playlists(DataPaths paths, ILogger<Playlists> log)
	{
		this.log = log;
		path = Path.Combine(paths.Data, "playlists.json");
		try
		{
			if (File.Exists(path))
				lists = JsonSerializer.Deserialize<List<Playlist>>(File.ReadAllText(path), JsonOpts) ?? [];
		}
		catch (Exception ex) when (ex is IOException or JsonException)
		{
			log.LogWarning("Could not read {Path}: {Error}", path, ex.Message);
			try { File.Copy(path, path + ".bad", overwrite: true); } catch { }
		}
	}

	public IReadOnlyList<Playlist> All() { lock (gate) return lists.ToArray(); }

	/// <summary>
	/// 1–40 characters with at least one letter or digit (lists are found by their letters, so "🔥"
	/// and "❤️" would be the same list) and no slashes (names are part of panel URLs).
	/// </summary>
	public static bool IsValidName(string name) =>
		name.Trim().Length is >= 1 and <= 40 && !name.Contains('/') && !name.Contains('\\')
		&& Library.Normalize(name).Trim().Length > 0;

	/// <summary>Exact name first (ignoring case and accents), then a name that starts with it, then one that contains it.</summary>
	public Playlist? Find(string name)
	{
		var n = Library.Normalize(name).Trim();
		if (n.Length == 0) return null;
		lock (gate)
		{
			string Key(Playlist p) => Library.Normalize(p.Name).Trim();
			return lists.FirstOrDefault(p => Key(p) == n)
				?? lists.FirstOrDefault(p => Key(p).StartsWith(n))
				?? lists.FirstOrDefault(p => Key(p).Contains(n));
		}
	}

	Playlist? ExactLocked(string name) =>
		lists.FirstOrDefault(p => Library.Normalize(p.Name).Trim() == Library.Normalize(name).Trim());

	/// <summary>Creates a list, or adds the songs to the list with that name. Returns the list.</summary>
	public Playlist Save(string name, IEnumerable<PlaylistSong> songs) => Save(name, songs, out _);

	/// <summary>Like Save; <paramref name="left"/> counts the new songs that didn't fit in the full list.</summary>
	public Playlist Save(string name, IEnumerable<PlaylistSong> songs, out int left)
	{
		Playlist list;
		left = 0;
		lock (gate)
		{
			name = name.Trim();
			var old = ExactLocked(name);
			var merged = (old?.Songs ?? []).ToList();
			foreach (var s in songs)
				if (merged.Any(x => x.Id == s.Id)) continue;
				else if (merged.Count < MaxSongs) merged.Add(s);
				else left++;
			list = new Playlist(old?.Name ?? name, merged, DateTimeOffset.UtcNow);
			lists = old is null ? [.. lists, list] : lists.Select(p => p == old ? list : p).ToList();
		}
		Persist();
		return list;
	}

	public bool Rename(string name, string newName)
	{
		lock (gate)
		{
			var old = ExactLocked(name);
			var taken = ExactLocked(newName);
			if (old is null || (taken != null && taken != old)) return false;
			lists = lists.Select(p => p == old ? old with { Name = newName.Trim(), Updated = DateTimeOffset.UtcNow } : p).ToList();
		}
		Persist();
		return true;
	}

	/// <summary>Removes the song at <paramref name="index"/>, if it is still <paramref name="id"/>.</summary>
	public bool RemoveSong(string name, int index, string id)
	{
		lock (gate)
		{
			var old = ExactLocked(name);
			if (old is null || index < 0 || index >= old.Songs.Count || old.Songs[index].Id != id) return false;
			var songs = old.Songs.ToList();
			songs.RemoveAt(index);
			lists = lists.Select(p => p == old ? old with { Songs = songs, Updated = DateTimeOffset.UtcNow } : p).ToList();
		}
		Persist();
		return true;
	}

	public bool Delete(string name)
	{
		lock (gate)
		{
			var old = ExactLocked(name);
			if (old is null) return false;
			lists = lists.Where(p => p != old).ToList();
		}
		Persist();
		return true;
	}

	void Persist()
	{
		try
		{
			// Taken in this order, the newest lists are always the ones written last.
			lock (saveLock)
			{
				string json;
				lock (gate) json = JsonSerializer.Serialize(lists, JsonOpts);
				SafeFile.Write(path, json);
			}
		}
		catch (Exception ex) { log.LogWarning("Could not save {Path}: {Error}", path, ex.Message); }
		try { Changed?.Invoke(); } catch { }
	}

	static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		IgnoreReadOnlyProperties = true,
	};
}
