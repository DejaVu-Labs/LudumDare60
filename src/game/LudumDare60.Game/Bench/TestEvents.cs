namespace LudumDare60.Game.Bench;

/// <summary>
/// What happened during one <see cref="ActionTest.Step"/>, for sounds.
/// </summary>
[Flags]
public enum TestEvents
{
    None = 0,

    /// <summary>
    /// The hammer passed half cock on its way back: the first click of the ratchet.
    /// </summary>
    HalfCock = 1,

    /// <summary>
    /// The hammer reached full cock and the cylinder turned one chamber.
    /// </summary>
    Cocked = 2,

    /// <summary>
    /// The player let go of the hammer before full cock, and its spring took it back down.
    /// </summary>
    HammerEased = 4,

    /// <summary>
    /// The trigger released the cocked hammer: the dry-fire click.
    /// </summary>
    DryFire = 8,

    /// <summary>
    /// The trigger was pulled with the hammer down, which does nothing on a single-action revolver.
    /// </summary>
    TriggerRefused = 16,
}
