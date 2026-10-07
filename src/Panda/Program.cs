// Panda 🐼 — a tiny YouTube music bot for TeamSpeak 6.
// Licensed under the Open Software License version 3.0.
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Panda;

Console.OutputEncoding = System.Text.Encoding.UTF8; // emoji in logs on Windows consoles

var dataDir = Path.GetFullPath(
	ArgValue(args, "--data") ?? Environment.GetEnvironmentVariable("PANDA_DATA") ?? "data");
Directory.CreateDirectory(dataDir);
var configPath = Path.Combine(dataDir, "config.json");
var config = PandaConfig.Load(configPath);

switch (args.FirstOrDefault(a => !a.StartsWith("--") && a != ArgValue(args, "--data")))
{
case "version":
	Console.WriteLine(PanelApi.Version);
	return 0;
case "set-password":
{
	var pw = args.SkipWhile(a => a != "set-password").Skip(1).FirstOrDefault();
	if (pw is null || pw.Length < 6)
	{
		Console.Error.WriteLine("usage: panda set-password <password>   (at least 6 characters)");
		return 1;
	}
	config.Panel.PasswordHash = PasswordHasher.Hash(pw);
	config.Save();
	Console.WriteLine("Panel password updated.");
	return 0;
}
case null:
	break;
default:
	Console.Error.WriteLine("usage: panda [--data <dir>] [version | set-password <password>]");
	return 1;
}

// First start: write a config with defaults and a random panel password.
if (config.Panel.PasswordHash.Length == 0)
{
	var pw = PasswordHasher.Generate();
	config.Panel.PasswordHash = PasswordHasher.Hash(pw);
	Console.WriteLine($"""

		  🐼  Panda is starting for the first time.
		      Panel password: {pw}
		      (change it in the panel under Settings)

		""");
}
var cookies = Path.Combine(dataDir, "cookies.txt");
if (config.YouTube.CookiesFile.Length == 0 && File.Exists(cookies))
	config.YouTube.CookiesFile = cookies;
config.Save();

TSLibLogging.Configure();

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = dataDir });
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
// Keys live in the data folder, which is already private to the panda user.
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);
builder.WebHost.UseUrls($"http://{config.Panel.Listen}:{config.Panel.Port}");

builder.Services.ConfigureHttpJsonOptions(o =>
	o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(new DataPaths(dataDir));
builder.Services.AddSingleton<YouTube>();
builder.Services.AddSingleton<Player>();
builder.Services.AddSingleton<TsBot>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TsBot>());

builder.Services.AddDataProtection()
	.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
	.SetApplicationName("panda");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(o =>
	{
		o.Cookie.Name = "panda_session";
		o.Cookie.HttpOnly = true;
		o.Cookie.SameSite = SameSiteMode.Strict;
		o.ExpireTimeSpan = TimeSpan.FromDays(30);
		o.SlidingExpiration = true;
		o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
		o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
	});
builder.Services.AddAuthorization();

var app = builder.Build();

var panelFiles = new ManifestEmbeddedFileProvider(typeof(PanelApi).Assembly, "wwwroot");
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = panelFiles });
app.UseStaticFiles(new StaticFileOptions { FileProvider = panelFiles });
app.UseAuthentication();
app.UseAuthorization();
PanelApi.Map(app);

app.Logger.LogInformation("🐼 Panda {Version} — panel on http://{Listen}:{Port}, data in {Data}",
	PanelApi.Version, config.Panel.Listen, config.Panel.Port, dataDir);
await app.RunAsync();
return 0;

static string? ArgValue(string[] args, string name)
{
	var i = Array.IndexOf(args, name);
	return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
