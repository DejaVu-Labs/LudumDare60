using System.Buffers.Binary;
using LudumDare60.Engine.Platform.Desktop.Sounds;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class SoundDecoderTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Decode_Pcm16Mono_ScalesToUnitRange()
    {
        var sound = SoundDecoder.Decode(Wav.Pcm16(1, 22050, 0, 16384, short.MinValue, short.MaxValue));

        Assert.Equal((1, 22050, 4), (sound.Channels, sound.SampleRate, sound.FrameCount));
        Assert.Equal([0, 0.5f, -1, short.MaxValue / 32768f], sound.Samples);
    }

    [Fact]
    public void Decode_Pcm16Stereo_KeepsSamplesInterleaved()
    {
        var sound = SoundDecoder.Decode(Wav.Pcm16(2, 44100, 16384, -16384, 0, 8192));

        Assert.Equal((2, 2), (sound.Channels, sound.FrameCount));
        Assert.Equal([0.5f, -0.5f, 0, 0.25f], sound.Samples);
    }

    [Fact]
    public void Decode_Pcm8_IsUnsigned()
    {
        var sound = SoundDecoder.Decode(Wav.Create(1, 1, 8000, 8, [128, 0, 192]));

        Assert.Equal([0, -1, 0.5f], sound.Samples);
    }

    [Fact]
    public void Decode_Pcm24_ReadsThreeBytesLittleEndian()
    {
        var sound = SoundDecoder.Decode(Wav.Create(1, 1, 8000, 24, [0x00, 0x00, 0x40, 0x00, 0x00, 0x80]));

        Assert.Equal([0.5f, -1], sound.Samples);
    }

    [Fact]
    public void Decode_Pcm32_ScalesToUnitRange()
    {
        var data = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(data, int.MinValue);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 1 << 30);

        var sound = SoundDecoder.Decode(Wav.Create(1, 1, 8000, 32, data));

        Assert.Equal([-1, 0.5f], sound.Samples);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decode_Float32_ClampsToUnitRange(bool extensible)
    {
        var data = new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(data, 0.25f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(4), 2f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(8), -3f);

        var sound = SoundDecoder.Decode(Wav.Create(3, 1, 48000, 32, data, extensible));

        Assert.Equal([0.25f, 1, -1], sound.Samples);
    }

    [Fact]
    public void Decode_ChunkBeforeData_IsSkipped()
    {
        var wav = Wav.Pcm16(1, 8000, 16384);
        var list = new byte[] { (byte)'L', (byte)'I', (byte)'S', (byte)'T', 3, 0, 0, 0, 1, 2, 3, 0 };
        var file = wav[..36].Concat(list).Concat(wav[36..]).ToArray();

        Assert.Equal([0.5f], SoundDecoder.Decode(file).Samples);
    }

    [Fact]
    public void Decode_DataSizeBeyondTheFile_ReadsToTheEnd()
    {
        var file = Wav.Pcm16(1, 8000, 16384, 16384);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), uint.MaxValue);

        Assert.Equal(2, SoundDecoder.Decode(file).FrameCount);
    }

    [Fact]
    public void Decode_SampleClickWav_Decodes()
    {
        var sound = SoundDecoder.Decode(File.ReadAllBytes(Fixture("click.wav")));

        Assert.True(sound.FrameCount > 0);
        Assert.Contains(sound.Samples, static sample => sample != 0);
    }

    [Fact]
    public void Decode_SampleRainOgg_Decodes()
    {
        var sound = SoundDecoder.Decode(File.ReadAllBytes(Fixture("rain.ogg")));

        Assert.InRange(sound.Channels, 1, 2);
        Assert.True(sound.SampleRate >= 8000);
        Assert.True(sound.FrameCount > sound.SampleRate / 2);
        Assert.All(sound.Samples, static sample => Assert.InRange(sample, -1.0001f, 1.0001f));
    }

    [Fact]
    public void Decode_BrokenOgg_ThrowsInvalidData()
    {
        var file = File.ReadAllBytes(Fixture("rain.ogg"));
        Array.Fill(file, (byte)0x5A, 64, file.Length - 64);

        Assert.Throws<InvalidDataException>(() => SoundDecoder.Decode(file));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    [InlineData(new byte[] { (byte)'I', (byte)'D', (byte)'3', 4 })]
    public void Decode_UnknownFormat_ThrowsInvalidData(byte[] file)
        => Assert.Throws<InvalidDataException>(() => SoundDecoder.Decode(file));

    [Theory]
    [InlineData(1, 3, 16)]
    [InlineData(1, 1, 12)]
    [InlineData(3, 1, 64)]
    [InlineData(2, 1, 16)]
    public void Decode_UnsupportedWavFormat_ThrowsInvalidData(int format, int channels, int bits)
        => Assert.Throws<InvalidDataException>(() => SoundDecoder.Decode(Wav.Create((ushort)format, channels, 8000, bits, new byte[48])));

    [Fact]
    public void Decode_WavWithoutData_ThrowsInvalidData()
    {
        var file = Wav.Pcm16(1, 8000, 1)[..36];

        var exception = Assert.Throws<InvalidDataException>(() => SoundDecoder.Decode(file));

        Assert.Equal("The WAV file has no data chunk.", exception.Message);
    }
}
