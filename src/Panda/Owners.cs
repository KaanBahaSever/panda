using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Panda;

/// <summary>
/// The links !panel sends a bot owner to make their panel account (<see cref="Roles.Owner"/>): a random 256-bit
/// key in the address's # part (browsers never send that part; the panel sends the key in a POST body), good once,
/// for 10 minutes. A new link for the same person ends the one before. Kept in memory only: a restart ends them all.
/// </summary>
public sealed class OwnerLinks
{
	public static readonly TimeSpan Life = TimeSpan.FromMinutes(10);
	readonly ConcurrentDictionary<string, Link> links = new();

	public sealed record Link(string Uid, string Name, DateTime Until);

	/// <summary>A new link's key for this person (their TeamSpeak unique id and name).</summary>
	public string Make(string uid, string name)
	{
		var now = DateTime.UtcNow;
		foreach (var (key, old) in links)
			if (old.Uid == uid || old.Until < now) links.TryRemove(key, out _);
		var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		links[token] = new Link(uid, name, now + Life);
		return token;
	}

	/// <summary>Whose link it is, while it's good.</summary>
	public Link? Peek(string token) =>
		token.Length is > 0 and <= 100 && links.TryGetValue(token, out var l) && l.Until > DateTime.UtcNow ? l : null;

	/// <summary>Uses the link up; false when it's gone (used, ended, or never was).</summary>
	public bool Take(string token) => Peek(token) != null && links.TryRemove(token, out _);

	/// <summary>Puts a link used up for nothing back (its time doesn't start again).</summary>
	public void GiveBack(string token, Link link)
	{
		if (link.Until > DateTime.UtcNow && !links.Values.Any(l => l.Uid == link.Uid)) links.TryAdd(token, link);
	}
}
