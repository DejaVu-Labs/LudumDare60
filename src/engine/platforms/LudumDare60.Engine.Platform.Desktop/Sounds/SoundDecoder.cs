using System.Buffers.Binary;
using NVorbis;

namespace LudumDare60.Engine.Platform.Desktop.Sounds;

/// <summary>
/// Decodes the sound files the game ships: WAV (PCM 8, 16, 24 or 32-bit, or 32-bit float) and Ogg Vorbis, mono or
/// stereo. The formats are told apart by their first bytes, not by a file name.
/// </summary>
internal static class SoundDecoder
{
    private const ushort FormatPcm = 1;
    private const ushort FormatFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    /// <exception cref="InvalidDataException">The file is not a supported sound, or it is broken.</exception>
    public static SoundData Decode(ReadOnlySpan<byte> file)
    {
        if (file.Length >= 12 && file[..4].SequenceEqual("RIFF"u8) && file[8..12].SequenceEqual("WAVE"u8))
        {
            return DecodeWav(file);
        }

        if (file.Length >= 4 && file[..4].SequenceEqual("OggS"u8))
        {
            return DecodeOgg(file.ToArray());
        }

        throw new InvalidDataException("The file is neither WAV nor Ogg Vorbis.");
    }

    private static SoundData DecodeWav(ReadOnlySpan<byte> file)
    {
        ushort format = 0;
        var channels = 0;
        var sampleRate = 0;
        var blockAlign = 0;
        var bits = 0;
        var hasFormat = false;
        var offset = 12;
        while (offset + 8 <= file.Length)
        {
            var id = file.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(file[(offset + 4)..]);
            var body = offset + 8;

            // Some writers stream WAV with a placeholder size; the data then runs to the end of the file.
            var length = (int)Math.Min(size, (uint)(file.Length - body));
            if (id.SequenceEqual("fmt "u8))
            {
                if (length < 16)
                {
                    throw new InvalidDataException("The WAV format chunk is too short.");
                }

                var chunk = file.Slice(body, length);
                format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                sampleRate = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]), int.MaxValue);
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);
                if (format == FormatExtensible)
                {
                    if (length < 26)
                    {
                        throw new InvalidDataException("The WAV extensible format chunk is too short.");
                    }

                    // The sub-format GUID starts with the format code.
                    format = BinaryPrimitives.ReadUInt16LittleEndian(chunk[24..]);
                }

                hasFormat = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (!hasFormat)
                {
                    throw new InvalidDataException("The WAV data chunk comes before the format chunk.");
                }

                return DecodePcm(file.Slice(body, length), format, channels, sampleRate, blockAlign, bits);
            }

            offset = body + length + (length & 1);
        }

        throw new InvalidDataException(hasFormat ? "The WAV file has no data chunk." : "The WAV file has no format chunk.");
    }

    private static SoundData DecodePcm(ReadOnlySpan<byte> data, ushort format, int channels, int sampleRate, int blockAlign, int bits)
    {
        if (channels is < 1 or > 2)
        {
            throw new InvalidDataException($"The WAV file has {channels} channels; only mono and stereo are supported.");
        }

        if (sampleRate < 1)
        {
            throw new InvalidDataException("The WAV file has no sample rate.");
        }

        var bytesPerSample = bits / 8;
        var supported = (format == FormatPcm && bits is 8 or 16 or 24 or 32) || (format == FormatFloat && bits == 32);
        if (!supported || blockAlign != bytesPerSample * channels)
        {
            throw new InvalidDataException($"WAV format {format} with {bits} bits per sample is not supported.");
        }

        var frames = data.Length / blockAlign;
        var samples = new float[frames * channels];
        for (var i = 0; i < samples.Length; i++)
        {
            var sample = data.Slice(i * bytesPerSample, bytesPerSample);
            samples[i] = (format, bits) switch
            {
                (FormatFloat, _) => Math.Clamp(BinaryPrimitives.ReadSingleLittleEndian(sample), -1f, 1f),
                (_, 8) => (sample[0] - 128) / 128f,
                (_, 16) => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f,
                (_, 24) => ((sample[2] << 24) | (sample[1] << 16) | (sample[0] << 8)) / 2147483648f,
                _ => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648f,
            };
        }

        return new SoundData(samples, channels, sampleRate);
    }

    private static SoundData DecodeOgg(byte[] file)
    {
        try
        {
            using var reader = new VorbisReader(new MemoryStream(file, writable: false), closeOnDispose: true);
            if (reader.Channels is < 1 or > 2)
            {
                throw new InvalidDataException($"The Ogg file has {reader.Channels} channels; only mono and stereo are supported.");
            }

            var samples = new List<float>((int)Math.Min(reader.TotalSamples * reader.Channels, 1 << 24));
            var buffer = new float[4096 * reader.Channels];
            int read;
            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(buffer.AsSpan(0, read));
            }

            return new SoundData([.. samples], reader.Channels, reader.SampleRate);
        }
        catch (Exception exception) when (exception is not InvalidDataException and not OutOfMemoryException)
        {
            // NVorbis reports a broken stream with whatever exception the bad data leads to.
            throw new InvalidDataException($"The Ogg Vorbis file is broken: {exception.Message}", exception);
        }
    }
}
