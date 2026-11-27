using System.Collections.Concurrent;
using System.Text.Json;

namespace Panda;

/// <summary>
/// Password rules for new passwords (existing accounts keep working until they change theirs):
/// length beats forced symbols, and the most common passwords are refused locally.
/// </summary>
public static class PasswordPolicy
{
	public const int MinLength = 12;
	public const int MaxLength = 256;

	/// <summary>Null when acceptable, else an error code for the panel ("password-too-short",
	/// "password-too-long" or "password-too-common").</summary>
	public static string? Validate(string? password, string? username = null)
	{
		if (password is null || password.Length < MinLength) return "password-too-short";
		if (password.Length > MaxLength) return "password-too-long";
		if (IsCommon(password, username)) return "password-too-common";
		return null;
	}

	static bool IsCommon(string password, string? username)
	{
		var trimmed = password.Trim();
		if (trimmed.Length == 0) return true;
		if (!string.IsNullOrEmpty(username) && trimmed.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
		// A single character repeated ("aaaaaaaaaaaa", "111111111111").
		var first = trimmed[0];
		if (trimmed.All(c => c == first)) return true;
		return CommonPasswords.Contains(trimmed);
	}

	static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
	{
		"123456789012", "1234567890123", "12345678901234", "123456789012345", "1234567890123456",
		"012345678901", "012345678910", "098765432109", "987654321098", "123456123456",
		"123123123123", "123412341234", "111111111111", "000000000000", "112233445566",
		"123456789101", "123456789123", "123456789abc", "123456789qwe", "1234567890ab",
		"password1234", "password12345", "password123456", "password123!", "passwordpassword",
		"passwort1234", "passwort12345", "passwort123456", "motdepasse12", "motdepasse123",
		"sifre1234567", "sifre12345678", "parola123456", "parola1234567", "admin1234567",
		"admin12345678", "administrator", "administrators", "adminpassword", "adminadmin12",
		"rootpassword", "panelpassword", "defaultpassword", "secretpassword", "changeme1234",
		"changeme12345", "qwertyuiopas", "qwertyuiop12", "qwertyuiop123", "qwerty123456",
		"qwerty1234567", "qwertzuiopas", "qwertz123456", "azertyuiop12", "azerty123456",
		"asdfghjkl123", "asdfghjkl1234", "zxcvbnm12345", "1q2w3e4r5t6y", "1qaz2wsx3edc",
		"qazwsxedcrfv", "abcdefghijkl", "abcdefghijklm", "abcdefghijklmn", "abcdefghijklmnop",
		"letmein12345", "welcome12345", "iloveyou1234", "iloveyou12345", "football1234",
		"baseball1234", "basketball12", "princess1234", "sunshine1234", "superman1234",
		"trustno11234", "monkey123456", "dragon123456", "master123456", "shadow123456",
		"michael12345", "charlie12345", "jessica12345", "ashley123456", "bailey123456",
		"starwars1234", "pokemon12345", "minecraft123", "minecraft1234", "discord12345",
		"teamspeak123", "teamspeak1234", "teamspeak6bot", "pandamusicbot", "pandabot1234",
		"musicbot1234", "correcthorsebatterystaple", "hunter2hunter2", "testpassword",
		"testpassword1", "testpassword12", "testing12345", "testing123456", "server123456",
	};
}

/// <summary>
/// Sessions ended with /api/logout: each cookie carries a random session id ("sid"), and logging out
/// puts it here until the cookie would have expired anyway. Saved to disk so a restart doesn't bring
/// a logged-out cookie back.
/// </summary>
public sealed class SessionRevocation
{
	const int MaxEntries = 4000;
	readonly string path;
	readonly object saveLock = new();
	readonly ConcurrentDictionary<string, DateTime> revoked = new(StringComparer.Ordinal);

	public SessionRevocation(DataPaths paths)
	{
		path = Path.Combine(paths.Data, "revoked-sessions.json");
		try
		{
			if (!File.Exists(path)) return;
			var loaded = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(path));
			if (loaded is null) return;
			var now = DateTime.UtcNow;
			foreach (var (sid, until) in loaded)
				if (sid.Length > 0 && until > now) revoked[sid] = until;
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
	}

	public bool IsRevoked(string? sid) =>
		!string.IsNullOrEmpty(sid) && revoked.TryGetValue(sid, out var until) && until > DateTime.UtcNow;

	public void Revoke(string sid, DateTime until)
	{
		if (string.IsNullOrEmpty(sid)) return;
		var now = DateTime.UtcNow;
		revoked[sid] = until;
		foreach (var (k, v) in revoked)
			if (v <= now) revoked.TryRemove(k, out _);
		if (revoked.Count > MaxEntries)
			foreach (var k in revoked.OrderBy(kv => kv.Value).Take(revoked.Count - MaxEntries).Select(kv => kv.Key).ToList())
				revoked.TryRemove(k, out _);
		Persist();
	}

	void Persist()
	{
		lock (saveLock)
		{
			try
			{
				var snapshot = revoked.Where(kv => kv.Value > DateTime.UtcNow).ToDictionary(kv => kv.Key, kv => kv.Value);
				SafeFile.Write(path, JsonSerializer.Serialize(snapshot), ownerOnly: true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		}
	}
}

/// <summary>
/// Guards state-changing API calls against cross-origin and same-site (another port or subdomain)
/// requests: Fetch Metadata (Sec-Fetch-Site) plus Origin / Referer matching the request Host.
/// API-key callers don't use browser cookies and pass straight through.
/// </summary>
public static class CsrfGuard
{
	public static bool IsAllowed(HttpRequest req)
	{
		if (HttpMethods.IsGet(req.Method) || HttpMethods.IsHead(req.Method) || HttpMethods.IsOptions(req.Method))
			return true;
		if (ApiKeyAuth.KeyOf(req) != null)
			return true;

		if (req.Headers["Sec-Fetch-Site"].FirstOrDefault() is { Length: > 0 } site && !site.Equals("same-origin", StringComparison.OrdinalIgnoreCase))
			return false;

		if (req.Headers.Origin.FirstOrDefault() is { Length: > 0 } origin)
			return MatchesHost(origin, req);

		if (req.Headers.Referer.FirstOrDefault() is { Length: > 0 } referer)
			return MatchesHost(referer, req);

		return true;
	}

	static bool MatchesHost(string headerUrl, HttpRequest req)
	{
		if (!Uri.TryCreate(headerUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
			return false;
		if (req.IsHttps && uri.Scheme != "https")
			return false;
		if (!req.Host.HasValue)
			return false;
		var host = req.Host.Host;
		if (!uri.IdnHost.Equals(host, StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
			return false;
		var expectedPort = req.Host.Port ?? (req.IsHttps ? 443 : (uri.Scheme == "https" ? 443 : 80));
		return uri.Port == expectedPort;
	}
}

/// <summary>Security headers sent with every panel and API answer.</summary>
public static class SecurityHeaders
{
	public const string Csp =
		"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' https: data: blob:; " +
		"connect-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";

	public static void Apply(IHeaderDictionary headers)
	{
		headers["Content-Security-Policy"] = Csp;
		headers["X-Frame-Options"] = "DENY";
		headers["X-Content-Type-Options"] = "nosniff";
		headers["Referrer-Policy"] = "no-referrer";
		headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
	}
}
