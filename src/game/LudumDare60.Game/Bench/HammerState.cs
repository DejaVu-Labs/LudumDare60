namespace LudumDare60.Game.Bench;

/// <summary>
/// Where the hammer rests (see <see cref="ActionTest"/>).
/// </summary>
public enum HammerState
{
    /// <summary>
    /// Down on the frame: nothing turns, nothing comes out.
    /// </summary>
    Down = 0,

    /// <summary>
    /// In the half-cock notch: the cylinder turns freely and the revolver can be taken apart.
    /// </summary>
    HalfCock = 1,

    /// <summary>
    /// Back at full cock, held by the sear until the trigger is pulled.
    /// </summary>
    FullCock = 2,
}
