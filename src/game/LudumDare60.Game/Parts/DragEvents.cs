namespace LudumDare60.Game.Parts;

/// <summary>
/// What happened to a <see cref="PartDrag"/> during one <see cref="PartDrag.Step(in PointerSample, float, bool)"/>, for sounds and effects.
/// </summary>
[Flags]
public enum DragEvents
{
    None = 0,

    /// <summary>
    /// The pointer went down on the part.
    /// </summary>
    Grabbed = 1,

    /// <summary>
    /// The part was pulled hard enough to leave the stop it was snapped in.
    /// </summary>
    BrokeFree = 2,

    /// <summary>
    /// The part snapped into <see cref="PartStop.Start"/>: it was moved or let go there. The player no longer holds it.
    /// </summary>
    SnappedStart = 4,

    /// <summary>
    /// Like <see cref="SnappedStart"/>, at <see cref="PartStop.End"/>.
    /// </summary>
    SnappedEnd = 8,

    /// <summary>
    /// The pointer went up while holding the part.
    /// </summary>
    Dropped = 16,

    /// <summary>
    /// The pointer went down on the part while something blocked it, so it did not move. Reported by
    /// <see cref="PartAssembly"/>.
    /// </summary>
    Refused = 32,
}
