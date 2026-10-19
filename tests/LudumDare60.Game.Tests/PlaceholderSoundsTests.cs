using System.Buffers.Binary;

namespace LudumDare60.Game.Tests;

public sealed class PlaceholderSoundsTests
{
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
        var rate = PlaceholderSounds.BellSampleRate;

        Assert.Equal(bell, PlaceholderSounds.CreateDoorBell());
        Assert.Equal((int)(rate * PlaceholderSounds.DoorBellSeconds), samples.Length);
        Assert.All(samples, sample => Assert.InRange(MathF.Abs(sample), 0, 0.71f));
        var start = Rms(samples.AsSpan(0, rate / 5));
        var end = Rms(samples.AsSpan(samples.Length - (rate / 10)));
        Assert.True(start > 0.1, $"The strikes ring at {start} RMS.");
        Assert.True(end < start / 20, $"The tail is still at {end} RMS against {start}.");
        Assert.InRange(MathF.Abs(samples[^1]), 0, 0.001f);
    }
}
