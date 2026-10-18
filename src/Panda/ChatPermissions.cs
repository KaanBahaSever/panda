namespace Panda;

/// <summary>Who someone is for the bot's chat commands. Higher can do everything lower can.</summary>
public enum ChatLevel { None, Everyone, Dj, Admin }

/// <summary>A TeamSpeak user picked in the panel: the unique id counts, the name is only for showing.</summary>
public sealed record ChatPerson(string Uid, string Name = "");

/// <summary>The chat commands whose level can be set in the panel, and the levels as text ("everyone", "dj", "admin").</summary>
public static class ChatPermissions
{
	/// <summary>Commands with a level, in the panel's order. !resume follows !pause, a pick (!1) follows !search.</summary>
	public static readonly string[] Commands =
	[
		"play", "search", "playnow", "skip", "remove", "move", "skipto", "back", "seek", "pause", "stop", "clear",
		"shuffle", "loop", "vol", "eq", "fx", "speed", "autoplay", "fm", "playlist", "fav", "myfav", "sleep", "join", "follow",
	];

	/// <summary>A sensible setup for a busy channel: adding and asking is for everyone, steering is for DJs.</summary>
	public static readonly IReadOnlyDictionary<string, string> Recommended = new Dictionary<string, string>
	{
		["play"] = "everyone", ["search"] = "everyone", ["skip"] = "everyone", ["remove"] = "everyone",
		["playlist"] = "everyone", ["fav"] = "everyone", ["myfav"] = "everyone", ["fm"] = "everyone",
		["playnow"] = "dj", ["move"] = "dj", ["skipto"] = "dj", ["back"] = "dj", ["seek"] = "dj", ["pause"] = "dj",
		["stop"] = "dj", ["clear"] = "dj", ["shuffle"] = "dj", ["loop"] = "dj", ["vol"] = "dj", ["eq"] = "dj", ["fx"] = "dj", ["speed"] = "dj", ["autoplay"] = "dj",
		["join"] = "dj", ["follow"] = "dj", ["sleep"] = "dj",
	};

	public static ChatLevel Parse(string? level) => level switch
	{
		"dj" => ChatLevel.Dj,
		"admin" => ChatLevel.Admin,
		_ => ChatLevel.Everyone,
	};

	public static string Name(ChatLevel level) => level switch
	{
		ChatLevel.Dj => "dj",
		ChatLevel.Admin => "admin",
		_ => "everyone",
	};

	public static bool IsLevel(string? level) => level is "everyone" or "dj" or "admin";

	/// <summary>The level a command needs; commands that only ask (!np, !queue, !help...) need nothing more than access.</summary>
	public static ChatLevel Required(BotSettings bot, string command)
	{
		var key = command switch { "resume" => "pause", "pick" => "search", _ => command };
		return bot.CommandLevels.TryGetValue(key, out var level) ? Parse(level) : ChatLevel.Everyone;
	}

	/// <summary>Someone's level from their unique id and server groups. Blocked people have none.</summary>
	public static ChatLevel LevelOf(BotSettings bot, string? uid, IEnumerable<ulong> groups)
	{
		if (uid != null && bot.Blocked.Any(p => p.Uid == uid)) return ChatLevel.None;
		var g = groups.ToHashSet();
		if ((uid != null && bot.Admins.Any(p => p.Uid == uid)) || bot.AdminGroups.Any(g.Contains)) return ChatLevel.Admin;
		if ((uid != null && bot.Djs.Any(p => p.Uid == uid)) || bot.DjGroups.Any(g.Contains)) return ChatLevel.Dj;
		if (bot.EveryoneCanUse || (uid != null && bot.Members.Any(p => p.Uid == uid)) || bot.MemberGroups.Any(g.Contains)) return ChatLevel.Everyone;
		return ChatLevel.None;
	}

	/// <summary>
	/// Settings from before 0.5: a list of allowed users and groups meant "only these may use the bot";
	/// they become the members and everyone else stays shut out. Vote skip on meant that cutting someone
	/// else's song short took a vote: other people's songs become a DJ thing, the others vote.
	/// </summary>
	public static void Upgrade(BotSettings bot)
	{
		// A hand-edited config.json may say null.
		bot.Admins = (bot.Admins ?? []).Where(p => p?.Uid is { Length: > 0 }).ToList();
		bot.Djs = (bot.Djs ?? []).Where(p => p?.Uid is { Length: > 0 }).ToList();
		bot.Blocked = (bot.Blocked ?? []).Where(p => p?.Uid is { Length: > 0 }).ToList();
		bot.Members = (bot.Members ?? []).Where(p => p?.Uid is { Length: > 0 }).ToList();
		bot.AdminGroups ??= [];
		bot.DjGroups ??= [];
		bot.MemberGroups ??= [];
		bot.CommandLevels ??= [];
		if (bot.AllowedUsers is { Count: > 0 } || bot.AllowedServerGroups is { Count: > 0 })
		{
			bot.EveryoneCanUse = false;
			foreach (var uid in bot.AllowedUsers ?? [])
				if (uid.Trim() is { Length: > 0 } u && bot.Members.All(p => p.Uid != u)) bot.Members.Add(new ChatPerson(u));
			foreach (var g in bot.AllowedServerGroups ?? [])
				if (!bot.MemberGroups.Contains(g)) bot.MemberGroups.Add(g);
		}
		bot.AllowedUsers = null;
		bot.AllowedServerGroups = null;
		bot.OthersSongs ??= bot.VoteSkip ? "dj" : "everyone";
		// Up to 0.5 autoplay was called radio; !fm (internet radio) gets what !radio had.
		if (bot.OldRadio is { } on) bot.Autoplay = on;
		bot.OldRadio = null;
		// 0.7: the equalizer, effects and speed are like the volume until someone sets them.
		if (bot.SettingsVersion < 7)
		{
			if (bot.CommandLevels.TryGetValue("vol", out var volLevel))
				foreach (var c in new[] { "eq", "fx", "speed" }) bot.CommandLevels.TryAdd(c, volLevel);
			bot.SettingsVersion = 7;
		}
		if (bot.CommandLevels.Remove("radio", out var radioLevel))
		{
			bot.CommandLevels.TryAdd("autoplay", radioLevel);
			bot.CommandLevels.TryAdd("fm", radioLevel);
		}
		foreach (var key in bot.CommandLevels.Keys.Where(k => !Commands.Contains(k) || !IsLevel(bot.CommandLevels[k])).ToList())
			bot.CommandLevels.Remove(key);
	}
}
