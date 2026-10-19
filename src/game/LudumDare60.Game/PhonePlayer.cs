using System.Numerics;

namespace LudumDare60.Game;

/// <summary>
/// The music on the phone lying on the bench. It is off at first, and only the rain plays; each click on the phone
/// plays the next track, and the click after the last one turns the music off again.
/// </summary>
public sealed class PhonePlayer
{
    private readonly int _trackCount;

    /// <param name="trackCount">How many tracks the phone has (<see cref="AssetPaths.MusicTracks"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trackCount"/> is less than 1.</exception>
    public PhonePlayer(int trackCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(trackCount, 1);
        _trackCount = trackCount;
    }

    /// <summary>
    /// The track playing, from 0, or -1 while the music is off.
    /// </summary>
    public int Track { get; private set; } = -1;

    /// <summary>
    /// Whether a track plays.
    /// </summary>
    public bool IsPlaying => Track >= 0;

    /// <summary>
    /// Goes on to the next track, or turns the music off after the last one. Returns the new <see cref="Track"/>.
    /// </summary>
    public int Next()
    {
        Track = Track + 1 < _trackCount ? Track + 1 : -1;
        return Track;
    }

    /// <summary>
    /// Whether <paramref name="point"/>, in virtual pixels, lies on the phone: on an opaque pixel of its sprite, drawn
    /// with its top-left corner at <see cref="ShopDecor.PhoneAt"/>. <paramref name="opaque"/> holds the sprite's pixels
    /// row by row, <paramref name="width"/> to a row.
    /// </summary>
    public static bool IsOnPhone(Vector2 point, ReadOnlySpan<bool> opaque, int width)
    {
        if (width <= 0)
        {
            return false;
        }

        var local = point - ShopDecor.PhoneAt;
        var x = (int)MathF.Floor(local.X);
        var y = (int)MathF.Floor(local.Y);
        var height = opaque.Length / width;
        return (uint)x < (uint)width && (uint)y < (uint)height && opaque[(y * width) + x];
    }
}
