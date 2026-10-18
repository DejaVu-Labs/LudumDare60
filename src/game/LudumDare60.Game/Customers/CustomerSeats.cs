using System.Numerics;

namespace LudumDare60.Game.Customers;

/// <summary>
/// The customers who wait somewhere else than the counter, by <see cref="Orders.Customer.Look"/>: their sheet's
/// <see cref="CustomerPose.Waiting"/> frame shows them sitting down or out in the street, so it is drawn there.
/// Everyone else waits at the counter.
/// </summary>
public static class CustomerSeats
{
    /// <summary>
    /// The bench under the revolver painting on the left wall of the background. It stands at the back of the shop,
    /// so the customer is drawn smaller than at the counter (<see cref="LudumDare60Game.CustomerScale"/>).
    /// </summary>
    public static readonly CustomerSeat UnderThePainting = new(new Vector2(156, 141), 0.9f);

    /// <summary>
    /// On the pavement right behind the window, walking the dog: close to the glass, so drawn larger than at the
    /// counter, with the feet and most of the dog below the sill, behind the wall.
    /// </summary>
    public static readonly CustomerSeat OutsideTheWindow = new(new Vector2(370, 150), 1.1f, IsOutside: true);

    /// <summary>
    /// The place where <paramref name="look"/> waits, if that customer does not wait at the counter.
    /// </summary>
    public static bool TryGetWaitingSeat(string look, out CustomerSeat seat)
    {
        if (string.Equals(look, "robe", StringComparison.Ordinal))
        {
            seat = UnderThePainting;
            return true;
        }

        if (string.Equals(look, "dog-walker", StringComparison.Ordinal))
        {
            seat = OutsideTheWindow;
            return true;
        }

        seat = default;
        return false;
    }
}
