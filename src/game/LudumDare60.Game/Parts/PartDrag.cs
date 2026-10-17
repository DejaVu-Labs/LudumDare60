using System.Numerics;

namespace LudumDare60.Game.Parts;

/// <summary>
/// A part the player drags along a <see cref="LinePath"/>: a constrained drag, the touch loop of
/// docs/gamedesign/core-loop.md. Snapped into an end, the part sticks until it is pulled
/// <see cref="DragFeel.BreakawayAtStart"/> pixels and only strains towards the pointer; loose, it follows the pointer
/// with friction; moving into an end, it snaps there and the player lets go of it. A part let go between the ends
/// stays where it is, unless it is within an end's snap zone. Advance it once per fixed step with <see cref="Step(in PointerSample, float, bool)"/>.
/// </summary>
public sealed class PartDrag
{
    private float _grabOffset;

    // The stop the part last broke out of. It cannot snap back there before it has left that stop's snap zone, so
    // that jitter right after breaking free does not click it back in.
    private PartStop _leftStop;

    /// <param name="path">Where the part can go. Its position is the point of the part the player grabs.</param>
    /// <param name="grabRadius">How close to <see cref="Position"/> the pointer must be to grab the part.</param>
    /// <param name="feel">Breakaway, friction and snapping.</param>
    /// <param name="stop">Where the part starts: <see cref="PartStop.Start"/> or <see cref="PartStop.End"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A distance, rate or speed is negative or not a number, the strain
    /// is outside [0, 1], the snap zones of the two ends touch, or <paramref name="stop"/> is not an end.</exception>
    public PartDrag(LinePath path, float grabRadius, in DragFeel feel, PartStop stop = PartStop.Start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(grabRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(feel.BreakawayAtStart, nameof(feel));
        ArgumentOutOfRangeException.ThrowIfNegative(feel.BreakawayAtEnd, nameof(feel));
        ArgumentOutOfRangeException.ThrowIfNegative(feel.Follow, nameof(feel));
        ArgumentOutOfRangeException.ThrowIfNegative(feel.MaxSpeed, nameof(feel));
        ArgumentOutOfRangeException.ThrowIfNegative(feel.SnapDistance, nameof(feel));
        if (feel.Strain is not (>= 0 and <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(feel), feel.Strain, "The strain must lie in [0, 1].");
        }

        if (!(feel.SnapDistance * 2 < path.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(feel), feel.SnapDistance, "The snap zones of the two ends must not touch.");
        }

        if (stop is not (PartStop.Start or PartStop.End))
        {
            throw new ArgumentOutOfRangeException(nameof(stop), stop, "A part starts snapped into one of the ends.");
        }

        Path = path;
        GrabRadius = grabRadius;
        Feel = feel;
        Stop = stop;
        Distance = PreviousDistance = StopDistance(stop);
    }

    public LinePath Path { get; }

    public float GrabRadius { get; }

    public DragFeel Feel { get; }

    /// <summary>
    /// How far along <see cref="Path"/> the part is, from 0 to <see cref="LinePath.Length"/>.
    /// </summary>
    public float Distance { get; private set; }

    /// <summary>
    /// <see cref="Distance"/> before the last <see cref="Step(in PointerSample, float, bool)"/>, for interpolated rendering.
    /// </summary>
    public float PreviousDistance { get; private set; }

    /// <summary>
    /// The end the part is snapped into, or <see cref="PartStop.None"/> while it is loose.
    /// </summary>
    public PartStop Stop { get; private set; }

    /// <summary>
    /// Whether the player is dragging the part.
    /// </summary>
    public bool IsHeld { get; private set; }

    /// <summary>
    /// Whether the pointer is over the part and could grab it. False while the part is held.
    /// </summary>
    public bool IsHovered { get; private set; }

    /// <summary>
    /// Where the part is, in virtual pixels.
    /// </summary>
    public Vector2 Position => Path.PointAt(Distance);

    /// <summary>
    /// Where to draw the part, <paramref name="alpha"/> of the way from its position before the last step to its
    /// current one.
    /// </summary>
    public Vector2 GetRenderPosition(float alpha) => Path.PointAt(float.Lerp(PreviousDistance, Distance, alpha));

    /// <summary>
    /// Whether <paramref name="point"/> lies within <see cref="GrabRadius"/> of the part.
    /// </summary>
    public bool IsUnder(Vector2 point) => Vector2.DistanceSquared(point, Position) <= GrabRadius * GrabRadius;

    /// <summary>
    /// Advances the part by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <returns>What happened during the step.</returns>
    public DragEvents Step(in PointerSample input, float deltaTime) => Step(input, deltaTime, canGrab: true);

    /// <summary>
    /// Advances the part by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <param name="canGrab">Whether the part may be hovered and grabbed in this step: false while it is blocked, or
    /// while another part has the pointer. A part already held keeps moving either way.</param>
    /// <returns>What happened during the step.</returns>
    public DragEvents Step(in PointerSample input, float deltaTime, bool canGrab)
    {
        PreviousDistance = Distance;
        var events = DragEvents.None;
        if (!IsHeld)
        {
            IsHovered = canGrab && IsUnder(input.Position);
            if (!input.IsPressed || !IsHovered)
            {
                return events;
            }

            IsHeld = true;
            IsHovered = false;
            _grabOffset = Path.Project(input.Position) - Distance;
            events |= DragEvents.Grabbed;
        }

        if (!input.IsDown)
        {
            IsHeld = false;
            events |= DragEvents.Dropped;
            if (Stop != PartStop.None)
            {
                // A stuck part springs back from its strain.
                Distance = StopDistance(Stop);
                return events;
            }

            // A part let go within an end's snap zone settles into that end, even right after leaving it.
            if (Distance <= Feel.SnapDistance)
            {
                return events | SnapTo(PartStop.Start);
            }

            return Distance >= Path.Length - Feel.SnapDistance ? events | SnapTo(PartStop.End) : events;
        }

        var target = Math.Clamp(Path.Project(input.Position) - _grabOffset, 0, Path.Length);
        if (Stop != PartStop.None)
        {
            var atStart = Stop == PartStop.Start;
            var pull = atStart ? target : Path.Length - target;
            if (pull <= (atStart ? Feel.BreakawayAtStart : Feel.BreakawayAtEnd))
            {
                var strain = pull * Feel.Strain;
                Distance = atStart ? strain : Path.Length - strain;
                return events;
            }

            _leftStop = Stop;
            Stop = PartStop.None;
            events |= DragEvents.BrokeFree;
        }

        var approach = (target - Distance) * (1 - MathF.Exp(-Feel.Follow * deltaTime));
        var maxStep = Feel.MaxSpeed * deltaTime;
        Distance = Math.Clamp(Distance + Math.Clamp(approach, -maxStep, maxStep), 0, Path.Length);

        var inStartZone = Distance <= Feel.SnapDistance;
        var inEndZone = Distance >= Path.Length - Feel.SnapDistance;
        if ((_leftStop == PartStop.Start && !inStartZone) || (_leftStop == PartStop.End && !inEndZone))
        {
            _leftStop = PartStop.None;
        }

        if (inStartZone && target < Distance && _leftStop != PartStop.Start)
        {
            return events | SnapTo(PartStop.Start);
        }

        if (inEndZone && target > Distance && _leftStop != PartStop.End)
        {
            return events | SnapTo(PartStop.End);
        }

        return events;
    }

    private DragEvents SnapTo(PartStop stop)
    {
        Stop = stop;
        Distance = StopDistance(stop);
        IsHeld = false;
        return stop == PartStop.Start ? DragEvents.SnappedStart : DragEvents.SnappedEnd;
    }

    private float StopDistance(PartStop stop) => stop == PartStop.End ? Path.Length : 0;
}
