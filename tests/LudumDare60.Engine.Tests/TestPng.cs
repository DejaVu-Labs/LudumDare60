using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// Writes minimal 8-bit PNG files, so image tests state their pixels in code instead of shipping binary fixtures.
/// </summary>
internal static class TestPng
{
    public const byte Gray = 0;
    public const byte Rgb = 2;
    public const byte GrayAlpha = 4;
    public const byte Rgba = 6;

    private static readonly uint[] s_crcTable = BuildCrcTable();

    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="colorType">PNG color type: <see cref="Gray"/>, <see cref="Rgb"/>, <see cref="GrayAlpha"/> or
    /// <see cref="Rgba"/>.</param>
    /// <param name="samples">Pixels row by row from the top, with the channels of <paramref name="colorType"/>.</param>
    public static byte[] Encode(int width, int height, byte colorType, byte[] samples)
    {
        var channels = colorType switch
        {
            Gray => 1,
            Rgb => 3,
            GrayAlpha => 2,
            Rgba => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(colorType)),
        };
        var stride = width * channels;
        Assert.Equal(stride * height, samples.Length);

        using var file = new MemoryStream();
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = colorType;
        WriteChunk(file, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // Filter type None.
                zlib.Write(samples, y * stride, stride);
            }
        }

        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", []);
        return file.ToArray();
    }

    private static void WriteChunk(MemoryStream file, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        file.Write(length);

        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, typeAndData);
        data.CopyTo(typeAndData, 4);
        file.Write(typeAndData);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        file.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
