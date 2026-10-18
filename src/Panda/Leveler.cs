using System.Runtime.InteropServices;

namespace Panda;

/// <summary>
/// Brings one song (48 kHz stereo s16) to the target loudness while it plays. A song measured before
/// (EBU R128, see <see cref="Library"/>) gets its gain from the start and keeps it, so its own dynamics stay.
/// One that isn't (the first play, other sites, radio) is listened to: its loudness is measured the way
/// R128 does it (K-weighted, 400 ms blocks, gated) and the gain follows, quickly in the first seconds,
/// then slowly enough not to be heard. A station is followed over its last 30 seconds.
/// A raised song may peak over full scale: the <see cref="Limiter"/> after the gain takes care of that.
/// </summary>
public sealed class Leveler
{
	const int Rate = 48000;
	const int Block = Rate / 10;               // 100 ms; four make a 400 ms R128 block
	const double FastSeconds = 3;              // settling quickly at the start...
	const double FastDbPerSecond = 12;         // ...then slowly enough not to be heard: down a bit faster
	const double SlowDownDbPerSecond = 2, SlowUpDbPerSecond = 0.5;   // than up (too loud is worse than too quiet)
	// Intros are often quieter than the song: in the first 10 seconds a song is raised a little less
	// (4 dB less at first, then less and less). Turning down is never held back.
	const double EarlyHeadroom = 4, EarlySeconds = 10;
	const int RadioBlocks = 300;               // 30 s for a station
	const int SongBlocks = 36000;              // an hour for a song
	const int RecentBlocks = 100;              // the last 10 s
	// A measured song is raised so that the limiter takes at most this much off its loudest peak:
	// a very dynamic recording (jazz, classical) isn't squashed.
	const double MaxLimitingDb = 6;

	readonly double target;
	readonly bool adaptive, live;
	double gainDb;
	float gain;
	readonly KFilter kl = new(), kr = new();
	readonly Limiter limiter = new();
	double blockSum;
	int blockFill;
	readonly double[] quarters = new double[4];   // the last four 100 ms energies
	int quarterCount;
	// The 400 ms block energies above the absolute gate, and the last 10 s of them. Changed by the audio
	// thread, copied by ContinueFrom: under the lock.
	readonly Queue<double> blocks = new();
	readonly Queue<double> recent = new();
	double blocksSum, recentSum;
	long frames;
	readonly object gate = new();

	/// <param name="measured">The song's known integrated loudness (LUFS) and sample peak (dBFS), or null to listen.</param>
	public Leveler(double targetLufs, (double Lufs, double Peak)? measured, bool live)
	{
		target = targetLufs;
		this.live = live;
		adaptive = measured is null;
		SetGain(measured is { } m
			? Math.Clamp(Math.Min(targetLufs - m.Lufs, Limiter.CeilingDb - m.Peak + MaxLimitingDb), -20, 12)
			: 0);
	}

	public double GainDb => gainDb;

	/// <summary>The same song started again elsewhere (a seek, another try, a new speed): it goes on with
	/// what was heard of it so far, without settling again from the start.</summary>
	public void ContinueFrom(Leveler old)
	{
		if (!adaptive || !old.adaptive) return;
		double[] heard, last;
		lock (old.gate)
		{
			heard = old.blocks.ToArray();
			last = old.recent.ToArray();
		}
		foreach (var b in heard) blocks.Enqueue(b);
		foreach (var b in last) recent.Enqueue(b);
		blocksSum = heard.Sum();
		recentSum = last.Sum();
		frames = old.frames;
		SetGain(old.gainDb);
	}

	void SetGain(double db)
	{
		gainDb = db;
		gain = (float)Math.Pow(10, db / 20);
	}

	public void Process(Span<byte> data)
	{
		var pcm = MemoryMarshal.Cast<byte, short>(data);
		for (int i = 0; i + 1 < pcm.Length; i += 2)
		{
			if (adaptive)
			{
				var l = kl.Next(pcm[i] / 32768.0);
				var r = kr.Next(pcm[i + 1] / 32768.0);
				blockSum += l * l + r * r;
				if (++blockFill == Block) EndBlock();
			}
			frames++;
			var (ol, or) = limiter.Next(pcm[i] * gain, pcm[i + 1] * gain);
			pcm[i] = (short)ol;
			pcm[i + 1] = (short)or;
		}
	}

	/// <summary>The song ended: the last 5 ms still in the limiter go into <paramref name="room"/>. Returns the bytes written.</summary>
	public int Drain(Span<byte> room) => limiter.Drain(MemoryMarshal.Cast<byte, short>(room)) * 4;

	void EndBlock()
	{
		quarters[quarterCount++ % 4] = blockSum / Block;
		blockSum = 0;
		blockFill = 0;
		if (quarterCount < 4) return;
		var energy = (quarters[0] + quarters[1] + quarters[2] + quarters[3]) / 4;
		// Silence and very quiet parts (R128's absolute gate is -70; a bit higher here) say nothing.
		if (Lufs(energy) < -60) return;
		double loudness, recentLoudness;
		int count;
		lock (gate)
		{
			blocks.Enqueue(energy);
			blocksSum += energy;
			if (blocks.Count > (live ? RadioBlocks : SongBlocks)) blocksSum -= blocks.Dequeue();
			recent.Enqueue(energy);
			recentSum += energy;
			if (recent.Count > RecentBlocks) recentSum -= recent.Dequeue();
			// Relative gate: blocks 10 LU under the average don't count (R128's integrated loudness).
			var threshold = blocksSum / blocks.Count * 0.1;
			double sum = 0; int n = 0;
			foreach (var b in blocks) if (b >= threshold) { sum += b; n++; }
			loudness = Lufs(sum / n);
			recentLoudness = Lufs(recentSum / recent.Count);
			count = recent.Count;
		}
		// A song that builds up: when the last 10 seconds are much louder than the song so far, they count
		// (3 LU of difference stay, so a chorus is still louder than the verse).
		if (!live && count >= RecentBlocks) loudness = Math.Max(loudness, recentLoudness - 3);
		var want = Math.Clamp(target - loudness, -20, 12);
		var seconds = frames / (double)Rate;
		var early = seconds < FastSeconds;
		if (!live && want > gainDb) want = Math.Max(gainDb, want - EarlyHeadroom * Math.Max(0, 1 - seconds / EarlySeconds));
		var down = (early ? FastDbPerSecond : SlowDownDbPerSecond) * 0.1;
		var up = (early ? FastDbPerSecond : SlowUpDbPerSecond) * 0.1;
		SetGain(gainDb + Math.Clamp(want - gainDb, -down, up));
	}

	static double Lufs(double energy) => energy <= 0 ? -100 : -0.691 + 10 * Math.Log10(energy);

	/// <summary>ITU-R BS.1770-4 K-weighting at 48 kHz: a high shelf, then a high pass.</summary>
	sealed class KFilter
	{
		double x1, x2, y1, y2, v1, v2;
		public double Next(double x)
		{
			var y = 1.53512485958697 * x - 2.69169618940638 * x1 + 1.19839281085285 * x2 + 1.69065929318241 * y1 - 0.73248077421585 * y2;
			x2 = x1; x1 = x;
			var v = y - 2 * y1 + y2 + 1.99004745483398 * v1 - 0.99007225036621 * v2;
			y2 = y1; y1 = y;
			v2 = v1; v1 = v;
			return v;
		}
	}
}

/// <summary>
/// A look-ahead peak limiter at -1.5 dBFS: the audio goes through 5 ms later, so the gain comes down
/// smoothly before a peak arrives, and back up over 150 ms after it.
/// </summary>
public sealed class Limiter
{
	const int Ahead = 240;                     // 5 ms at 48 kHz
	const int Slots = Ahead + 1;
	public const float Ceiling = 27571;        // -1.5 dBFS: room for the peaks between samples
	public const double CeilingDb = -1.5;
	static readonly float Attack = 1 - MathF.Exp(-1f / 60);                // ~1.25 ms: 98 % within the 5 ms
	static readonly float Release = 1 - MathF.Exp(-1f / (0.15f * 48000));
	readonly float[] delayL = new float[Ahead], delayR = new float[Ahead];
	int pos;
	// The smallest gain any sample in the window needs: a queue of (sample, gain), gains rising.
	readonly long[] qIndex = new long[Slots];
	readonly float[] qGain = new float[Slots];
	int head, tail;
	long index;
	float env = 1;

	public (float L, float R) Next(float l, float r)
	{
		var peak = Math.Max(Math.Abs(l), Math.Abs(r));
		var need = peak > Ceiling ? Ceiling / peak : 1f;
		while (tail != head && qGain[(tail - 1 + Slots) % Slots] >= need) tail = (tail - 1 + Slots) % Slots;
		qIndex[tail] = index;
		qGain[tail] = need;
		tail = (tail + 1) % Slots;
		while (qIndex[head] <= index - Ahead) head = (head + 1) % Slots;
		var want = qGain[head];
		env += (want - env) * (want < env ? Attack : Release);
		var outL = delayL[pos] * env;
		var outR = delayR[pos] * env;
		delayL[pos] = l;
		delayR[pos] = r;
		pos = (pos + 1) % Ahead;
		index++;
		// What the smoothing still let through (a sudden huge peak) is cut at the ceiling.
		return (Math.Clamp(outL, -Ceiling, Ceiling), Math.Clamp(outR, -Ceiling, Ceiling));
	}

	/// <summary>Writes out what is still delayed (silence goes in behind it). Returns the frames written.</summary>
	public int Drain(Span<short> room)
	{
		var frames = Math.Min(room.Length / 2, (int)Math.Min(index, Ahead));
		for (int i = 0; i < frames; i++)
		{
			var (l, r) = Next(0, 0);
			room[2 * i] = (short)l;
			room[2 * i + 1] = (short)r;
		}
		return frames;
	}
}
