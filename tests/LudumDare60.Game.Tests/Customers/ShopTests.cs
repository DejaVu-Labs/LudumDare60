using System.Numerics;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Customers;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Customers;

public sealed class ShopTests
{
    private const float Dt = 1f / 60;

    private static readonly OrderBook s_shipped = new OrderBookLoader().Load(
        AssetPaths.Orders, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", AssetPaths.Orders)));

    private static readonly PointerSample s_idle = new(new Vector2(600, 40), IsDown: false, IsPressed: false);

    private readonly Shop _shop = new(s_shipped);

    /// <summary>
    /// The first shipped order twice, under two ids, so a second customer can come in.
    /// </summary>
    private static OrderBook TwoPlayableOrders()
    {
        var first = s_shipped.Orders[0];
        var again = new Order("hoodie-again", first.Customer, first.Symptom, first.Parts, first.Find, first.Message, first.TargetSeconds, first.Dirty);
        return new OrderBook([first, again]);
    }

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    /// <summary>
    /// Steps with the pointer idle for <paramref name="seconds"/>, and returns every event seen.
    /// </summary>
    private static ShopEvents Wait(Shop shop, float seconds)
    {
        var seen = ShopEvents.None;
        for (var t = 0f; t < seconds; t += Dt)
        {
            shop.Step(s_idle, Dt);
            seen |= shop.Events;
        }

        return seen;
    }

    private static ShopEvents Drag(Shop shop, Vector2 from, Vector2 to)
    {
        var seen = ShopEvents.None;
        shop.Step(Press(from), Dt);
        seen |= shop.Events;
        for (var i = 0; i < 90; i++)
        {
            shop.Step(Hold(to), Dt);
            seen |= shop.Events;
        }

        shop.Step(Release(to), Dt);
        return seen | shop.Events;
    }

    /// <summary>
    /// Takes the hammer's spur where it is and pulls it back past full cock.
    /// </summary>
    private static ShopEvents PullHammer(Shop shop)
        => Drag(shop, shop.Test!.Spur, shop.Test.SpurCocked + (shop.Test.CockDirection * 10));

    /// <summary>
    /// Waits for the first customer, repairs order 1 the way a player would (half cock first), and returns every
    /// event seen.
    /// </summary>
    private static ShopEvents Repair(Shop shop)
    {
        var seen = Wait(shop, Shop.WaitSeconds + Shop.WalkSeconds + Shop.DropSeconds + 0.1f);
        seen |= PullHammer(shop);
        seen |= Drag(shop, BenchLayout.PinSeated, BenchLayout.PinOut);
        seen |= Drag(shop, BenchLayout.CylinderSeated, BenchLayout.CylinderOut);

        shop.Step(Press(BenchLayout.BrushRest), Dt);
        var rust = shop.Bench!.Dirt(RevolverParts.Cylinder)!;
        for (var y = -10; y <= 10 && !rust.IsClean; y++)
        {
            for (var x = -13; x <= 13; x += 2)
            {
                shop.Step(Hold(BenchLayout.CylinderOut + new Vector2(x, y)), Dt);
                seen |= shop.Events;
            }
        }

        shop.Step(Hold(BenchLayout.BrushRest), Dt);
        shop.Step(Release(BenchLayout.BrushRest), Dt);
        seen |= Wait(shop, 0.5f);
        seen |= Drag(shop, BenchLayout.CylinderOut, BenchLayout.CylinderSeated - new Vector2(0, 20));
        seen |= Drag(shop, BenchLayout.PinOut, BenchLayout.PinSeated + new Vector2(20, 0));
        return seen;
    }

    /// <summary>
    /// Cocks the hammer and pulls the trigger, and returns every event seen.
    /// </summary>
    private static ShopEvents FireTest(Shop shop)
    {
        var seen = PullHammer(shop);
        shop.Step(Press(ActionTest.TriggerCenter), Dt);
        seen |= shop.Events;
        shop.Step(Release(ActionTest.TriggerCenter), Dt);
        return seen | shop.Events;
    }

    private static ShopEvents HandBack(Shop shop) => Drag(shop, Shop.GunGrip, Shop.GunGrip + Shop.HandBackOffset - new Vector2(0, 30));

    [Fact]
    public void Constructor_OpensWithAnEmptyCounter()
    {
        Assert.Equal(ShopPhase.Waiting, _shop.Phase);
        Assert.False(_shop.HasCustomer);
        Assert.False(_shop.HasGun);
        Assert.Null(_shop.Bench);
    }

    [Fact]
    public void Step_AfterTheWait_FirstCustomerEntersWithTheFirstOrder()
    {
        var events = Wait(_shop, Shop.WaitSeconds + Dt);

        Assert.Equal(ShopEvents.CustomerEntered, events);
        Assert.Equal(ShopPhase.Arriving, _shop.Phase);
        Assert.Equal("hoodie", _shop.Bench!.Order.Id);
        Assert.True(_shop.HasCustomer);
        Assert.False(_shop.HasGun);
        Assert.Equal(Shop.DoorX, _shop.CustomerX, 0.5f);
    }

    [Fact]
    public void Step_Arriving_WalksFromTheDoorToTheCounter()
    {
        Wait(_shop, Shop.WaitSeconds + Dt);
        Wait(_shop, Shop.WalkSeconds / 2);
        var halfway = _shop.CustomerX;

        Wait(_shop, Shop.WalkSeconds / 2);

        Assert.InRange(halfway, Shop.CounterX + 10, Shop.DoorX - 10);
        Assert.Equal(ShopPhase.Working, _shop.Phase);
        Assert.Equal(Shop.CounterX, _shop.CustomerX);
    }

    [Fact]
    public void Step_AtTheCounter_TheRevolverDropsAndLandsOnce()
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + Dt);
        var falling = _shop.GetGunOffset(0);

        var events = Wait(_shop, 1);

        Assert.True(falling.Y < -Shop.DropHeight / 2);
        Assert.Equal(ShopEvents.GunLanded, events);
        Assert.Equal(Vector2.Zero, _shop.GetGunOffset(0));
        Assert.True(_shop.HasGun);
    }

    [Fact]
    public void Step_HammerDown_PartsWaitForHalfCock()
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + 1);

        Drag(_shop, BenchLayout.PinSeated, BenchLayout.PinOut);
        Assert.Equal(PartStop.Start, _shop.Bench!.Part(RevolverParts.BasePin).Stop);

        PullHammer(_shop);
        Drag(_shop, BenchLayout.PinSeated, BenchLayout.PinOut);

        Assert.Equal(HammerState.HalfCock, _shop.Test!.State);
        Assert.Equal(PartStop.End, _shop.Bench.Part(RevolverParts.BasePin).Stop);
    }

    [Fact]
    public void Step_PressAPartWithTheHammerDown_TheBenchSaysHammerFirst()
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + 1);

        _shop.Step(Press(BenchLayout.PinSeated), Dt);

        Assert.Equal(BenchEvents.HammerFirst, _shop.Bench!.Events);
        Assert.Equal(_shop.Bench.IndexOf(RevolverParts.BasePin), _shop.Bench.EventPart);
    }

    [Fact]
    public void Step_WhileTheRevolverFalls_PartsCannotBeTouched()
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + Dt);

        _shop.Step(Press(BenchLayout.PinSeated), Dt);

        Assert.False(_shop.Bench!.Part(RevolverParts.BasePin).IsHeld);
    }

    [Fact]
    public void Step_BackTogether_TestBeginsAndPartsStayPut()
    {
        var events = Repair(_shop);

        Assert.True((events & ShopEvents.Assembled) != ShopEvents.None);
        Assert.Equal(ShopPhase.Testing, _shop.Phase);
        Assert.False(_shop.IsHandOut);
        Assert.True(_shop.HasGun);

        Drag(_shop, BenchLayout.PinSeated, BenchLayout.PinOut);
        Assert.Equal(PartStop.Start, _shop.Bench!.Part(RevolverParts.BasePin).Stop);
    }

    [Fact]
    public void HasCustomer_FromComingInToLeaving_InEveryPhase()
    {
        var phases = new HashSet<ShopPhase>();
        var shop = new Shop(s_shipped);
        void note()
        {
            if (shop.HasCustomer)
            {
                phases.Add(shop.Phase);
            }
            else
            {
                Assert.True(shop.Phase is ShopPhase.Waiting or ShopPhase.Closed, $"No customer while {shop.Phase}.");
            }
        }

        note();
        Repair(shop);
        note();
        FireTest(shop);
        note();
        HandBack(shop);
        for (var t = 0f; t < Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 0.5f; t += Dt)
        {
            shop.Step(s_idle, Dt);
            note();
        }

        Assert.Equal(
            [ShopPhase.Testing, ShopPhase.Ready, ShopPhase.Thanking, ShopPhase.Leaving],
            phases.Order());
    }

    [Fact]
    public void Step_Testing_OnlyTheTriggerAfterTheHammerFinishesIt()
    {
        Repair(_shop);
        _shop.Step(Press(ActionTest.TriggerCenter), Dt);
        _shop.Step(Release(ActionTest.TriggerCenter), Dt);
        Assert.Equal(ShopPhase.Testing, _shop.Phase);

        var events = FireTest(_shop);

        Assert.Equal(ShopEvents.Repaired, events);
        Assert.Equal(ShopPhase.Ready, _shop.Phase);
        Assert.True(_shop.IsHandOut);
    }

    [Fact]
    public void Step_Testing_RunsTheTestOnlyThen()
    {
        Wait(_shop, Shop.WaitSeconds + (Shop.DropSeconds / 2));
        Assert.False(_shop.TestStepped);

        Repair(_shop);
        _shop.Step(s_idle, Dt);

        Assert.True(_shop.TestStepped);
        Assert.False(_shop.BenchStepped);
        Assert.Equal(HammerState.HalfCock, _shop.Test!.State);
    }

    [Fact]
    public void Step_DragTheRevolverUp_HandsItBackAndTheCustomerThanks()
    {
        Repair(_shop);
        FireTest(_shop);

        var events = HandBack(_shop);

        Assert.Equal(ShopEvents.HandedBack, events);
        Assert.Equal(ShopPhase.Thanking, _shop.Phase);
        Assert.Equal(Expression.Thankful, _shop.Expression);
        Assert.False(_shop.HasGun);
    }

    [Fact]
    public void Step_RevolverLetGoHalfway_StaysWithThePlayer()
    {
        Repair(_shop);
        FireTest(_shop);

        _shop.Step(Press(Shop.GunGrip), Dt);
        for (var i = 0; i < 10; i++)
        {
            _shop.Step(Hold(Shop.GunGrip + new Vector2(0, -30)), Dt);
        }

        _shop.Step(Release(Shop.GunGrip + new Vector2(0, -30)), Dt);

        Assert.Equal(ShopPhase.Ready, _shop.Phase);
        Assert.InRange(_shop.GetGunOffset(1).Y, -50, -5);
    }

    [Fact]
    public void Step_AfterThanking_CustomerLeavesAndTheLastPlayableOrderClosesTheShop()
    {
        Repair(_shop);
        FireTest(_shop);
        HandBack(_shop);

        var events = Wait(_shop, Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 0.2f);

        Assert.Equal(ShopEvents.CustomerLeft, events);
        Assert.Equal(ShopPhase.Closed, _shop.Phase);
        Assert.Equal(1, _shop.OrderIndex);
        Assert.Null(_shop.Bench);
        Assert.False(_shop.HasCustomer);
    }

    [Fact]
    public void Step_NextOrderPlayable_NextCustomerComesInWithIt()
    {
        var shop = new Shop(TwoPlayableOrders());
        Repair(shop);
        FireTest(shop);
        HandBack(shop);

        var events = Wait(shop, Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 0.2f);

        Assert.Equal(ShopEvents.CustomerLeft | ShopEvents.CustomerEntered, events);
        Assert.Equal(ShopPhase.Arriving, shop.Phase);
        Assert.Equal("hoodie-again", shop.Bench!.Order.Id);
        Assert.False(shop.Test!.IsFired);
        Assert.Equal(PartStop.Start, shop.Bench.Part(RevolverParts.Cylinder).Stop);
        Assert.False(shop.Bench.Dirt(RevolverParts.Cylinder)!.IsClean);
    }

    [Fact]
    public void Expression_WhileWorking_IsNeutral()
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + 1);

        Assert.Equal(Expression.Neutral, _shop.Expression);
        Assert.False(_shop.IsHandOut);
    }
}
