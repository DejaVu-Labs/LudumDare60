using System.Buffers.Binary;

namespace LudumDare60.Game;

/// <summary>
/// Sounds synthesized in code until the recordings exist, as 16-bit mono WAV files that
/// <see cref="Engine.Sounds.Audio.CreateSound"/> accepts.
/// </summary>
internal static class PlaceholderSounds
{
    public const int SampleRate = 22050;

    /// <summary>
    /// Sample rate of <see cref="CreateScrubLoop"/>: its bristle clicks reach past 10 kHz.
    /// </summary>
    public const int ScrubSampleRate = 44100;

    /// <summary>
    /// Length of <see cref="CreateScrubLoop"/> in seconds.
    /// </summary>
    public const float ScrubSeconds = 1.5f;

    private const int HeaderSize = 44;

    /// <summary>
    /// A dry metal click, 60 ms: a burst of high noise over a short ring. Play it at a lower pitch for a seating
    /// thunk. The same bytes every time.
    /// </summary>
    public static byte[] CreateClick()
    {
        var count = SampleRate * 60 / 1000;
        var wav = new byte[HeaderSize + (count * sizeof(short))];
        WriteHeader(wav, count, SampleRate);
        var noise = 60u;
        var previous = 0f;
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)SampleRate;

            // A linear congruential generator: deterministic and cheap, and no security concern.
            noise = (noise * 1664525) + 1013904223;
            var white = ((noise >> 8) / (float)(1 << 24) * 2) - 1;

            // A first difference keeps the highs: a tight click, not a hiss.
            var click = (white - previous) * 0.5f * MathF.Exp(-t / 0.006f);
            previous = white;
            var ring = MathF.Sin(2 * MathF.PI * 2400 * t) * 0.35f * MathF.Exp(-t / 0.018f);
            var sample = Math.Clamp((click + ring) * 0.8f, -1, 1);
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(HeaderSize + (i * sizeof(short))), (short)(sample * short.MaxValue));
        }

        return wav;
    }

    /// <summary>
    /// A wire brush rubbed over rusty steel, as a seamless loop: thousands of bristle clicks per second over a bright
    /// hiss, the rasp of the bristles sticking and slipping around 900 Hz, and a slow flutter of the bristle bundles.
    /// Play it as a loop and drive its volume and pitch with the brush's speed. The same bytes every time.
    /// </summary>
    public static byte[] CreateScrubLoop()
    {
        var count = (int)(ScrubSampleRate * ScrubSeconds);
        var seed = 61u;

        // Bristles catching on the rust and letting go: tiny noise bursts, most faint, a few loud. They wrap around
        // the end, and every filter runs over the loop twice, so the end flows into the start without a click.
        var bright = new float[count];
        var grains = (int)(2600 * ScrubSeconds);
        for (var g = 0; g < grains; g++)
        {
            var start = (int)(Next(ref seed) * count);
            var length = 1 + (int)(ScrubSampleRate * (0.0002f + (0.0007f * Next(ref seed))));
            var u = Next(ref seed);
            var amplitude = u * u * u * 1.6f;
            for (var k = 0; k < length; k++)
            {
                bright[(start + k) % count] += Signed(ref seed) * amplitude * MathF.Exp(-4f * k / length);
            }
        }

        for (var i = 0; i < count; i++)
        {
            bright[i] += Signed(ref seed) * 0.3f;
        }

        FilterLooped(bright, Biquad.HighPass(1500, 0.7f, ScrubSampleRate));
        FilterLooped(bright, Biquad.LowPass(9000, 0.7f, ScrubSampleRate));

        var rasp = new float[count];
        for (var i = 0; i < count; i++)
        {
            rasp[i] = Signed(ref seed);
        }

        FilterLooped(rasp, Biquad.BandPass(900, 1.4f, ScrubSampleRate));
        Normalize(bright, 1);
        Normalize(rasp, 0.55f);

        // Periodic modulations: whole cycles over the loop, so they wrap without a jump too.
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)ScrubSampleRate;
            var chatter = 0.55f + (0.45f * MathF.Sin(Cycles(135, t)));
            var flutter = 1 + (0.18f * (MathF.Sin(Cycles(26, t)) + (0.6f * MathF.Sin(Cycles(44, t) + 1)) + (0.4f * MathF.Sin(Cycles(61, t) + 2))));
            samples[i] = ((bright[i] * 0.9f) + (rasp[i] * chatter)) * flutter;
        }

        Normalize(samples, 0.22f);
        var wav = new byte[HeaderSize + (count * sizeof(short))];
        WriteHeader(wav, count, ScrubSampleRate);
        for (var i = 0; i < count; i++)
        {
            // A soft clip keeps the loudest clicks from cracking.
            var sample = MathF.Tanh(samples[i] * 1.4f) * 0.75f;
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(HeaderSize + (i * sizeof(short))), (short)(sample * short.MaxValue));
        }

        return wav;
    }

    /// <summary>
    /// The phase of a sine that makes <paramref name="cycles"/> whole cycles over the scrub loop, at time
    /// <paramref name="t"/>.
    /// </summary>
    private static float Cycles(int cycles, float t) => 2 * MathF.PI * cycles / ScrubSeconds * t;

    /// <summary>
    /// A linear congruential generator, in [0, 1): deterministic and cheap, and no security concern.
    /// </summary>
    private static float Next(ref uint state)
    {
        state = (state * 1664525) + 1013904223;
        return (state >> 8) / (float)(1 << 24);
    }

    private static float Signed(ref uint state) => (Next(ref state) * 2) - 1;

    /// <summary>
    /// Filters a loop in place as if it repeated forever: the first pass only settles the filter's state.
    /// </summary>
    private static void FilterLooped(float[] samples, Biquad filter)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            filter.Process(samples[i]);
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = filter.Process(samples[i]);
        }
    }

    /// <summary>
    /// Scales <paramref name="samples"/> to the root mean square <paramref name="rms"/>.
    /// </summary>
    private static void Normalize(float[] samples, float rms)
    {
        var sum = 0.0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        var scale = rms / (float)Math.Sqrt(sum / samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }
    }

    private static void WriteHeader(Span<byte> wav, int sampleCount, int sampleRate)
    {
        var dataSize = sampleCount * sizeof(short);
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav[4..], HeaderSize - 8 + dataSize);
        "WAVE"u8.CopyTo(wav[8..]);
        "fmt "u8.CopyTo(wav[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(wav[22..], 1); // mono
        BinaryPrimitives.WriteInt32LittleEndian(wav[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav[28..], sampleRate * sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(wav[32..], sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(wav[34..], 16);
        "data"u8.CopyTo(wav[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[40..], dataSize);
    }

    /// <summary>
    /// A second-order IIR filter (RBJ audio EQ cookbook), direct form I.
    /// </summary>
    private sealed class Biquad
    {
        private readonly float _b0;
        private readonly float _b1;
        private readonly float _b2;
        private readonly float _a1;
        private readonly float _a2;
        private float _x1;
        private float _x2;
        private float _y1;
        private float _y2;

        private Biquad(float b0, float b1, float b2, float a0, float a1, float a2)
        {
            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public static Biquad HighPass(float frequency, float q, int sampleRate)
        {
            var (cos, alpha) = Prepare(frequency, q, sampleRate);
            return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad LowPass(float frequency, float q, int sampleRate)
        {
            var (cos, alpha) = Prepare(frequency, q, sampleRate);
            return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        /// <summary>
        /// A band pass with 0 dB gain at <paramref name="frequency"/>.
        /// </summary>
        public static Biquad BandPass(float frequency, float q, int sampleRate)
        {
            var (cos, alpha) = Prepare(frequency, q, sampleRate);
            return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public float Process(float x)
        {
            var y = (_b0 * x) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return y;
        }

        private static (float Cos, float Alpha) Prepare(float frequency, float q, int sampleRate)
        {
            var w0 = 2 * MathF.PI * frequency / sampleRate;
            return (MathF.Cos(w0), MathF.Sin(w0) / (2 * q));
        }
    }
}
