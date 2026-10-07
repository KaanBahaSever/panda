using System.Runtime.InteropServices;
using Concentus;
using Concentus.Enums;
using TSLib;
using TSLib.Audio;

namespace Panda;

/// <summary>
/// 48 kHz stereo PCM → Opus music packets. Uses Concentus, which picks a native libopus when the
/// system has one and otherwise falls back to its own managed encoder, so nothing has to be installed.
/// </summary>
public sealed class OpusEncoderPipe : IAudioPipe, ISampleInfo
{
	const int FrameSamples = 960; // 20 ms
	public const int PacketSize = FrameSamples * 2 * 2;
	const int MaxPacket = 1275;

	readonly IOpusEncoder encoder;
	readonly byte[] pending = new byte[PacketSize];
	int pendingLength;
	readonly short[] pcm = new short[FrameSamples * 2];
	readonly byte[] packet = new byte[MaxPacket];

	public int SampleRate => 48000;
	public int Channels => 2;
	public int BitsPerSample => 16;
	public bool Active => OutStream?.Active ?? false;
	public IAudioPassiveConsumer? OutStream { get; set; }

	public OpusEncoderPipe(int bitrate)
	{
		encoder = OpusCodecFactory.CreateEncoder(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO);
		encoder.Bitrate = bitrate;
	}

	public void Write(Span<byte> data, Meta? meta)
	{
		if (OutStream is null) return;
		while (data.Length > 0)
		{
			var n = Math.Min(data.Length, PacketSize - pendingLength);
			data[..n].CopyTo(pending.AsSpan(pendingLength));
			pendingLength += n;
			data = data[n..];
			if (pendingLength < PacketSize) break;

			MemoryMarshal.Cast<byte, short>(pending).CopyTo(pcm);
			pendingLength = 0;
			var len = encoder.Encode(pcm, FrameSamples, packet, MaxPacket);
			meta ??= new Meta();
			meta.Codec = Codec.OpusMusic;
			OutStream?.Write(packet.AsSpan(0, len), meta);
		}
	}
}
