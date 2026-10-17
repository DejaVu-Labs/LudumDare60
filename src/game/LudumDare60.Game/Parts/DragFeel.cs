using System.Runtime.InteropServices;

namespace LudumDare60.Game.Parts;

/// <summary>
/// How heavy a <see cref="PartDrag"/> feels. Distances are in virtual pixels, times in seconds. Start from
/// <c>new DragFeel { ... }</c>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DragFeel
{
    public DragFeel()
    {
    }

    /// <summary>
    /// The feel of a pin pulled out of its seat: stuck at first, then sliding with some friction.
    /// </summary>
    public static DragFeel Default => new();

    /// <summary>
    /// How far the pointer must pull a part out of <see cref="PartStop.Start"/> before it comes loose. Until then the
    /// part only strains towards the pointer.
    /// </summary>
    public float BreakawayAtStart { get; init; } = 8;

    /// <summary>
    /// Like <see cref="BreakawayAtStart"/>, for a part leaving <see cref="PartStop.End"/>. 0 moves it at once.
    /// </summary>
    public float BreakawayAtEnd { get; init; }

    /// <summary>
    /// How much of the pull a stuck part shows as motion, from 0 (none) to 1 (all of it).
    /// </summary>
    public float Strain { get; init; } = 0.25f;

    /// <summary>
    /// How fast a loose part closes the gap to the pointer: the gap shrinks to 1/e in <c>1 / Follow</c> seconds.
    /// Lower feels heavier.
    /// </summary>
    public float Follow { get; init; } = 14;

    /// <summary>
    /// The fastest a loose part moves, in pixels per second, however fast the pointer goes.
    /// </summary>
    public float MaxSpeed { get; init; } = 160;

    /// <summary>
    /// How close to an end a part moving towards it must come to snap in.
    /// </summary>
    public float SnapDistance { get; init; } = 3;
}
