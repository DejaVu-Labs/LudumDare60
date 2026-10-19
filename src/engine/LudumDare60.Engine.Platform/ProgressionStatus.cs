namespace LudumDare60.Engine.Platform;

/// <summary>
/// The state of a progression attempt (see <see cref="IAnalytics.Progression"/>). The values are GameAnalytics'
/// <c>EGAProgressionStatus</c>.
/// </summary>
public enum ProgressionStatus
{
    /// <summary>
    /// Not a status: <c>analytics.js</c> drops an event with it.
    /// </summary>
    None = 0,

    /// <summary>
    /// The attempt begins.
    /// </summary>
    Start = 1,

    /// <summary>
    /// The attempt succeeded.
    /// </summary>
    Complete = 2,

    /// <summary>
    /// The attempt failed or was given up.
    /// </summary>
    Fail = 3,
}
