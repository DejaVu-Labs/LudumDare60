using LudumDare60.Engine.Platform;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Customers;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Analytics;

/// <summary>
/// Turns what happens in the <see cref="Shop"/> into the product analytics events of docs/gamedesign/analytics.md:
/// the game ready and finished, and per order a progression (started when the player takes the revolver, completed
/// when they hand it back, scored with the seconds it took), the moments of the repair with the seconds since the
/// start, and at the end counts of what went wrong. Call <see cref="Open"/> once the shop opens, then
/// <see cref="Observe"/> after every <see cref="Shop.Step"/>.
/// </summary>
/// <remarks>Event ids are built when a customer comes in, so the steps in between allocate nothing. An order the player
/// leaves unfinished (closing the page, or a debug key) sends no completion: analytics shows it as a start without
/// a completion.</remarks>
public sealed class ShopAnalytics
{
    /// <summary>
    /// The first level of every order's progression; the second is the order's id.
    /// </summary>
    public const string OrderProgression = "Order";

    /// <summary>
    /// Sent once when the shop opens, with the seconds from the first frame (the loading screen).
    /// </summary>
    public const string ReadyEvent = "Game:Ready";

    /// <summary>
    /// Sent once when the shop closes after its last order, with the seconds since it opened.
    /// </summary>
    public const string FinishedEvent = "Game:Finished";

    /// <summary>
    /// How long the player may go without progress before it counts as a stall, in seconds: the first rung of the hint
    /// ladder in docs/gamedesign/ftue.md.
    /// </summary>
    public const float StallSeconds = 6;

    /// <summary>
    /// The last part of every per-order design event id (<c>Order:&lt;id&gt;:&lt;name&gt;</c>), indexed by
    /// <see cref="OrderEvent"/>.
    /// </summary>
    public static IReadOnlyList<string> OrderEventNames { get; } =
    [
        "Waited",
        "HalfCock",
        "FirstPartOut",
        "Cleaned",
        "Oiled",
        "Replaced",
        "Assembled",
        "Tested",
        "Blocked",
        "HammerFirst",
        "NotReady",
        "TriggerEarly",
        "Stalls",
        "LongestPause",
    ];

    private readonly IAnalytics _analytics;
    private readonly string[] _ids = new string[OrderEventNames.Count];
    private readonly bool[] _sent = new bool[OrderEventNames.Count];
    private OrderBench? _bench;
    private float _shopSeconds;
    private bool _finished;
    private bool _started;
    private bool _completed;
    private float _waitedSeconds;
    private float _orderSeconds;
    private float _sinceProgress;
    private float _longestPause;
    private int _stalls;
    private int _blocked;
    private int _hammerFirst;
    private int _notReady;
    private int _triggerEarly;

    /// <param name="analytics">Where the events go.</param>
    public ShopAnalytics(IAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);
        _analytics = analytics;
    }

    /// <summary>
    /// The per-order design events, in the order of <see cref="OrderEventNames"/>.
    /// </summary>
    private enum OrderEvent
    {
        Waited,
        HalfCock,
        FirstPartOut,
        Cleaned,
        Oiled,
        Replaced,
        Assembled,
        Tested,
        Blocked,
        HammerFirst,
        NotReady,
        TriggerEarly,
        Stalls,
        LongestPause,
    }

    /// <summary>
    /// The id of the per-order design event <paramref name="name"/> (one of <see cref="OrderEventNames"/>) for
    /// <paramref name="orderId"/>.
    /// </summary>
    public static string OrderEventId(string orderId, string name) => string.Concat(OrderProgression, ":", orderId, ":", name);

    /// <summary>
    /// Sends <see cref="ReadyEvent"/>: the shop is open.
    /// </summary>
    /// <param name="loadingSeconds">Seconds from the first frame until the shop opened.</param>
    public void Open(float loadingSeconds) => _analytics.Design(ReadyEvent, Seconds(loadingSeconds));

    /// <summary>
    /// Sends the events of the step <paramref name="shop"/> just took.
    /// </summary>
    /// <param name="shop">The shop, right after <see cref="Shop.Step"/>.</param>
    /// <param name="deltaTime">The step's length in seconds.</param>
    public void Observe(Shop shop, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(shop);
        _shopSeconds += deltaTime;
        if (shop.Bench != _bench)
        {
            BeginOrder(shop.Bench);
        }

        if (_bench is null)
        {
            if (shop.Phase == ShopPhase.Closed && !_finished)
            {
                _finished = true;
                _analytics.Design(FinishedEvent, Seconds(_shopSeconds));
            }

            return;
        }

        if (!_started)
        {
            if ((shop.Events & ShopEvents.GunThrown) != ShopEvents.None)
            {
                _started = true;
                _analytics.Progression(ProgressionStatus.Start, OrderProgression, _bench.Order.Id, null, null);
                SendSeconds(OrderEvent.Waited, _waitedSeconds);
            }
            else if (shop.Phase == ShopPhase.Offering)
            {
                _waitedSeconds += deltaTime;
            }

            return;
        }

        if (_completed)
        {
            return;
        }

        _orderSeconds += deltaTime;
        var progress = ObserveTest(shop);
        progress |= ObserveBench(shop);
        progress |= ObserveShop(shop.Events);
        if (_completed)
        {
            return;
        }

        if (progress)
        {
            _sinceProgress = 0;
            return;
        }

        var before = _sinceProgress;
        _sinceProgress += deltaTime;
        _longestPause = MathF.Max(_longestPause, _sinceProgress);
        if (before < StallSeconds && _sinceProgress >= StallSeconds)
        {
            _stalls++;
        }
    }

    /// <summary>
    /// Seconds as analytics shows them: to the hundredth.
    /// </summary>
    private static double Seconds(float seconds) => Math.Round(seconds, 2);

    private void BeginOrder(OrderBench? bench)
    {
        _bench = bench;
        _started = false;
        _completed = false;
        _waitedSeconds = 0;
        _orderSeconds = 0;
        _sinceProgress = 0;
        _longestPause = 0;
        _stalls = 0;
        _blocked = 0;
        _hammerFirst = 0;
        _notReady = 0;
        _triggerEarly = 0;
        Array.Clear(_sent);
        if (bench is null)
        {
            return;
        }

        // Once per customer, so the steps in between build no strings.
        for (var i = 0; i < _ids.Length; i++)
        {
            _ids[i] = OrderEventId(bench.Order.Id, OrderEventNames[i]);
        }
    }

    /// <summary>
    /// Counts the hammer and trigger mistakes of the step, sends <see cref="OrderEvent.HalfCock"/> the first time, and
    /// returns whether the hammer or the trigger made progress.
    /// </summary>
    private bool ObserveTest(Shop shop)
    {
        if (!shop.TestStepped)
        {
            return false;
        }

        var events = shop.Test!.Events;
        if ((events & TestEvents.TriggerRefused) != TestEvents.None)
        {
            _triggerEarly++;
        }

        if ((events & TestEvents.HalfCock) != TestEvents.None)
        {
            SendOnce(OrderEvent.HalfCock);
        }

        return (events & (TestEvents.HalfCock | TestEvents.Cocked | TestEvents.DryFire)) != TestEvents.None;
    }

    /// <summary>
    /// Counts the refused presses and drops of the step, sends the bench's moments the first time each happens, and
    /// returns whether a part snapped or docked, or a part was cleaned, oiled or replaced.
    /// </summary>
    private bool ObserveBench(Shop shop)
    {
        if (!shop.BenchStepped)
        {
            return false;
        }

        var bench = shop.Bench!;
        var progress = false;
        var parts = bench.Gun.Events;
        for (var i = 0; i < parts.Length; i++)
        {
            var events = parts[i];
            if ((events & DragEvents.Refused) != DragEvents.None)
            {
                _blocked++;
            }

            if ((events & DragEvents.DockRefused) != DragEvents.None)
            {
                _notReady++;
            }

            if ((events & DragEvents.SnappedEnd) != DragEvents.None)
            {
                SendOnce(OrderEvent.FirstPartOut);
            }

            progress |= (events & (DragEvents.SnappedStart | DragEvents.SnappedEnd | DragEvents.Docked)) != DragEvents.None;
        }

        var benchEvents = bench.Events;
        if ((benchEvents & BenchEvents.HammerFirst) != BenchEvents.None)
        {
            _hammerFirst++;
        }

        if ((benchEvents & BenchEvents.Cleaned) != BenchEvents.None)
        {
            SendOnce(OrderEvent.Cleaned);
        }

        if ((benchEvents & BenchEvents.Oiled) != BenchEvents.None)
        {
            SendOnce(OrderEvent.Oiled);
        }

        if ((benchEvents & BenchEvents.Replaced) != BenchEvents.None)
        {
            SendOnce(OrderEvent.Replaced);
        }

        return progress || (benchEvents & (BenchEvents.Cleaned | BenchEvents.Oiled | BenchEvents.Replaced)) != BenchEvents.None;
    }

    /// <summary>
    /// Sends the shop's moments of the step and, when the revolver was handed back, completes the order. Returns
    /// whether the order moved on.
    /// </summary>
    private bool ObserveShop(ShopEvents events)
    {
        if ((events & ShopEvents.Assembled) != ShopEvents.None)
        {
            SendOnce(OrderEvent.Assembled);
        }

        if ((events & ShopEvents.Repaired) != ShopEvents.None)
        {
            SendOnce(OrderEvent.Tested);
        }

        if ((events & ShopEvents.HandedBack) != ShopEvents.None)
        {
            Complete();
        }

        return (events & (ShopEvents.Assembled | ShopEvents.Repaired | ShopEvents.HandedBack)) != ShopEvents.None;
    }

    /// <summary>
    /// Completes the order's progression, scored with its whole seconds, then sends what went wrong during it. Counts
    /// are sent even when they are zero, so their averages count every order.
    /// </summary>
    private void Complete()
    {
        _completed = true;
        _analytics.Progression(
            ProgressionStatus.Complete, OrderProgression, _bench!.Order.Id, null, (int)MathF.Round(_orderSeconds));
        Send(OrderEvent.Blocked, _blocked);
        Send(OrderEvent.HammerFirst, _hammerFirst);
        Send(OrderEvent.NotReady, _notReady);
        Send(OrderEvent.TriggerEarly, _triggerEarly);
        Send(OrderEvent.Stalls, _stalls);
        SendSeconds(OrderEvent.LongestPause, _longestPause);
    }

    /// <summary>
    /// Sends a moment of the repair with the seconds since the order started, the first time it happens.
    /// </summary>
    private void SendOnce(OrderEvent orderEvent)
    {
        if (!_sent[(int)orderEvent])
        {
            _sent[(int)orderEvent] = true;
            SendSeconds(orderEvent, _orderSeconds);
        }
    }

    private void SendSeconds(OrderEvent orderEvent, float seconds) => Send(orderEvent, Seconds(seconds));

    private void Send(OrderEvent orderEvent, double value) => _analytics.Design(_ids[(int)orderEvent], value);
}
