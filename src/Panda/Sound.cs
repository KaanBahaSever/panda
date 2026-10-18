using System.Globalization;
using System.Runtime.InteropServices;
using TSLib.Audio;

namespace Panda;

/// <summary>
/// How a bot sounds: an equalizer, effects and the speed. Kept in the bot's settings, changed from the
/// panel or the chat. The equalizer and 8D work on the audio as it goes out (<see cref="SoundPipe"/>), at
/// once; nightcore, slowed and the speed are ffmpeg filters, so the song starts again where it was.
/// </summary>
public sealed record Sound(string Equalizer, double[] Bands, string Effect, double Speed, bool EightD, int Crossfade)
{
	/// <summary>Centre frequencies of the ten bands (Hz).</summary>
	public static readonly int[] Frequencies = [32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
	public const double MaxGain = 12;

	/// <summary>The presets' gains (dB) for the ten bands; "custom" uses the bot's own bands.</summary>
	public static readonly IReadOnlyDictionary<string, double[]> Presets = new Dictionary<string, double[]>
	{
		["bass"] = [6, 5, 4, 2, 0, 0, 0, 0, 0, 0],
		["treble"] = [0, 0, 0, 0, 0, 0, 2, 4, 5, 6],
		["vocal"] = [-2, -2, -1, 0, 2, 4, 4, 3, 1, 0],
		["pop"] = [-1, 1, 3, 4, 3, 0, -1, -1, 1, 2],
		["rock"] = [4, 3, 2, 0, -1, -1, 1, 3, 4, 4],
		["classical"] = [4, 3, 2, 1, -1, -1, 0, 2, 3, 4],
		["electronic"] = [5, 4, 1, 0, -2, 1, 0, 1, 4, 5],
		["jazz"] = [3, 2, 1, 2, -1, -1, 0, 1, 2, 3],
	};

	public static readonly string[] Equalizers = ["off", .. Presets.Keys, "custom"];
	public static readonly string[] Effects = ["off", "nightcore", "slowed"];
	public const int MaxCrossfade = 12;

	public static Sound Of(BotSettings b) => new(
		Equalizers.Contains(b.Equalizer) ? b.Equalizer : "off",
		Normalize(b.EqualizerBands),
		Effects.Contains(b.Effect) ? b.Effect : "off",
		Math.Clamp(b.Speed is > 0 ? b.Speed : 1, 0.5, 2),
		b.EightD,
		Math.Clamp(b.Crossfade, 0, MaxCrossfade));

	/// <summary>Ten gains between -12 and +12 dB, whatever was saved.</summary>
	public static double[] Normalize(IEnumerable<double>? bands)
	{
		var list = (bands ?? []).Take(10).Select(g => double.IsFinite(g) ? Math.Round(Math.Clamp(g, -MaxGain, MaxGain), 1) : 0).ToList();
		while (list.Count < 10) list.Add(0);
		return [.. list];
	}

	/// <summary>The gains in use: the preset's, the bot's own, or none.</summary>
	public double[] Gains => Equalizer == "custom" ? Bands : Presets.TryGetValue(Equalizer, out var p) ? p : new double[10];

	/// <summary>Song seconds per second heard: nightcore 1.25, slowed 0.85, times the speed.</summary>
	public double Rate => (Effect switch { "nightcore" => 1.25, "slowed" => 0.85, _ => 1 }) * Speed;

	/// <summary>The ffmpeg filters for the rate, or null. Resampling to 48 kHz first: asetrate assumes the rate it's given.</summary>
	public string? Filter
	{
		get
		{
			var parts = new List<string>();
			if (Effect is "nightcore" or "slowed")
			{
				var to = (int)Math.Round(48000 * (Effect == "nightcore" ? 1.25 : 0.85));
				parts.Add($"aresample=48000,asetrate={to},aresample=48000");
			}
			if (Math.Abs(Speed - 1) >= 0.01) parts.Add("atempo=" + Speed.ToString("0.###", CultureInfo.InvariantCulture));
			return parts.Count == 0 ? null : string.Join(',', parts);
		}
	}

	/// <summary>Effect names in the chat languages ("yavaş" is slowed...).</summary>
	public static string? EffectName(string word) => word.ToLowerInvariant() switch
	{
		"nightcore" or "nc" => "nightcore",
		"slowed" or "slow" or "yavaş" or "yavas" or "ralenti" or "langsam" => "slowed",
		"8d" => "8d",
		"off" or "kapat" or "kapalı" or "kapali" or "non" or "aus" or "none" => "off",
		_ => null,
	};

	/// <summary>Equalizer names in the chat languages.</summary>
	public static string? EqualizerName(string word) => word.ToLowerInvariant() switch
	{
		"off" or "flat" or "kapat" or "kapalı" or "kapali" or "non" or "aus" or "none" => "off",
		"bass" or "bas" or "basses" => "bass",
		"treble" or "tiz" or "aigus" or "aigu" or "höhen" or "hoehen" => "treble",
		"vocal" or "vokal" or "voix" or "gesang" => "vocal",
		"pop" => "pop",
		"rock" => "rock",
		"classical" or "klasik" or "classique" or "klassik" => "classical",
		"electronic" or "elektronik" or "electro" or "électro" or "elektro" => "electronic",
		"jazz" or "caz" => "jazz",
		"custom" or "özel" or "ozel" or "perso" or "eigene" => "custom",
		_ => null,
	};
}

/// <summary>
/// The equalizer (ten peaking filters per channel) and 8D (the sound slowly circling between the ears)
/// on the 48 kHz stereo PCM going to the encoder. Settings change at once, between two packets.
/// </summary>
public sealed class SoundPipe : IAudioPipe
{
	const int Rate = 48000;
	public bool Active => OutStream?.Active ?? false;
	public IAudioPassiveConsumer? OutStream { get; set; }

	sealed class Setup(Biquad[] left, Biquad[] right, float preamp, bool eightD)
	{
		public readonly Biquad[] Left = left, Right = right;
		public readonly float Preamp = preamp;
		public readonly bool EightD = eightD;
	}

	volatile Setup? setup;
	long frames;   // for the 8D turn

	public void Set(Sound s)
	{
		var gains = s.Gains;
		var eq = gains.Any(g => Math.Abs(g) >= 0.1);
		Biquad[] Bank() => eq ? Sound.Frequencies.Select((f, i) => Biquad.Peaking(f, gains[i], Rate)).ToArray() : [];
		// Boosted bands would clip: everything goes down by half the biggest boost, and the soft limiter
		// (Clip) rounds off what is still too loud.
		var preamp = eq ? (float)Math.Pow(10, -Math.Max(0, gains.Max()) * 0.5 / 20) : 1f;
		var old = setup;
		var next = new Setup(Bank(), Bank(), preamp, s.EightD);
		// New gains go on from where the filters were: no click while a band is being moved.
		if (old != null && old.Left.Length == next.Left.Length)
			for (int i = 0; i < next.Left.Length; i++)
			{
				next.Left[i].Continue(old.Left[i]);
				next.Right[i].Continue(old.Right[i]);
			}
		setup = next;
	}

	public void Write(Span<byte> data, Meta? meta)
	{
		if (OutStream is null) return;
		if (setup is { } s && (s.Left.Length > 0 || s.EightD || depth > 0)) Process(MemoryMarshal.Cast<byte, short>(data), s);
		OutStream.Write(data, meta);
	}

	// How much of 8D is on: eased in and out over 50 ms, no jump from the middle to one side.
	float depth;

	void Process(Span<short> pcm, Setup s)
	{
		for (int i = 0; i + 1 < pcm.Length; i += 2)
		{
			float l = pcm[i], r = pcm[i + 1];
			if (s.Left.Length > 0)
			{
				l *= s.Preamp;
				r *= s.Preamp;
				foreach (var f in s.Left) l = f.Next(l);
				foreach (var f in s.Right) r = f.Next(r);
			}
			depth = s.EightD ? Math.Min(1f, depth + 1f / 2400) : Math.Max(0f, depth - 1f / 2400);
			if (depth > 0)
			{
				// One turn in 8 seconds; equal power, never fully on one side.
				var pan = 0.5 + 0.4 * depth * Math.Sin(2 * Math.PI * frames / (Rate * 8.0));
				var mono = (l + r) * 0.5f;
				var side = (l - r) * 0.5f;
				l = (float)(mono * Math.Sqrt(2 * (1 - pan))) + side;
				r = (float)(mono * Math.Sqrt(2 * pan)) - side;
			}
			frames++;
			pcm[i] = Clip(l);
			pcm[i + 1] = Clip(r);
		}
	}

	// Linear up to the knee, then bent smoothly towards full scale: no hard clipping from a boost.
	const float Knee = 24000f;
	public static short Limit(float v) => Clip(v);

	static short Clip(float v)
	{
		var a = Math.Abs(v);
		if (a <= Knee) return (short)v;
		const float room = short.MaxValue - Knee;
		var bent = Knee + room * MathF.Tanh((a - Knee) / room);
		return (short)(v < 0 ? -bent : bent);
	}
}

/// <summary>One biquad filter (RBJ's audio EQ cookbook), direct form I.</summary>
public sealed class Biquad
{
	readonly float b0, b1, b2, a1, a2;
	float x1, x2, y1, y2;

	Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
	{
		this.b0 = (float)(b0 / a0); this.b1 = (float)(b1 / a0); this.b2 = (float)(b2 / a0);
		this.a1 = (float)(a1 / a0); this.a2 = (float)(a2 / a0);
	}

	/// <summary>A bell around <paramref name="freq"/>, an octave wide.</summary>
	public static Biquad Peaking(double freq, double gainDb, int rate)
	{
		var a = Math.Pow(10, gainDb / 40);
		var w = 2 * Math.PI * Math.Min(freq, rate * 0.45) / rate;
		var alpha = Math.Sin(w) / (2 * 1.41);
		var cos = Math.Cos(w);
		return new Biquad(1 + alpha * a, -2 * cos, 1 - alpha * a, 1 + alpha / a, -2 * cos, 1 - alpha / a);
	}

	/// <summary>Takes over the last samples of the filter this one replaces.</summary>
	public void Continue(Biquad old) { x1 = old.x1; x2 = old.x2; y1 = old.y1; y2 = old.y2; }

	public float Next(float x)
	{
		var y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
		// Tiny values (a fading tail) would make the CPU slow down on denormals.
		if (!float.IsFinite(y) || Math.Abs(y) < 1e-12f) y = 0;
		x2 = x1; x1 = x;
		y2 = y1; y1 = y;
		return y;
	}
}
