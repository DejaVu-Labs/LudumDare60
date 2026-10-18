namespace LudumDare60.Game.Parts;

/// <summary>
/// Where a <see cref="PartDrag"/> is snapped in.
/// </summary>
public enum PartStop
{
    /// <summary>
    /// Loose, somewhere between the ends.
    /// </summary>
    None = 0,

    /// <summary>
    /// At <see cref="PartPath.Start"/>: seated in the gun.
    /// </summary>
    Start = 1,

    /// <summary>
    /// At <see cref="PartPath.End"/>: pulled all the way out.
    /// </summary>
    End = 2,
}
