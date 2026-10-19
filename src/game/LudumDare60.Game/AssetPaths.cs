namespace LudumDare60.Game;

/// <summary>
/// The game's asset files, below <c>wwwroot/assets/</c> of the browser project.
/// </summary>
public static class AssetPaths
{
    /// <summary>
    /// The workshop with the empty bench and the cutting mat, the size of the virtual screen. The window panes and the
    /// door glass are transparent: <see cref="Street"/> shows through them.
    /// </summary>
    public const string Bench = "backgrounds/bench.png";

    /// <summary>
    /// The rainy street outside, drawn under <see cref="Bench"/> (see <see cref="LudumDare60Game.StreetOffset"/>).
    /// </summary>
    public const string Street = "backgrounds/street.png";

    /// <summary>
    /// The wire brush over a part, a seamless loop the game plays while the brush is in the hand, louder and higher the
    /// faster it rubs. Cut from a CC0 recording (Freesound 379919, steel brush on an aluminium tube).
    /// </summary>
    public const string ScrubSound = "sounds/scrub.wav";

    /// <summary>
    /// Every customer's order, in the order they come in (see <see cref="Orders.OrderBook"/>).
    /// </summary>
    public const string Orders = "data/orders.json";

    /// <summary>
    /// The customers drawn so far, by <see cref="Orders.Customer.Look"/>: a sheet of their poses each (see
    /// <see cref="Customers.CustomerSheet"/>). The other looks are drawn as placeholders.
    /// </summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> CustomerSheets =
    [
        new("hoodie", "customers/hoodie.png"),
        new("robe", "customers/robe.png"),
        new("dog-walker", "customers/dog-walker.png"),
        new("clown", "customers/clown.png"),
        new("parcel", "customers/parcel.png"),
    ];
}
