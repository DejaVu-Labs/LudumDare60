using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Cleaning;

/// <summary>
/// The scrubbing brush on the bench. A press on it picks it up; while the button stays down its bristles follow the
/// pointer over the work surface, and let go it lies where it was put down. Advanced once per fixed step.
/// </summary>
public sealed class BrushTool
{
    private readonly Vector2 _min;
    private readonly Vector2 _max;

    /// <param name="rest">Where the bristles are at first.</param>
    /// <param name="grabMin">The top-left corner of the area that picks the brush up, relative to the bristles.</param>
    /// <param name="grabMax">The bottom-right corner of that area, relative to the bristles.</param>
    /// <param name="min">The top-left corner of the work surface the bristles stay on.</param>
    /// <param name="max">The bottom-right corner of that surface.</param>
    public BrushTool(Vector2 rest, Vector2 grabMin, Vector2 grabMax, Vector2 min, Vector2 max)
    {
        GrabMin = grabMin;
        GrabMax = grabMax;
        _min = min;
        _max = max;
        Position = PreviousPosition = rest;
    }

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
    /// <param name="canGrab">Whether the brush may be hovered and picked up: false while the player holds a part.</param>
    public BrushEvents Step(in PointerSample input, bool canGrab)
    {
        PreviousPosition = Position;
        var events = BrushEvents.None;
        if (!IsHeld)
        {
            IsHovered = canGrab && IsUnder(input.Position);
            if (!input.IsPressed || !IsHovered)
            {
                return events;
            }

            IsHeld = true;
            IsHovered = false;
            events |= BrushEvents.PickedUp;
        }

        // In the hand, the bristles sit under the pointer, on the work surface.
        Position = Vector2.Clamp(input.Position, _min, _max);
        if (!input.IsDown)
        {
            IsHeld = false;
            events |= BrushEvents.PutDown;
        }

        return events;
    }
}
