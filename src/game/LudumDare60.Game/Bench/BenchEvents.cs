using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Bench;

/// <summary>
/// What happened on the bench during one <see cref="OrderBench.Step(in PointerSample, float, bool)"/>, beyond the
/// parts' own drag events. The part involved is in <see cref="OrderBench.EventPart"/>.
/// </summary>
[Flags]
public enum BenchEvents
{
    None = 0,

    /// <summary>
    /// The brush scrubbed a part past its clean threshold.
    /// </summary>
    Cleaned = 1,

    /// <summary>
    /// The player pressed a part while the hammer was down: half cock comes first.
    /// </summary>
    HammerFirst = 2,

    /// <summary>
    /// The player put a broken part down in the tray, and it was swapped for the spare.
    /// </summary>
    Replaced = 4,

    /// <summary>
    /// The oil can's spout touched a dry part: it is oiled.
    /// </summary>
    Oiled = 8,
}
