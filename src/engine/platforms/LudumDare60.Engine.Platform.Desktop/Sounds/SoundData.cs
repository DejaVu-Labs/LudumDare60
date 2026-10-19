namespace LudumDare60.Engine.Platform.Desktop.Sounds;

/// <summary>
/// A decoded sound: 32-bit float samples in [-1, 1], interleaved when there are two channels.
/// </summary>
internal sealed class SoundData
{
    /// <param name="samples"><c>frames * channels</c> samples.</param>
    /// <param name="channels">1 (mono) or 2 (stereo).</param>
    /// <param name="sampleRate">Frames per second, at least 1.</param>
    public SoundData(float[] samples, int channels, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        Samples = samples;
        Channels = channels;
        SampleRate = sampleRate;
        FrameCount = samples.Length / channels;
    }

    public float[] Samples { get; }

    public int Channels { get; }

    public int SampleRate { get; }

    public int FrameCount { get; }
}
