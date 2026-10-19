using System.Numerics;
using System.Text.RegularExpressions;
using LudumDare60.Engine.Platform;
using LudumDare60.Game.Analytics;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Customers;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Analytics;

public sealed partial class ShopAnalyticsTests
{
    private const float Dt = 1f / 60;

    private static readonly OrderBook s_shipped = new OrderBookLoader().Load(
        AssetPaths.Orders, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", AssetPaths.Orders)));

    private static readonly PointerSample s_idle = new(new Vector2(600, 40), IsDown: false, IsPressed: false);

    private static Vector2 CustomerMiddle => new(Shop.CounterX, Shop.CustomerBottom - 60);

    private readonly RecordingAnalytics _recorded = new();
    private readonly Shop _shop;
    private readonly ShopAnalytics _analytics;

    public ShopAnalyticsTests()
        : this(s_shipped)
    {
    }

    private ShopAnalyticsTests(OrderBook book)
    {
        _shop = new Shop(book);
        _analytics = new ShopAnalytics(_recorded);
    }

    private static string Id(string name) => ShopAnalytics.OrderEventId("hoodie", name);

    /// <summary>
    /// The first shipped order alone, so the shop closes after it.
    /// </summary>
    private static OrderBook FirstOrderOnly() => new([s_shipped.Orders[0]]);

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    [GeneratedRegex(@"^[A-Za-z0-9\s\-_.()!?]{1,64}(:[A-Za-z0-9\s\-_.()!?]{1,64}){0,4}$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GameAnalyticsDesignId();

    private void Step(in PointerSample input)
    {
        _shop.Step(input, Dt);
        _analytics.Observe(_shop, Dt);
    }

    private void Wait(float seconds)
    {
        for (var t = 0f; t < seconds; t += Dt)
        {
            Step(s_idle);
        }
    }

    private void ComeToTheCounter()
    {
        Wait(Shop.WaitSeconds + Shop.WalkSeconds + Dt);
        Assert.Equal(ShopPhase.Offering, _shop.Phase);
    }

    private void TakeTheRevolver()
    {
        Step(Press(CustomerMiddle));
        Step(Release(CustomerMiddle));
        Wait(Shop.ThrowSeconds + 0.1f);
    }

    private void Drag(Vector2 from, Vector2 to)
    {
        Step(Press(from));
        for (var i = 0; i < 90; i++)
        {
            Step(Hold(to));
        }

        Step(Release(to));
    }

    private void PullHammer() => Drag(_shop.Test!.Spur, _shop.Test.SpurCocked + (_shop.Test.CockDirection * 10));

    /// <summary>
    /// Repairs order 1 the way a player would, from the revolver on the mat until it is back together.
    /// </summary>
    private void Repair()
    {
        PullHammer();
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);
        Drag(BenchLayout.CylinderSeated, BenchLayout.CylinderOut);

        Step(Press(BenchLayout.BrushRest));
        var rust = _shop.Bench!.Dirt(RevolverParts.Cylinder)!;
        // Every other row: the brush is wider than a row, and the scrub stays shorter than a stall.
        for (var y = 1 - (PlaceholderParts.Cylinder.Height / 2); y <= (PlaceholderParts.Cylinder.Height / 2) - 1 && !rust.IsClean; y += 2)
        {
            for (var x = -(PlaceholderParts.Cylinder.Width / 2); x <= PlaceholderParts.Cylinder.Width / 2; x += 2)
            {
                Step(Hold(BenchLayout.CylinderOut + new Vector2(x, y)));
            }
        }

        Step(Hold(BenchLayout.BrushRest));
        Step(Release(BenchLayout.BrushRest));
        Wait(0.5f);
        Drag(BenchLayout.CylinderOut, BenchLayout.CylinderSeated - new Vector2(0, 20));
        Drag(BenchLayout.PinOut, BenchLayout.PinSeated + new Vector2(20, 0));
        Assert.Equal(ShopPhase.Testing, _shop.Phase);
    }

    /// <summary>
    /// Cocks the hammer and pulls the trigger.
    /// </summary>
    private void FireTest()
    {
        PullHammer();
        Step(Press(ActionTest.TriggerCenter));
        Step(Release(ActionTest.TriggerCenter));
        Assert.Equal(ShopPhase.Ready, _shop.Phase);
    }

    private void RepairAndTest()
    {
        Repair();
        FireTest();
    }

    private void HandBack() => Drag(Shop.GunGrip, Shop.GunGrip + Shop.HandBackOffset - new Vector2(0, 30));

    [Fact]
    public void Open_LoadingSeconds_SendsReadyRounded()
    {
        _analytics.Open(3.14159f);

        Assert.Equal([(ShopAnalytics.ReadyEvent, 3.14)], _recorded.Designs);
    }

    [Fact]
    public void Observe_CustomerAtTheCounter_SendsNothingUntilClicked()
    {
        ComeToTheCounter();
        Wait(3);

        Assert.Empty(_recorded.Events);
    }

    [Fact]
    public void Observe_ClickTheCustomer_StartsTheOrderWithTheSecondsTheyWaited()
    {
        ComeToTheCounter();
        Wait(2);

        Step(Press(CustomerMiddle));

        var start = Assert.Single(_recorded.Progressions);
        Assert.Equal(ProgressionStatus.Start, start.Status);
        Assert.Equal("Order:hoodie", start.Id);
        Assert.Null(start.Value);
        Assert.InRange(_recorded.ValueOf(Id("Waited"))!.Value, 2, 2.1);
    }

    [Fact]
    public void Observe_RepairOrder1_SendsEachMomentOnceInOrderWithSecondsSinceTheStart()
    {
        ComeToTheCounter();
        TakeTheRevolver();

        RepairAndTest();

        var moments = _recorded.Designs.Select(d => d.Id).Where(id => !string.Equals(id, Id("Waited"), StringComparison.Ordinal)).ToList();
        Assert.Equal([Id("HalfCock"), Id("FirstPartOut"), Id("Cleaned"), Id("Assembled"), Id("Tested")], moments);
        var times = _recorded.Designs.Skip(1).Select(d => d.Value!.Value).ToList();
        Assert.Equal(times.Order(), times);
        Assert.True(times[0] > Shop.ThrowSeconds, $"Half cock at {times[0]} s.");
        Assert.Single(_recorded.Progressions);
    }

    [Fact]
    public void Observe_HandBack_CompletesTheOrderScoredInWholeSecondsThenSendsItsCounts()
    {
        ComeToTheCounter();
        TakeTheRevolver();
        RepairAndTest();
        var tested = _recorded.ValueOf(Id("Tested"))!.Value;

        HandBack();

        var complete = _recorded.Progressions.Last();
        Assert.Equal(ProgressionStatus.Complete, complete.Status);
        Assert.Equal("Order:hoodie", complete.Id);
        Assert.InRange(complete.Value!.Value, Math.Floor(tested), Math.Ceiling(tested) + 3);
        Assert.Equal(complete.Value % 1, 0);
        var counts = _recorded.Events.SkipWhile(e => e != complete).Skip(1).Select(e => e.Id).ToList();
        Assert.Equal(
            [Id("Blocked"), Id("HammerFirst"), Id("NotReady"), Id("TriggerEarly"), Id("Stalls"), Id("LongestPause")],
            counts);
        Assert.Equal(0, _recorded.ValueOf(Id("HammerFirst")));
        Assert.Equal(0, _recorded.ValueOf(Id("TriggerEarly")));
    }

    [Fact]
    public void Observe_AfterTheHandBack_SendsNothingMoreForTheOrder()
    {
        ComeToTheCounter();
        TakeTheRevolver();
        RepairAndTest();
        HandBack();
        var sent = _recorded.Events.Count;

        Wait(Shop.ThankSeconds + 0.5f);

        Assert.Equal(sent, _recorded.Events.Count);
    }

    [Fact]
    public void Observe_PressAPartWithTheHammerDown_CountsHammerFirst()
    {
        ComeToTheCounter();
        TakeTheRevolver();
        Step(Press(BenchLayout.PinSeated));
        Step(Release(BenchLayout.PinSeated));
        Step(Press(BenchLayout.CylinderSeated));
        Step(Release(BenchLayout.CylinderSeated));

        RepairAndTest();
        HandBack();

        Assert.Equal(2, _recorded.ValueOf(Id("HammerFirst")));
    }

    [Fact]
    public void Observe_TriggerBeforeFullCock_CountsTriggerEarly()
    {
        ComeToTheCounter();
        TakeTheRevolver();
        Repair();
        Step(Press(ActionTest.TriggerCenter));
        Step(Release(ActionTest.TriggerCenter));
        Step(Press(ActionTest.TriggerCenter));
        Step(Release(ActionTest.TriggerCenter));

        FireTest();
        HandBack();

        Assert.Equal(2, _recorded.ValueOf(Id("TriggerEarly")));
    }

    [Fact]
    public void Observe_IdleOnTheBench_CountsOneStallPerPauseAndTheLongestPause()
    {
        ComeToTheCounter();
        TakeTheRevolver();
        Wait(ShopAnalytics.StallSeconds + 1);
        PullHammer();
        Wait((2 * ShopAnalytics.StallSeconds) + 1);

        RepairAndTest();
        HandBack();

        Assert.Equal(2, _recorded.ValueOf(Id("Stalls")));
        // The pause lasts until the pin snaps out: the repair first takes the hammer again, then drags the pin, about
        // 1.5 s each.
        Assert.InRange(_recorded.ValueOf(Id("LongestPause"))!.Value, (2 * ShopAnalytics.StallSeconds) + 1, (2 * ShopAnalytics.StallSeconds) + 5);
    }

    [Fact]
    public void Observe_LastOrderHandedBack_SendsFinishedOnceWhenTheShopCloses()
    {
        var test = new ShopAnalyticsTests(FirstOrderOnly());
        test.ComeToTheCounter();
        test.TakeTheRevolver();
        test.RepairAndTest();
        test.HandBack();

        test.Wait(Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 1);

        Assert.Equal(ShopPhase.Closed, test._shop.Phase);
        var finished = test._recorded.ValueOf(ShopAnalytics.FinishedEvent)!.Value;
        Assert.InRange(finished, 10, 120);
        Assert.Equal(ShopAnalytics.FinishedEvent, test._recorded.Events[^1].Id);
    }

    [Fact]
    public void Observe_CustomerSentAwayByADebugKey_DropsTheOrderWithoutCompletingIt()
    {
        ComeToTheCounter();
        TakeTheRevolver();
        PullHammer();

        _shop.Restart(1);
        Step(s_idle);
        ComeToTheCounter();
        Step(Press(CustomerMiddle));

        Assert.Equal(
            [(ProgressionStatus.Start, "Order:hoodie"), (ProgressionStatus.Start, "Order:robe")],
            _recorded.Progressions.Select(p => (p.Status!.Value, p.Id)));
        Assert.DoesNotContain(_recorded.Designs, d => string.Equals(d.Id, ShopAnalytics.OrderEventId("robe", "HalfCock"), StringComparison.Ordinal));
    }

    [Fact]
    public void OrderEventId_EveryShippedOrderAndName_IsAValidGameAnalyticsDesignId()
    {
        foreach (var order in s_shipped.Orders)
        {
            foreach (var name in ShopAnalytics.OrderEventNames)
            {
                var id = ShopAnalytics.OrderEventId(order.Id, name);
                Assert.Matches(GameAnalyticsDesignId(), id);
            }
        }

        Assert.Matches(GameAnalyticsDesignId(), ShopAnalytics.ReadyEvent);
        Assert.Matches(GameAnalyticsDesignId(), ShopAnalytics.FinishedEvent);
    }
}
