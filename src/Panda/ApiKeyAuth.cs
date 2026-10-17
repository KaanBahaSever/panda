using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Panda;

/// <summary>
/// Logs in a program that sends an API key ("Authorization: Bearer pk_..." or "X-Api-Key: pk_...")
/// as the key's role. Requests without a key go to the panel's cookie login.
/// </summary>
public sealed class ApiKeyAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, PandaConfig config)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string Name = "ApiKey";

	/// <summary>The key a request carries, or null.</summary>
	public static string? KeyOf(HttpRequest request)
	{
		var auth = request.Headers.Authorization.ToString();
		if (auth.StartsWith("Bearer pk_", StringComparison.Ordinal)) return auth["Bearer ".Length..].Trim();
		var header = request.Headers["X-Api-Key"].ToString();
		return header.StartsWith("pk_", StringComparison.Ordinal) ? header.Trim() : null;
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (KeyOf(Request) is not { } secret) return Task.FromResult(AuthenticateResult.NoResult());
		if (config.FindApiKey(secret) is not { } key) return Task.FromResult(AuthenticateResult.Fail("unknown key"));
		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.Name, "api:" + key.Name), new Claim(ClaimTypes.Role, key.Role), new Claim("apikey", key.Id)], Name);
		return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
	}

	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		Response.StatusCode = 401;
		return Task.CompletedTask;
	}

	protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
	{
		Response.StatusCode = 403;
		return Task.CompletedTask;
	}
}
