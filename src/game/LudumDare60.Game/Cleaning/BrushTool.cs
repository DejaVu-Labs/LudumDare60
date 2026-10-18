using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Cleaning;

/// <summary>
/// The scrubbing brush, lying at its place on the bench. A press on it picks it up; while the button stays down its
/// bristles follow the pointer, and let go it slides back to its place. Advanced once per fixed step.
/// </summary>
public sealed class BrushTool
{
    /// <summary>
    /// How fast a brush that was let go slides home: the gap shrinks to 1/e in <c>1 / ReturnRate</c> seconds.
    /// </summary>
    public const float ReturnRate = 18;

    // Closer than this to its place, a returning brush lands.
    private const float LandDistance = 0.5f;

    /// <param name="rest">Where the bristles are while the brush lies at its place.</param>
    /// <param name="grabMin">The top-left corner of the area that picks the brush up, relative to the bristles.</param>
    /// <param name="grabMax">The bottom-right corner of that area, relative to the bristles.</param>
    public BrushTool(Vector2 rest, Vector2 grabMin, Vector2 grabMax)
    {
        Rest = rest;
        GrabMin = grabMin;
        GrabMax = grabMax;
        Position = PreviousPosition = rest;
    }

    public Vector2 Rest { get; }

    public Vector2 GrabMin { get; }

    public Vector2 GrabMax { get; }

    /// <summary>
    /// Where the bristles are, in virtual pixels.
    /// </summary>
    public Vector2 Position { get; private set; }

    /// <summary>
    /// <see cref="Position"/> before the last <see cref="Step"/>, for interpolated rendering and speed.
    /// </summary>
    public Vector2 PreviousPosition { get; private set; }

    public bool IsHeld { get; private set; }

    /// <summary>
    /// Whether the pointer is over the brush and could pick it up. False while it is held.
    /// </summary>
    public bool IsHovered { get; private set; }

    /// <summary>
    /// Whether the brush lies at its place.
    /// </summary>
    public bool IsResting => !IsHeld && Position == Rest;

    /// <summary>
    /// Whether <paramref name="point"/> lies on the brush.
    /// </summary>
    public bool IsUnder(Vector2 point)
    {
        var local = point - Position;
        return local.X >= GrabMin.X && local.Y >= GrabMin.Y && local.X <= GrabMax.X && local.Y <= GrabMax.Y;
    }

    /// <summary>
    /// Where to draw the brush, <paramref name="alpha"/> of the way from its position before the last step to its
    /// current one.
    /// </summary>
    public Vector2 GetRenderPosition(float alpha) => Vector2.Lerp(PreviousPosition, Position, alpha);

    /// <summary>
    /// Advances the brush by one fixed step.
    /// </summary>
    /// <param name="input">The pointer at the end of the step.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    /// <param name="canGrab">Whether the brush may be hovered and picked up: false while the player holds a part.</param>
    public BrushEvents Step(in PointerSample input, float deltaTime, bool canGrab)
    {
        PreviousPosition = Position;
        var events = BrushEvents.None;
        if (!IsHeld)
        {
            IsHovered = canGrab && IsUnder(input.Position);
            if (!input.IsPressed || !IsHovered)
            {
                return events | SlideHome(deltaTime);
            }

            IsHeld = true;
            IsHovered = false;
            events |= BrushEvents.PickedUp;
        }

        if (!input.IsDown)
        {
            IsHeld = false;
            return events | BrushEvents.PutDown;
        }

        // In the hand, the bristles sit under the pointer.
        Position = input.Position;
        return events;
    }

    private BrushEvents SlideHome(float deltaTime)
    {
        if (Position == Rest)
        {
            return BrushEvents.None;
        }

        Position = Vector2.Lerp(Position, Rest, 1 - MathF.Exp(-ReturnRate * deltaTime));
        if (Vector2.DistanceSquared(Position, Rest) > LandDistance * LandDistance)
        {
            return BrushEvents.None;
        }

        Position = Rest;
        return BrushEvents.Landed;
    }
}
