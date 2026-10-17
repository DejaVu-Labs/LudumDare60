using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Sounds;

/// <summary>
/// How a voice plays. Start from <c>new SoundOptions { ... }</c>: <c>default</c> has a zero volume and pitch, which
/// <see cref="Audio"/> rejects.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SoundOptions
{
    public SoundOptions()
    {
    }

    /// <summary>
    /// Full volume, original pitch, centered.
    /// </summary>
    public static SoundOptions Default => new();

    /// <summary>
    /// Linear gain: 1 plays the file as it is, 0 is silent, above 1 amplifies. At least 0.
    /// </summary>
    public float Volume { get; init; } = 1;

    /// <summary>
    /// Playback rate: 2 is an octave up and twice as fast, 0.5 an octave down. In (0, <see cref="Audio.MaxPitch"/>].
    /// </summary>
    public float Pitch { get; init; } = 1;

    /// <summary>
    /// Stereo position from -1 (left) through 0 (center) to 1 (right).
    /// </summary>
    public float Pan { get; init; }
}
