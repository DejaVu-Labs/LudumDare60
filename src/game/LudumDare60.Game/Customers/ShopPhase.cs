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
    /// The revolver lies on the mat and the player repairs it.
    /// </summary>
    Working = 2,

    /// <summary>
    /// The repair is done: the customer holds out a hand, and the player hands the revolver back.
    /// </summary>
    Ready = 3,

    /// <summary>
    /// The customer has the revolver back and thanks the player.
    /// </summary>
    Thanking = 4,

    /// <summary>
    /// The customer walks out.
    /// </summary>
    Leaving = 5,

    /// <summary>
    /// No more orders the bench can play.
    /// </summary>
    Closed = 6,
}
