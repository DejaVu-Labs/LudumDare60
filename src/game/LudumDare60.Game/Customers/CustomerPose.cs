namespace LudumDare60.Game.Customers;

/// <summary>
/// How a customer stands at the counter, in the order of the frames of a customer sheet
/// (<c>assets/customers/&lt;look&gt;.png</c>).
/// </summary>
public enum CustomerPose
{
    /// <summary>
    /// Bringing the revolver in and saying what is wrong, and waiting with a hand out to take it back.
    /// </summary>
    Presenting = 0,

    /// <summary>
    /// Waiting while the player repairs it: on the phone.
    /// </summary>
    Waiting = 1,

    /// <summary>
    /// Pleased, with the revolver back.
    /// </summary>
    Pleased = 2,
}
