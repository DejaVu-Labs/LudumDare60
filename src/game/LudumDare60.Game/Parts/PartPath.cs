using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Game.Parts;

/// <summary>
/// Where a <see cref="PartDrag"/> can go, from its seat in the gun (distance 0) to out (<see cref="Length"/>): a
/// straight <see cref="LinePath"/>, or a screw turned around its middle. A screw does not move while it turns: the
/// hand circles it counterclockwise to back it out and clockwise to drive it in, and its distance is the turn
/// measured along a circle of <see cref="TurnRadius"/> pixels, so drag feel, speeds and snaps read the same for both.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PartPath
{
    /// <summary>
    /// The radius a screw's turn is measured along, in virtual pixels: one turn is <c>2 * pi * TurnRadius</c> long.
    /// </summary>
    public const float DefaultTurnRadius = 8;

    private PartPath(Vector2 start, Vector2 end, float length, Vector2 direction, float turnRadius)
    {
        Start = start;
        End = end;
        Length = length;
        Direction = direction;
        TurnRadius = turnRadius;
    }

    /// <summary>
    /// The seat: the start of a line, the middle of a screw.
    /// </summary>
    public Vector2 Start { get; }

    /// <summary>
    /// Out: the end of a line, the middle of a screw.
    /// </summary>
    public Vector2 End { get; }

    public float Length { get; }

    /// <summary>
    /// Unit vector from <see cref="Start"/> towards <see cref="End"/>; zero for a screw.
    /// </summary>
    public Vector2 Direction { get; }

    /// <summary>
    /// The radius a screw's turn is measured along; 0 for a line.
    /// </summary>
    public float TurnRadius { get; }

    /// <summary>
    /// Whether the part is a screw, turned rather than moved.
    /// </summary>
    public bool IsTurn => TurnRadius > 0;

    /// <summary>
    /// How far the hand may wind a screw past either end before the part follows it back, along the turn: half a
    /// turn. Unlimited for a line, whose projection does not wind.
    /// </summary>
    public float Slack => IsTurn ? MathF.PI * TurnRadius : float.PositiveInfinity;

    public static implicit operator PartPath(LinePath line) => FromLinePath(line);

    public static PartPath FromLinePath(LinePath line) => new(line.Start, line.End, line.Length, line.Direction, 0);

    /// <summary>
    /// A screw at <paramref name="center"/> that comes out after <paramref name="turns"/> full turns.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turns"/> or <paramref name="turnRadius"/> is not
    /// positive.</exception>
    public static PartPath Screw(Vector2 center, float turns, float turnRadius = DefaultTurnRadius)
    {
        if (!(turns > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(turns), turns, "A screw needs a positive number of turns.");
        }

        if (!(turnRadius > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(turnRadius), turnRadius, "A screw needs a positive turn radius.");
        }

        return new(center, center, turns * MathF.Tau * turnRadius, Vector2.Zero, turnRadius);
    }

    /// <summary>
    /// Where the part is <paramref name="distance"/> along the path (not clamped). A screw stays at its middle.
    /// </summary>
    public Vector2 PointAt(float distance) => IsTurn ? Start : Start + (Direction * distance);

    /// <summary>
    /// How much a screw at <paramref name="distance"/> has turned from its seat, in radians, as sprites turn:
    /// clockwise on screen is positive, so backing it out is negative. 0 for a line.
    /// </summary>
    public float AngleAt(float distance) => IsTurn ? -distance / TurnRadius : 0;

    /// <summary>
    /// How far along the path <paramref name="point"/> lies. For a line, its projection onto the line from
    /// <see cref="Start"/>. For a screw, the hand's angle around the middle, counterclockwise on screen, as a distance
    /// along the turn: of all the turns that put the hand there, the one closest to <paramref name="near"/>. Not
    /// clamped.
    /// </summary>
    public float Project(Vector2 point, float near)
    {
        if (!IsTurn)
        {
            return Vector2.Dot(point - Start, Direction);
        }

        // Screen y points down, so the angle grows clockwise; backing a screw out turns it the other way.
        var offset = point - Start;
        var along = -MathF.Atan2(offset.Y, offset.X) * TurnRadius;
        var turn = MathF.Tau * TurnRadius;
        return along + (MathF.Round((near - along) / turn) * turn);
    }

    /// <summary>
    /// How far <paramref name="point"/> lies off a line, to the side; 0 for a screw, which has no side.
    /// </summary>
    public float SideDistance(Vector2 point)
    {
        if (IsTurn)
        {
            return 0;
        }

        var fromStart = point - Start;
        return MathF.Abs((fromStart.X * Direction.Y) - (fromStart.Y * Direction.X));
    }
}
