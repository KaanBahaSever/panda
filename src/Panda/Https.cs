using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Panda;

public sealed class HttpsSettings
{
	/// <summary>The panel speaks HTTPS itself, on its own port (no plain HTTP there any more).</summary>
	public bool Enabled { get; set; }
	/// <summary>PEM certificate and key (a Cloudflare Origin Certificate, Let's Encrypt…); a relative path is in
	/// the data folder. When neither file is there under these default names, Panda makes a self-signed pair.</summary>
	public string CertFile { get; set; } = DefaultCert;
	public string KeyFile { get; set; } = DefaultKey;
	/// <summary>Strict-Transport-Security for a year: browsers then use HTTPS for this name on every port, also
	/// after the panel's HTTPS is turned off again. Off: Cloudflare can send it too.</summary>
	public bool Hsts { get; set; }

	public const string DefaultCert = "cert.pem", DefaultKey = "key.pem";
}

/// <summary>
/// The panel's own certificate (panel.https), sent with the rest of its file (the intermediates of a
/// fullchain.pem). Read again when its files change (a renewed Let's Encrypt certificate), at most once a
/// minute; a pair that can't be read then keeps the old one.
/// </summary>
public sealed class PanelCertificate
{
	readonly string certFile, keyFile;
	readonly object gate = new();
	X509Certificate2 current;
	SslStreamCertificateContext context;
	DateTime certTime, keyTime, checkedAt;

	public ILogger? Log { get; set; }

	PanelCertificate(string certFile, string keyFile)
	{
		this.certFile = certFile;
		this.keyFile = keyFile;
		(certTime, keyTime) = (File.GetLastWriteTimeUtc(certFile), File.GetLastWriteTimeUtc(keyFile));
		(current, context) = Read(certFile, keyFile);
		checkedAt = DateTime.UtcNow;
	}

	/// <summary>What TLS sends: the certificate and its chain.</summary>
	public SslStreamCertificateContext Context { get { lock (gate) { CheckFiles(); return context; } } }

	/// <summary>The certificate of the settings; <paramref name="made"/> when there was none and Panda made one.
	/// Throws with a message for people when the files can't be used.</summary>
	public static PanelCertificate Load(HttpsSettings s, string dataDir, PanelSettings panel, out bool made)
	{
		var cert = Path.GetFullPath(s.CertFile, dataDir);
		var key = Path.GetFullPath(s.KeyFile, dataDir);
		made = false;
		try
		{
			// Only under the default names: a file named in the settings that isn't there (a typo, a folder
			// Panda may not read, like certbot's) is a mistake to report, not a place for a certificate.
			if (!File.Exists(cert) && !File.Exists(key) && s.CertFile == HttpsSettings.DefaultCert && s.KeyFile == HttpsSettings.DefaultKey)
			{
				MakeSelfSigned(cert, key, panel);
				made = true;
			}
			foreach (var f in new[] { cert, key })
				if (!File.Exists(f))
					throw new InvalidOperationException($"panel.https: {f} is missing or can't be read (both files are needed; " +
						$"with neither {HttpsSettings.DefaultCert} nor {HttpsSettings.DefaultKey} in the data folder Panda makes a self-signed pair)");
			return new PanelCertificate(cert, key);
		}
		catch (Exception ex) when (ex is CryptographicException or ArgumentException or IOException or UnauthorizedAccessException)
		{
			throw new InvalidOperationException($"panel.https: can't use {cert} with {key}: {ex.Message}", ex);
		}
	}

	public X509Certificate2 Current { get { lock (gate) { CheckFiles(); return current; } } }

	void CheckFiles()
	{
		if (DateTime.UtcNow - checkedAt < TimeSpan.FromMinutes(1)) return;
		checkedAt = DateTime.UtcNow;
		try
		{
			var (c, k) = (File.GetLastWriteTimeUtc(certFile), File.GetLastWriteTimeUtc(keyFile));
			if (c == certTime && k == keyTime) return;
			(current, context) = Read(certFile, keyFile);
			(certTime, keyTime) = (c, k);
			Log?.LogInformation("Panel certificate read again: {Subject}, valid until {Until:yyyy-MM-dd}, SHA-256 {Fingerprint}",
				current.Subject, current.NotAfter, Fingerprint(current));
		}
		catch (Exception ex)
		{
			// Half written (a renewal in progress): the next look a minute later tries again.
			Log?.LogWarning("Panel certificate not read again, the old one stays: {Error}", ex.Message);
		}
	}

	public static string Fingerprint(X509Certificate2 c) => string.Join(':', SHA256.HashData(c.RawData).Select(b => b.ToString("X2")));

	static (X509Certificate2, SslStreamCertificateContext) Read(string cert, string key)
	{
		using var pem = X509Certificate2.CreateFromPemFile(cert, key);   // the first certificate of the file, with the key
		if (!pem.HasPrivateKey) throw new CryptographicException("the key doesn't belong to the certificate");
		// A PEM key is ephemeral: on Windows TLS can't use it until it's loaded again from PKCS#12.
		var leaf = new X509Certificate2(pem.Export(X509ContentType.Pkcs12));
		var chain = new X509Certificate2Collection();
		chain.ImportFromPemFile(cert);
		var rest = new X509Certificate2Collection(chain.Where(c => c.Thumbprint != leaf.Thumbprint).ToArray());
		return (leaf, SslStreamCertificateContext.Create(leaf, rest, offline: true));
	}

	/// <summary>A certificate only this Panda trusts: browsers warn once; enough for Cloudflare's "Full" mode
	/// (not "Full (strict)", which wants an Origin Certificate). Named for localhost, this machine, the listen
	/// address and the public address.</summary>
	static void MakeSelfSigned(string certFile, string keyFile, PanelSettings panel)
	{
		using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var request = new CertificateRequest("CN=Panda", key, HashAlgorithmName.SHA256);
		var names = new SubjectAlternativeNameBuilder();
		var dns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost" };
		var ips = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
		try { dns.Add(Dns.GetHostName()); } catch (Exception) { }
		if (Uri.TryCreate(panel.PublicUrl, UriKind.Absolute, out var url))
		{
			if (IPAddress.TryParse(url.Host.Trim('[', ']'), out var ip)) ips.Add(ip);
			else dns.Add(url.IdnHost);
		}
		if (IPAddress.TryParse(panel.Listen, out var listen) && !listen.Equals(IPAddress.Any) && !listen.Equals(IPAddress.IPv6Any))
			ips.Add(listen);
		foreach (var d in dns) names.AddDnsName(d);
		foreach (var ip in ips) names.AddIpAddress(ip);
		request.CertificateExtensions.Add(names.Build());
		request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
		request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
		request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));   // server
		using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
		Directory.CreateDirectory(Path.GetDirectoryName(keyFile)!);
		Directory.CreateDirectory(Path.GetDirectoryName(certFile)!);
		SafeFile.Write(keyFile, key.ExportPkcs8PrivateKeyPem() + "\n", ownerOnly: true);
		SafeFile.Write(certFile, cert.ExportCertificatePem() + "\n");
	}
}

/// <summary>
/// Cloudflare in front of the panel (panel.trustCloudflare): a request from Cloudflare's own addresses names
/// the visitor in CF-Connecting-IP. Not X-Forwarded-For: anyone's Cloudflare Worker reaches the panel from
/// these addresses with whatever X-Forwarded-For it likes, while CF-Connecting-IP is always Cloudflare's.
/// </summary>
public static class Cloudflare
{
	/// <summary>The visitor's address as Cloudflare says, when the request comes from Cloudflare.</summary>
	public static IPAddress? Visitor(IPAddress? from, string? connectingIp) =>
		from != null && Owns(from) && IPAddress.TryParse(connectingIp, out var visitor) ? visitor : null;

	static bool Owns(IPAddress a)
	{
		if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
		foreach (var n in Parsed) if (n.Contains(a)) return true;
		return false;
	}

	static readonly System.Net.IPNetwork[] Parsed = Networks().Select(n => System.Net.IPNetwork.Parse(n)).ToArray();

	/// <summary>https://www.cloudflare.com/ips/, 2026-11.</summary>
	static string[] Networks() =>
	[
		"173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18",
		"190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
		"104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
		"2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32",
	];
}
