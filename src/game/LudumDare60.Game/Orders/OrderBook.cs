namespace LudumDare60.Game.Orders;

/// <summary>
/// Every order of the game, in the order customers come in: <c>assets/data/orders.json</c>, loaded with
/// <see cref="OrderBookLoader"/>.
/// </summary>
/// <param name="Orders">The orders.</param>
public sealed record OrderBook(IReadOnlyList<Order> Orders)
{
    /// <summary>
    /// Checks what the JSON schema cannot: ids, part references against <see cref="RevolverParts"/>, and that every
    /// part an order needs can actually come off.
    /// </summary>
    /// <returns>One sentence per problem; empty when the book is valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (Orders.Count == 0)
        {
            problems.Add("There are no orders.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var order in Orders)
        {
            if (!ids.Add(order.Id))
            {
                problems.Add($"Order id '{order.Id}' is used twice.");
            }

            order.Validate(problems);
        }

        return problems;
    }
}
