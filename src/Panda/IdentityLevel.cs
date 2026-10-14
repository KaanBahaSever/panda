using System.Buffers.Binary;
using System.Buffers.Text;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Panda;

/// <summary>
/// Raises a TeamSpeak identity's security level. The level of a key offset is the number of leading
/// zero bits in SHA-1("public key" + offset as decimal text); servers can ask for a minimum
/// (virtualserver_needed_identity_security_level). Every extra level doubles the work, so this
/// searches on all cores but one (TSLib's own search uses one thread and can't be stopped).
/// Raising the level keeps the key, so the client's unique ID stays the same.
/// </summary>
static class IdentityLevel
{
	/// <summary>Beyond this the search takes hours on a small server.</summary>
	public const int Max = 36;

	/// <summary>Zero bits as TeamSpeak counts them: whole zero bytes first, then the low bits of the next byte.</summary>
	static int Of(ReadOnlySpan<byte> hash)
	{
		int bits = 0;
		foreach (var b in hash)
		{
			if (b != 0) return bits + BitOperations.TrailingZeroCount(b);
			bits += 8;
		}
		return bits;
	}

	public static int Of(string publicKey, ulong offset)
	{
		var key = Encoding.ASCII.GetBytes(publicKey);
		var buffer = new byte[key.Length + 20];   // + ulong.MaxValue as text
		key.CopyTo(buffer, 0);
		Utf8Formatter.TryFormat(offset, buffer.AsSpan(key.Length), out var digits);
		return Of(SHA1.HashData(buffer.AsSpan(0, key.Length + digits)));
	}

	/// <summary>A key offset with at least <paramref name="level"/>, or null when stopped.</summary>
	public static ulong? Find(string publicKey, int level, CancellationToken ct)
	{
		const int Chunk = 1 << 16;
		var hasher = new Hasher(Encoding.ASCII.GetBytes(publicKey));
		long next = 0, found = -1;
		var threads = Enumerable.Range(0, Math.Max(1, Environment.ProcessorCount - 1)).Select(_ => new Thread(() =>
		{
			while (Interlocked.Read(ref found) < 0 && !ct.IsCancellationRequested)
			{
				var start = Interlocked.Add(ref next, Chunk) - Chunk;
				for (var offset = start; offset < start + Chunk; offset++)
				{
					if (hasher.Level((ulong)offset) >= level)
					{
						Interlocked.CompareExchange(ref found, offset, -1);
						return;
					}
				}
			}
		}) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Panda identity level" }).ToList();
		threads.ForEach(t => t.Start());
		threads.ForEach(t => t.Join());
		return found >= 0 ? (ulong)found : null;
	}

	/// <summary>
	/// SHA-1 of key + offset, with the key's full 64-byte blocks hashed only once. About three times
	/// as fast as SHA1.HashData on Linux, where every call goes through OpenSSL.
	/// </summary>
	sealed class Hasher
	{
		readonly uint[] start = [0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0];
		readonly byte[] tail;
		readonly int keyLength;

		public Hasher(byte[] key)
		{
			int full = key.Length / 64;
			for (int b = 0; b < full; b++) Compress(start, key.AsSpan(b * 64, 64));
			tail = key[(full * 64)..];
			keyLength = key.Length;
		}

		public int Level(ulong offset)
		{
			Span<byte> blocks = stackalloc byte[128];
			blocks.Clear();
			tail.CopyTo(blocks);
			Utf8Formatter.TryFormat(offset, blocks[tail.Length..], out var digits);
			int length = tail.Length + digits;
			blocks[length] = 0x80;
			int count = length + 9 <= 64 ? 1 : 2;
			BinaryPrimitives.WriteUInt64BigEndian(blocks.Slice(count * 64 - 8), (ulong)(keyLength + digits) * 8);
			Span<uint> s = stackalloc uint[5];
			start.CopyTo(s);
			for (int b = 0; b < count; b++) Compress(s, blocks.Slice(b * 64, 64));

			int bits = 0;
			foreach (var word in s)
			{
				for (int shift = 24; shift >= 0; shift -= 8)
				{
					var b = (byte)(word >> shift);
					if (b != 0) return bits + BitOperations.TrailingZeroCount(b);
					bits += 8;
				}
			}
			return bits;
		}

		static void Compress(Span<uint> s, ReadOnlySpan<byte> block)
		{
			Span<uint> w = stackalloc uint[80];
			for (int i = 0; i < 16; i++) w[i] = BinaryPrimitives.ReadUInt32BigEndian(block.Slice(i * 4));
			for (int i = 16; i < 80; i++) w[i] = BitOperations.RotateLeft(w[i - 3] ^ w[i - 8] ^ w[i - 14] ^ w[i - 16], 1);
			uint a = s[0], b = s[1], c = s[2], d = s[3], e = s[4];
			for (int i = 0; i < 80; i++)
			{
				uint f, k;
				if (i < 20) { f = (b & c) | (~b & d); k = 0x5A827999; }
				else if (i < 40) { f = b ^ c ^ d; k = 0x6ED9EBA1; }
				else if (i < 60) { f = (b & c) | (b & d) | (c & d); k = 0x8F1BBCDC; }
				else { f = b ^ c ^ d; k = 0xCA62C1D6; }
				uint t = BitOperations.RotateLeft(a, 5) + f + e + k + w[i];
				e = d; d = c; c = BitOperations.RotateLeft(b, 30); b = a; a = t;
			}
			s[0] += a; s[1] += b; s[2] += c; s[3] += d; s[4] += e;
		}
	}
}
