using System.Numerics;

namespace LudumDare60.Game.Parts;

/// <summary>
/// A part the player drags along a <see cref="PartPath"/> (a line, or a screw turned in circles): a constrained drag, the touch loop of
/// docs/gamedesign/core-loop.md. Snapped into an end, the part sticks until it is pulled
/// <see cref="DragFeel.BreakawayAtStart"/> pixels and only strains towards the pointer; loose, it follows the pointer
/// with friction; moving into an end, it snaps there and the player lets go of it. A part let go between the ends
/// stays where it is, unless it is within an end's snap zone. Advance it once per fixed step with
/// <see cref="Step(in PointerSample, float, bool, bool)"/>.
/// <para>With a <see cref="FreeMove"/>, a part that clicks out at the end of its rail stays in the hand and can be
/// carried anywhere on the work surface and put down there. Brought back close enough to its rail it sticks to it and
/// can be pushed in again; pulled off to the side near the end of the rail, it comes loose again.</para>
/// </summary>
public sealed class PartDrag
{
    // How fast a loose part follows the hand: the gap shrinks to 1/e in 1/FreeFollow seconds.
    private const float FreeFollow = 30;

    private readonly FreeMove? _free;
    private float _grabOffset;

    // Where the hand is along the path, wound up over a screw's turns so that circling on keeps counting.
    private float _along;
    private Vector2 _freePosition;
    private Vector2 _freeGrab;

    // A part that just left its rail cannot stick to it again before it has left the dock zone, so it does not cling
    // to the rail it is being pulled off.
    private bool _dockBlocked;
    private bool _wasInDockZone;

    // The stop the part last broke out of. It cannot snap back there before it has left that stop's snap zone, so
    // that jitter right after breaking free does not click it back in.
    private PartStop _leftStop;

    /// <param name="path">Where the part can go. Its position is the point of the part the player grabs.</param>
    /// <param name="grabRadius">How close to <see cref="Position"/> the pointer must be to grab the part.</param>
    /// <param name="feel">Breakaway, friction and snapping.</param>
    /// <param name="stop">Where the part starts: <see cref="PartStop.Start"/> or <see cref="PartStop.End"/>.</param>
    /// <param name="free">How the part moves once it is out, or null to keep it on its rail.</param>
    /// <param name="shape">The pixels the part is taken by, or null to take it within <paramref name="grabRadius"/> of
    /// <see cref="Position"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A distance, rate or speed is negative or not a number, the strain
    /// is outside [0, 1], the snap zones of the two ends touch, or <paramref name="stop"/> is not an end.</exception>
    public PartDrag(PartPath path, float grabRadius, in DragFeel feel, PartStop stop = PartStop.Start, FreeMove? free = null, GrabShape? shape = null)
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
        _free = free;
        Shape = shape;
        Distance = PreviousDistance = StopDistance(stop);
        PreviousPosition = Position;
    }

    public PartPath Path { get; }

    public float GrabRadius { get; }

    /// <summary>
    /// The pixels the part is taken by, or null for a part taken within <see cref="GrabRadius"/> of its position.
    /// </summary>
    public GrabShape? Shape { get; }

    public DragFeel Feel { get; }

    /// <summary>
    /// How far along <see cref="Path"/> the part is, from 0 to <see cref="PartPath.Length"/>.
    /// </summary>
    public float Distance { get; private set; }

    /// <summary>
    /// <see cref="Distance"/> before the last <see cref="Step(in PointerSample, float, bool)"/>, for interpolated rendering.
    /// </summary>
    public float PreviousDistance { get; private set; }

    /// <summary>
    /// The end the part is snapped into, or <see cref="PartStop.None"/> while it is loose on its rail. A part off its
    /// rail is out: <see cref="PartStop.End"/>.
    /// </summary>
    public PartStop Stop { get; private set; }

    /// <summary>
    /// Whether the part is off its rail, loose on the work surface.
    /// </summary>
    public bool IsFree { get; private set; }

    /// <summary>
    /// <see cref="Position"/> before the last step, for interpolated rendering.
    /// </summary>
    public Vector2 PreviousPosition { get; private set; }

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
    public Vector2 Position => IsFree ? _freePosition : Path.PointAt(Distance);

    /// <summary>
    /// Where to draw the part, <paramref name="alpha"/> of the way from its position before the last step to its
    /// current one.
    /// </summary>
    public Vector2 GetRenderPosition(float alpha) => Vector2.Lerp(PreviousPosition, Position, alpha);

    /// <summary>
    /// Whether <paramref name="point"/> lies on the part's <see cref="Shape"/>, or within <see cref="GrabRadius"/> of
    /// the part when it has none.
    /// </summary>
    public bool IsUnder(Vector2 point)
        => Shape is { } shape ? shape.Contains(point - Position) : Vector2.DistanceSquared(point, Position) <= GrabRadius * GrabRadius;

    /// <summary>
    /// Advances the part by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <returns>What happened during the step.</returns>
    public DragEvents Step(in PointerSample input, float deltaTime) => Step(input, deltaTime, canGrab: true, canDock: true);

    /// <inheritdoc cref="Step(in PointerSample, float, bool, bool)"/>
    public DragEvents Step(in PointerSample input, float deltaTime, bool canGrab) => Step(input, deltaTime, canGrab, canDock: true);

    /// <summary>
    /// Advances the part by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <param name="canGrab">Whether the part may be hovered and grabbed in this step: false while it is blocked, or
    /// while another part has the pointer. A part already held keeps moving either way.</param>
    /// <param name="canDock">Whether a loose part may stick to its rail: false while something keeps it from going
    /// back. Brought to its rail anyway, it reports <see cref="DragEvents.DockRefused"/>.</param>
    /// <returns>What happened during the step.</returns>
    public DragEvents Step(in PointerSample input, float deltaTime, bool canGrab, bool canDock)
    {
        PreviousDistance = Distance;
        PreviousPosition = Position;
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
            _along = Path.Project(input.Position, Distance);
            _grabOffset = _along - Distance;
            _freeGrab = input.Position - Position;
            // Taken up by its rail, a part that may not go back says so at once.
            _dockBlocked = false;
            _wasInDockZone = false;
            events |= DragEvents.Grabbed;
        }

        if (IsFree)
        {
            return events | StepFree(input, deltaTime, canDock);
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

        var along = Path.Project(input.Position, _along);
        _along = Math.Clamp(along, _grabOffset - Path.Slack, _grabOffset + Path.Length + Path.Slack);
        var target = Math.Clamp(along - _grabOffset, 0, Path.Length);
        if (_free is { } free && Stop != PartStop.Start && Distance >= Path.Length - free.DockSpan && IsPulledOff(along, input, free))
        {
            return events | Leave();
        }

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
            if (_free is null)
            {
                return events | SnapTo(PartStop.End);
            }

            // Out of the gun with a click, and still in the hand.
            Distance = Path.Length;
            return events | DragEvents.SnappedEnd | Leave();
        }

        return events;
    }

    /// <summary>
    /// Puts the part into <paramref name="stop"/> at once and out of the player's hand, without events: a spring or
    /// another part moved it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stop"/> is not an end.</exception>
    public void ReturnTo(PartStop stop)
    {
        if (stop is not (PartStop.Start or PartStop.End))
        {
            throw new ArgumentOutOfRangeException(nameof(stop), stop, "A part returns to one of the ends.");
        }

        Stop = stop;
        IsFree = false;
        Distance = PreviousDistance = StopDistance(stop);
        PreviousPosition = Position;
        IsHeld = false;
        IsHovered = false;
        _leftStop = PartStop.None;
    }

    private DragEvents SnapTo(PartStop stop)
    {
        Stop = stop;
        Distance = StopDistance(stop);
        IsHeld = false;
        return stop == PartStop.Start ? DragEvents.SnappedStart : DragEvents.SnappedEnd;
    }

    private float StopDistance(PartStop stop) => stop == PartStop.End ? Path.Length : 0;

    /// <summary>
    /// Whether the hand pulls a part off its rail: off to the side by more than the dock tolerance plus the grab, or,
    /// from the end of the rail, on out past it (a screw turned on out of its hole). A part moving out along the rail
    /// clicks out at the end instead. <paramref name="along"/> is where the hand is along the path.
    /// </summary>
    private bool IsPulledOff(float along, in PointerSample input, FreeMove free)
    {
        var side = Path.SideDistance(input.Position);
        var beyond = along - _grabOffset - Path.Length;
        return side > free.DockRadius + GrabRadius || (Stop == PartStop.End && beyond > free.DockRadius);
    }

    /// <summary>
    /// Takes the part off its rail, still in the hand.
    /// </summary>
    private DragEvents Leave()
    {
        _freePosition = Position;
        IsFree = true;
        Stop = PartStop.End;

        // The hand keeps holding the part where it took it, along the rail: a part pulled off sideways catches up with
        // the hand instead of keeping the gap. A screw has no rail and comes to the hand.
        _freeGrab = Path.Direction * _grabOffset;
        _dockBlocked = true;
        _wasInDockZone = true;
        return DragEvents.None;
    }

    /// <summary>
    /// Whether the loose part lies close enough to its rail to stick to it, and where on the rail: near its seat for a
    /// part that seats on docking.
    /// </summary>
    private bool InDockZone(out float distance)
    {
        var free = _free!.Value;
        distance = free.SeatsOnDock ? 0 : Math.Clamp(Path.Project(_freePosition, Path.Length), Path.Length - free.DockSpan, Path.Length);
        return Vector2.Distance(_freePosition, Path.PointAt(distance)) <= free.DockRadius;
    }

    private DragEvents StepFree(in PointerSample input, float deltaTime, bool canDock)
    {
        var free = _free!.Value;
        if (!input.IsDown)
        {
            // Put down where it is.
            IsHeld = false;
            return DragEvents.Dropped;
        }

        var target = Vector2.Clamp(input.Position - _freeGrab, free.Min, free.Max);
        _freePosition += (target - _freePosition) * (1 - MathF.Exp(-FreeFollow * deltaTime));
        var inZone = InDockZone(out var dockAt);
        var entered = inZone && !_wasInDockZone;
        _wasInDockZone = inZone;
        if (!inZone)
        {
            _dockBlocked = false;
            return DragEvents.None;
        }

        if (_dockBlocked)
        {
            return DragEvents.None;
        }

        if (!canDock)
        {
            return entered ? DragEvents.DockRefused : DragEvents.None;
        }

        if (free.SeatsOnDock)
        {
            // It drops into its seat with a click and out of the hand.
            IsFree = false;
            _leftStop = PartStop.None;
            return SnapTo(PartStop.Start);
        }

        // It sticks to its rail and can be pushed in from here.
        IsFree = false;
        Distance = dockAt;
        Stop = dockAt >= Path.Length ? PartStop.End : PartStop.None;
        _leftStop = PartStop.None;
        _along = Path.Project(input.Position, Distance);
        _grabOffset = _along - Distance;
        return DragEvents.Docked;
    }
}
