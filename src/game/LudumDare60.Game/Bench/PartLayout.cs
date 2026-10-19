using System.Numerics;
using LudumDare60.Engine.Rendering;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// Where one revolver part lies on the bench and how it looks and feels: everything about a part that is not in the
/// order data.
/// </summary>
/// <param name="Path">Seated at its start, out at its end.</param>
/// <param name="GrabRadius">How close to the part's grab point the pointer must be to take it when it has no
/// <paramref name="Grab"/> shape, and how far past the dock tolerance the hand pulls it off its rail sideways.</param>
/// <param name="Feel">Breakaway, friction and snapping.</param>
/// <param name="Sprite">The part in the parts sheet.</param>
/// <param name="Glow">Its glow outline in the parts sheet, one pixel larger on every side.</param>
/// <param name="Origin">The grab point in sprite pixels: the sprite is drawn with it at the part's position.</param>
/// <param name="Layer">What the part is drawn over and under while it is in the gun.</param>
/// <param name="ClickPitch">Pitch of the part's clicks: heavier parts click lower.</param>
/// <param name="TurnedSprite">How the part looks one chamber further round, for the cylinder; null for parts that do
/// not turn.</param>
/// <param name="Free">How the part moves on the work surface once it is out, and how close to its rail it must be put
/// to stick to it again; null for a part that stays on its rail.</param>
/// <param name="BrokenSprite">How the part looks broken, for an order that has it swapped for a spare; null for a part
/// the bench has no spare of.</param>
/// <param name="Grab">The pixels the part is taken by; null to take it within <paramref name="GrabRadius"/> of its
/// grab point. A part drawn under the frame is not taken where the frame hides it.</param>
public sealed record PartLayout(
    PartPath Path,
    float GrabRadius,
    DragFeel Feel,
    IntRect Sprite,
    IntRect Glow,
    Vector2 Origin,
    PartLayer Layer,
    float ClickPitch,
    IntRect? TurnedSprite = null,
    FreeMove? Free = null,
    IntRect? BrokenSprite = null,
    GrabShape? Grab = null)
{
    /// <summary>
    /// Where the part sits in the gun as the customer brings it.
    /// </summary>
    public Vector2 Seat => Path.Start;
}
