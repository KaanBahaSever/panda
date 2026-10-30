using Microsoft.Extensions.FileProviders;

namespace Panda;

/// <summary>
/// A bot's picture on TeamSpeak: one chosen in the panel (data/bots/&lt;id&gt;/avatar), else Panda's own.
/// The panel makes it small before sending; here it only has to be a PNG, JPEG or GIF of a sane size.
/// </summary>
public static class Avatar
{
	public const string FileName = "avatar";
	/// <summary>TeamSpeak servers usually allow far less (i_client_max_avatar_filesize); the panel shrinks pictures to ~100 KB.</summary>
	public const int MaxBytes = 1024 * 1024;

	static readonly Lazy<byte[]> Panda = new(() =>
	{
		var file = new ManifestEmbeddedFileProvider(typeof(Avatar).Assembly, "wwwroot").GetFileInfo("img/avatar.png");
		using var ms = new MemoryStream();
		using (var s = file.CreateReadStream()) s.CopyTo(ms);
		return ms.ToArray();
	});

	/// <summary>"image/png", "image/jpeg" or "image/gif" by what the bytes are, or null.</summary>
	public static string? TypeOf(ReadOnlySpan<byte> data) =>
		data.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]) ? "image/png"
		: data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? "image/jpeg"
		: data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8) ? "image/gif"
		: null;

	public static bool IsValid(ReadOnlySpan<byte> data) => data.Length is > 0 and <= MaxBytes && TypeOf(data) != null;

	/// <summary>The bot's own picture when there is a good one, else Panda's.</summary>
	public static byte[] Read(string path)
	{
		try
		{
			if (File.Exists(path) && File.ReadAllBytes(path) is var data && IsValid(data)) return data;
		}
		catch (IOException) { }
		return Panda.Value;
	}

	public static bool IsCustom(string path) => File.Exists(path);
}
