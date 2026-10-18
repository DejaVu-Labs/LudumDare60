namespace LudumDare60.Game;

/// <summary>
/// The game's asset files, below <c>wwwroot/assets/</c> of the browser project.
/// </summary>
public static class AssetPaths
{
    /// <summary>
    /// The workshop with the empty bench and the cutting mat, the size of the virtual screen.
    /// </summary>
    public const string Bench = "backgrounds/bench.png";

    /// <summary>
    /// Every customer's order, in the order they come in (see <see cref="Orders.OrderBook"/>).
    /// </summary>
    public const string Orders = "data/orders.json";
}
