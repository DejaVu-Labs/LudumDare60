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
    /// Sample rate of <see cref="CreateDoorBell"/>: its highest partial rings near 12 kHz.
    /// </summary>
    public const int BellSampleRate = 44100;

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
    /// Length of <see cref="CreateDoorBell"/> in seconds.
    /// </summary>
    public const float DoorBellSeconds = 1.6f;

    /// <summary>
    /// The little brass bell over the shop door: struck three times, softer each time, as the door swings. Each strike
    /// rings a small bell's inharmonic partials, every one of them a pair of slightly detuned sines that beat. The same
    /// bytes every time. Mono, at <see cref="BellSampleRate"/>.
    /// </summary>
    public static byte[] CreateDoorBell()
    {
        // Partials of a small bell: frequency, level, decay time in seconds.
        ReadOnlySpan<float> partials =
        [
            1760, 1.00f, 0.50f,
            4080, 0.55f, 0.28f,
            7480, 0.30f, 0.15f,
            11670, 0.15f, 0.08f,
        ];

        // Strikes as the door swings: time in seconds and strength.
        ReadOnlySpan<float> strikes = [0, 1.0f, 0.13f, 0.6f, 0.31f, 0.3f];
        var count = (int)(BellSampleRate * DoorBellSeconds);
        var samples = new float[count];
        for (var s = 0; s < strikes.Length; s += 2)
        {
            var start = (int)(strikes[s] * BellSampleRate);
            var strength = strikes[s + 1];
            for (var i = start; i < count; i++)
            {
                var t = (i - start) / (float)BellSampleRate;

                // A two-millisecond attack keeps the strike from clicking.
                var attack = MathF.Min(1, t / 0.002f);
                var sample = 0f;
                for (var p = 0; p < partials.Length; p += 3)
                {
                    var frequency = partials[p];
                    var envelope = partials[p + 1] * MathF.Exp(-t / partials[p + 2]);
                    sample += envelope * 0.5f * (MathF.Sin(2 * MathF.PI * frequency * t) + MathF.Sin(2 * MathF.PI * frequency * 1.004f * t));
                }

                samples[i] += sample * strength * attack;
            }
        }

        var peak = 0f;
        foreach (var sample in samples)
        {
            peak = MathF.Max(peak, MathF.Abs(sample));
        }

        var wav = new byte[HeaderSize + (count * sizeof(short))];
        WriteHeader(wav, count, BellSampleRate);
        var fadeStart = count - (BellSampleRate / 5);
        for (var i = 0; i < count; i++)
        {
            // The last 200 ms fade to silence, so the sound never ends on a click.
            var fade = i < fadeStart ? 1 : (count - i) / (float)(count - fadeStart);
            var sample = samples[i] / peak * 0.7f * fade;
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(HeaderSize + (i * sizeof(short))), (short)(sample * short.MaxValue));
        }

        return wav;
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
}
