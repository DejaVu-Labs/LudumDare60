namespace LudumDare60.Game.Customers;

/// <summary>
/// What happened in the shop during one <see cref="Shop.Step"/>, for sounds and effects.
/// </summary>
[Flags]
public enum ShopEvents
{
    None = 0,

    /// <summary>
    /// The door opened: a customer comes in.
    /// </summary>
    CustomerEntered = 1,

    /// <summary>
    /// The customer's revolver landed on the mat.
    /// </summary>
    GunLanded = 2,

    /// <summary>
    /// The revolver is back together; the test begins.
    /// </summary>
    Assembled = 4,

    /// <summary>
    /// The test is done; the customer holds out a hand.
    /// </summary>
    Repaired = 8,

    /// <summary>
    /// The player handed the revolver back.
    /// </summary>
    HandedBack = 16,

    /// <summary>
    /// The door closed behind the customer.
    /// </summary>
    CustomerLeft = 32,
}
