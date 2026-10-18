using System.Buffers.Binary;

namespace LudumDare60.Game.Tests;

public sealed class PlaceholderSoundsTests
{
    private static readonly byte[] s_scrub = PlaceholderSounds.CreateScrubLoop();

    private static float[] Samples(byte[] wav)
    {
        var samples = new float[(wav.Length - 44) / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (i * 2))) / (float)short.MaxValue;
        }

        return samples;
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        var sum = 0.0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    [Fact]
    public void CreateDoorBell_RingsAndDiesAway()
    {
        var bell = PlaceholderSounds.CreateDoorBell();
        var samples = Samples(bell);
        var rate = PlaceholderSounds.ScrubSampleRate;

        Assert.Equal(bell, PlaceholderSounds.CreateDoorBell());
        Assert.Equal((int)(rate * PlaceholderSounds.DoorBellSeconds), samples.Length);
        Assert.All(samples, sample => Assert.InRange(MathF.Abs(sample), 0, 0.71f));
        var start = Rms(samples.AsSpan(0, rate / 5));
        var end = Rms(samples.AsSpan(samples.Length - (rate / 10)));
        Assert.True(start > 0.1, $"The strikes ring at {start} RMS.");
        Assert.True(end < start / 20, $"The tail is still at {end} RMS against {start}.");
        Assert.InRange(MathF.Abs(samples[^1]), 0, 0.001f);
    }

    [Fact]
    public void CreateScrubLoop_IsMonoPcmAt44100()
    {
        Assert.Equal("RIFF"u8.ToArray(), s_scrub[..4]);
        Assert.Equal(PlaceholderSounds.ScrubSampleRate, BinaryPrimitives.ReadInt32LittleEndian(s_scrub.AsSpan(24)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(s_scrub.AsSpan(22)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(s_scrub.AsSpan(34)));
        Assert.Equal((int)(PlaceholderSounds.ScrubSampleRate * PlaceholderSounds.ScrubSeconds), Samples(s_scrub).Length);
    }

    [Fact]
    public void CreateScrubLoop_SameBytesEveryTime()
        => Assert.Equal(s_scrub, PlaceholderSounds.CreateScrubLoop());

    [Fact]
    public void CreateScrubLoop_LoudButNotClipped()
    {
        var samples = Samples(s_scrub);

        Assert.InRange(Rms(samples), 0.12, 0.3);
        Assert.All(samples, sample => Assert.InRange(MathF.Abs(sample), 0, 0.76f));
    }

    [Fact]
    public void CreateScrubLoop_IsBright()
    {
        // A sine of frequency f has first-difference energy 4 sin²(pi f / rate) times its own, so the ratio gives
        // the frequency the energy centers on. A wire brush on steel sits around 3 to 6 kHz.
        var samples = Samples(s_scrub);
        var differences = new float[samples.Length - 1];
        for (var i = 1; i < samples.Length; i++)
        {
            differences[i - 1] = samples[i] - samples[i - 1];
        }

        var ratio = Math.Pow(Rms(differences) / Rms(samples), 2);
        var center = PlaceholderSounds.ScrubSampleRate / Math.PI * Math.Asin(Math.Sqrt(ratio) / 2);

        Assert.InRange(center, 3000, 7000);
    }

    [Fact]
    public void CreateScrubLoop_WrapsWithoutAClick()
    {
        var samples = Samples(s_scrub);
        var differences = new float[samples.Length - 1];
        for (var i = 1; i < samples.Length; i++)
        {
            differences[i - 1] = samples[i] - samples[i - 1];
        }

        var seam = MathF.Abs(samples[0] - samples[^1]);

        Assert.True(seam < 4 * Rms(differences), $"The seam jumps {seam}, adjacent samples differ by {Rms(differences)} (RMS).");
    }
}
