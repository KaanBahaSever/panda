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
		"shuffle", "loop", "vol", "radio", "playlist", "fav", "join", "follow",
	];

	/// <summary>A sensible setup for a busy channel: adding and asking is for everyone, steering is for DJs.</summary>
	public static readonly IReadOnlyDictionary<string, string> Recommended = new Dictionary<string, string>
	{
		["play"] = "everyone", ["search"] = "everyone", ["skip"] = "everyone", ["remove"] = "everyone",
		["playlist"] = "everyone", ["fav"] = "everyone",
		["playnow"] = "dj", ["move"] = "dj", ["skipto"] = "dj", ["back"] = "dj", ["seek"] = "dj", ["pause"] = "dj",
		["stop"] = "dj", ["clear"] = "dj", ["shuffle"] = "dj", ["loop"] = "dj", ["vol"] = "dj", ["radio"] = "dj",
		["join"] = "dj", ["follow"] = "dj",
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
		return bot.EveryoneCanUse ? ChatLevel.Everyone : ChatLevel.None;
	}

	/// <summary>
	/// Settings from before 0.5: a list of allowed users and groups meant "only these may use the bot".
	/// They become DJs and everyone else is shut out, so nothing changes for them. Vote skip on meant
	/// that cutting someone else's song took a vote: changing other people's songs becomes a DJ thing.
	/// </summary>
	public static void Upgrade(BotSettings bot)
	{
		if (bot.AllowedUsers is { Count: > 0 } || bot.AllowedServerGroups is { Count: > 0 })
		{
			bot.EveryoneCanUse = false;
			foreach (var uid in bot.AllowedUsers ?? [])
				if (uid.Trim() is { Length: > 0 } u && bot.Djs.All(p => p.Uid != u)) bot.Djs.Add(new ChatPerson(u));
			foreach (var g in bot.AllowedServerGroups ?? [])
				if (!bot.DjGroups.Contains(g)) bot.DjGroups.Add(g);
		}
		bot.AllowedUsers = null;
		bot.AllowedServerGroups = null;
		bot.OthersSongs ??= bot.VoteSkip ? "dj" : "everyone";
		foreach (var key in bot.CommandLevels.Keys.Where(k => !Commands.Contains(k) || !IsLevel(bot.CommandLevels[k])).ToList())
			bot.CommandLevels.Remove(key);
	}
}
