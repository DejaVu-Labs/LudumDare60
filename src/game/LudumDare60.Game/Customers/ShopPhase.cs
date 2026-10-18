namespace LudumDare60.Game.Customers;

/// <summary>
/// Where the shop is in one customer's visit (see <see cref="Shop"/>).
/// </summary>
public enum ShopPhase
{
    /// <summary>
    /// Nobody at the counter; the next customer comes in after a moment.
    /// </summary>
    Waiting = 0,

    /// <summary>
    /// The customer walks from the door to the counter.
    /// </summary>
    Arriving = 1,

    /// <summary>
    /// The customer stands at the counter with the revolver, glowing, until the player clicks them.
    /// </summary>
    Offering = 2,

    /// <summary>
    /// The revolver flies onto the mat, and once it lies there the player repairs it.
    /// </summary>
    Working = 3,

    /// <summary>
    /// The revolver is back together: the player cocks the hammer and pulls the trigger to test it.
    /// </summary>
    Testing = 4,

    /// <summary>
    /// The repair is done: the customer holds out a hand, and the player hands the revolver back.
    /// </summary>
    Ready = 5,

    /// <summary>
    /// The customer has the revolver back and thanks the player.
    /// </summary>
    Thanking = 6,

    /// <summary>
    /// The customer walks out.
    /// </summary>
    Leaving = 7,

    /// <summary>
    /// No more orders the bench can play.
    /// </summary>
    Closed = 8,
}
