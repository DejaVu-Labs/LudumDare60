using System.Numerics;
using LudumDare60.Engine.Rendering;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// Where one revolver part lies on the bench and how it looks and feels: everything about a part that is not in the
/// order data.
/// </summary>
/// <param name="Path">Seated at its start, out at its end.</param>
/// <param name="GrabRadius">How close to the part's grab point the pointer must be to take it.</param>
/// <param name="Feel">Breakaway, friction and snapping.</param>
/// <param name="Sprite">The part in the parts sheet.</param>
/// <param name="Glow">Its glow outline in the parts sheet, one pixel larger on every side.</param>
/// <param name="Origin">The grab point in sprite pixels: the sprite is drawn with it at the part's position.</param>
/// <param name="UnderFrame">Whether the frame is drawn over the part, hiding what of it is still inside.</param>
/// <param name="ClickPitch">Pitch of the part's clicks: heavier parts click lower.</param>
/// <param name="TurnedSprite">How the part looks one chamber further round, for the cylinder; null for parts that do
/// not turn.</param>
public sealed record PartLayout(
    LinePath Path,
    float GrabRadius,
    DragFeel Feel,
    IntRect Sprite,
    IntRect Glow,
    Vector2 Origin,
    bool UnderFrame,
    float ClickPitch,
    IntRect? TurnedSprite = null);
