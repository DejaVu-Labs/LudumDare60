using System.Buffers.Binary;

namespace LudumDare60.Game;

/// <summary>
/// Sounds synthesized in code until the recordings exist, as 16-bit mono WAV files that
/// <see cref="Engine.Sounds.Audio.CreateSound"/> accepts.
/// </summary>
internal static class PlaceholderSounds
{
    public const int SampleRate = 22050;

    private const int HeaderSize = 44;

    /// <summary>
    /// A dry metal click, 60 ms: a burst of high noise over a short ring. Play it at a lower pitch for a seating
    /// thunk. The same bytes every time.
    /// </summary>
    public static byte[] CreateClick()
    {
        var count = SampleRate * 60 / 1000;
        var wav = new byte[HeaderSize + (count * sizeof(short))];
        WriteHeader(wav, count);
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

    private static void WriteHeader(Span<byte> wav, int sampleCount)
    {
        var dataSize = sampleCount * sizeof(short);
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav[4..], HeaderSize - 8 + dataSize);
        "WAVE"u8.CopyTo(wav[8..]);
        "fmt "u8.CopyTo(wav[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(wav[22..], 1); // mono
        BinaryPrimitives.WriteInt32LittleEndian(wav[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav[28..], SampleRate * sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(wav[32..], sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(wav[34..], 16);
        "data"u8.CopyTo(wav[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[40..], dataSize);
    }
}
