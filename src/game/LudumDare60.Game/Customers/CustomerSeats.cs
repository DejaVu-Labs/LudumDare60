using System.Numerics;

namespace LudumDare60.Game.Customers;

/// <summary>
/// The customers who sit down to wait, by <see cref="Orders.Customer.Look"/>: their sheet's
/// <see cref="CustomerPose.Waiting"/> frame is drawn sitting, so it goes on a seat in the background instead of the
/// counter. Everyone else waits at the counter.
/// </summary>
public static class CustomerSeats
{
    /// <summary>
    /// The bench under the revolver painting on the left wall of the background. It stands at the back of the shop,
    /// so the customer is drawn smaller than at the counter (<see cref="LudumDare60Game.CustomerScale"/>).
    /// </summary>
    public static readonly CustomerSeat UnderThePainting = new(new Vector2(152, 136), 0.9f);

    /// <summary>
    /// The seat where <paramref name="look"/> waits, if that customer sits down to wait.
    /// </summary>
    public static bool TryGetWaitingSeat(string look, out CustomerSeat seat)
    {
        if (string.Equals(look, "robe", StringComparison.Ordinal))
        {
            seat = UnderThePainting;
            return true;
        }

        seat = default;
        return false;
    }
}
