namespace LudumDare60.Game.Bench;

/// <summary>
/// What happened on the bench during one <see cref="OrderBench.Step"/>, beyond the parts' own drag events. The part
/// involved is in <see cref="OrderBench.EventPart"/>.
/// </summary>
[Flags]
public enum BenchEvents
{
    None = 0,

    /// <summary>
    /// The player pressed a dirty part without the brush: bare hands do nothing to dirt.
    /// </summary>
    BareHands = 1,

    /// <summary>
    /// The brush scrubbed a part past its clean threshold.
    /// </summary>
    Cleaned = 2,
}
