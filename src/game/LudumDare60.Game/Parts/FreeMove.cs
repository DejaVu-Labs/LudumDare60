using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Game.Parts;

/// <summary>
/// How a <see cref="PartDrag"/> that has come out of the gun moves on the mat, and how it finds its way back onto its
/// rail.
/// </summary>
/// <param name="Min">The top-left corner of the area the part's grab point stays in: the work surface.</param>
/// <param name="Max">The bottom-right corner of that area.</param>
/// <param name="DockRadius">How close to its rail a loose part must come to stick to it: the tolerance for putting
/// it back.</param>
/// <param name="DockSpan">How much of the rail, from its outer end inwards, the part can stick to: a few pixels for a
/// part that must go in tip first. Not used when <paramref name="SeatsOnDock"/> is set.</param>
/// <param name="SeatsOnDock">Whether the part has no rail to go back along: brought within
/// <paramref name="DockRadius"/> of its seat it drops straight into it, like a cylinder into its window.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FreeMove(Vector2 Min, Vector2 Max, float DockRadius, float DockSpan, bool SeatsOnDock = false);
