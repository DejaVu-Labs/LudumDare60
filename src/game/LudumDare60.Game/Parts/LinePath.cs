using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Game.Parts;

/// <summary>
/// A straight path a part slides along, from <see cref="Start"/> (where it sits in the gun) to <see cref="End"/>
/// (where it is free), in virtual pixels.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LinePath
{
    /// <exception cref="ArgumentException"><paramref name="start"/> and <paramref name="end"/> are the same point.</exception>
    public LinePath(Vector2 start, Vector2 end)
    {
        Length = Vector2.Distance(start, end);
        if (!(Length > 0))
        {
            throw new ArgumentException("A path needs two different points.", nameof(end));
        }

        Start = start;
        End = end;
        Direction = (end - start) / Length;
    }

    public Vector2 Start { get; }

    public Vector2 End { get; }

    /// <summary>
    /// Distance from <see cref="Start"/> to <see cref="End"/>.
    /// </summary>
    public float Length { get; }

    /// <summary>
    /// Unit vector from <see cref="Start"/> towards <see cref="End"/>.
    /// </summary>
    public Vector2 Direction { get; }

    /// <summary>
    /// The point <paramref name="distance"/> pixels from <see cref="Start"/> along the path (not clamped).
    /// </summary>
    public Vector2 PointAt(float distance) => Start + (Direction * distance);

    /// <summary>
    /// How far along the path <paramref name="point"/> lies: its projection onto the path's line, measured from
    /// <see cref="Start"/>. Not clamped, so it is negative before the start and above <see cref="Length"/> past the end.
    /// </summary>
    public float Project(Vector2 point) => Vector2.Dot(point - Start, Direction);
}
