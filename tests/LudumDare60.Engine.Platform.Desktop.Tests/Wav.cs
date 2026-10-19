using System.Buffers.Binary;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

/// <summary>
/// Builds WAV files for decoder tests.
/// </summary>
internal static class Wav
{
    public static byte[] Create(ushort format, int channels, int sampleRate, int bits, ReadOnlySpan<byte> data, bool extensible = false)
    {
        var formatSize = extensible ? 40 : 16;
        var file = new byte[12 + 8 + formatSize + 8 + data.Length + (data.Length & 1)];
        var span = file.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], file.Length - 8);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], formatSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], extensible ? (ushort)0xFFFE : format);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], (ushort)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * channels * bits / 8);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], (ushort)(channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], (ushort)bits);
        if (extensible)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[36..], 22);
            BinaryPrimitives.WriteUInt16LittleEndian(span[38..], (ushort)bits);
            BinaryPrimitives.WriteUInt16LittleEndian(span[44..], format);
        }

        var dataOffset = 20 + formatSize;
        "data"u8.CopyTo(span[dataOffset..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[(dataOffset + 4)..], data.Length);
        data.CopyTo(span[(dataOffset + 8)..]);
        return file;
    }

    public static byte[] Pcm16(int channels, int sampleRate, params short[] samples)
    {
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2), samples[i]);
        }

        return Create(1, channels, sampleRate, 16, data);
    }
}
