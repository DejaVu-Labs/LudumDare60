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

    /// <summary>
    /// The middle of a customer standing at the counter.
    /// </summary>
    private static Vector2 CustomerMiddle => new(Shop.CounterX, Shop.CustomerBottom - 60);

    /// <summary>
    /// Waits for the first customer to reach the counter, clicks them, and returns every event seen.
    /// </summary>
    private static ShopEvents TakeTheRevolver(Shop shop)
    {
        var seen = Wait(shop, Shop.WaitSeconds + Shop.WalkSeconds + Dt);
        Assert.Equal(ShopPhase.Offering, shop.Phase);
        shop.Step(Press(CustomerMiddle), Dt);
        seen |= shop.Events;
        shop.Step(Release(CustomerMiddle), Dt);
        return seen | shop.Events;
    }

    /// <summary>
    /// <see cref="TakeTheRevolver"/>, then waits for it to land on the mat.
    /// </summary>
    private static ShopEvents Arrive(Shop shop) => TakeTheRevolver(shop) | Wait(shop, Shop.ThrowSeconds + 0.1f);

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
        => Arrive(shop) | RepairWithoutWaiting(shop);

    /// <summary>
    /// Repairs the order on the bench, the customer already at the counter.
    /// </summary>
    private static ShopEvents RepairWithoutWaiting(Shop shop)
    {
        var seen = PullHammer(shop);
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
        Assert.Equal(ShopPhase.Offering, _shop.Phase);
        Assert.Equal(Shop.CounterX, _shop.CustomerX);
    }

    [Fact]
    public void Step_AtTheCounter_KeepsTheRevolverUntilClicked()
    {
        var events = Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + 5);

        Assert.Equal(ShopEvents.CustomerEntered, events);
        Assert.Equal(ShopPhase.Offering, _shop.Phase);
        Assert.Equal(CustomerPose.Presenting, _shop.Pose);
        Assert.False(_shop.HasGun);
        Assert.False(_shop.TestStepped);
    }

    [Fact]
    public void Step_PointerOverTheWaitingCustomer_HoversThem()
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + Dt);

        _shop.Step(Release(CustomerMiddle), Dt);
        var over = _shop.IsCustomerHovered;
        _shop.Step(Release(BenchLayout.CylinderSeated), Dt);

        Assert.True(over);
        Assert.False(_shop.IsCustomerHovered);
        Assert.Equal(ShopPhase.Offering, _shop.Phase);
    }

    [Theory]
    [InlineData(Shop.CounterX - Shop.CustomerHalfWidth - 2, Shop.CustomerBottom - 60)]
    [InlineData(Shop.CounterX + Shop.CustomerHalfWidth + 2, Shop.CustomerBottom - 60)]
    [InlineData(Shop.CounterX, Shop.CustomerBottom + 10)]
    [InlineData(Shop.CounterX, Shop.CustomerBottom - Shop.CustomerHeight - 2)]
    public void Step_PressBesideTheWaitingCustomer_KeepsTheRevolverWithThem(float x, float y)
    {
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + Dt);

        _shop.Step(Press(new Vector2(x, y)), Dt);

        Assert.Equal(ShopEvents.None, _shop.Events);
        Assert.Equal(ShopPhase.Offering, _shop.Phase);
    }

    [Fact]
    public void Step_PressTheCustomerOnTheirWayIn_DoesNothing()
    {
        Wait(_shop, Shop.WaitSeconds + Dt);

        _shop.Step(Press(new Vector2(_shop.CustomerX, Shop.CustomerBottom - 60)), Dt);

        Assert.Equal(ShopPhase.Arriving, _shop.Phase);
        Assert.False(_shop.IsCustomerHovered);
    }

    [Fact]
    public void Step_ClickTheWaitingCustomer_TheRevolverFliesFromTheirHandsOntoTheMatAndLandsOnce()
    {
        var thrown = TakeTheRevolver(_shop);
        var start = _shop.GetGunOffset(0);
        Wait(_shop, (Shop.ThrowSeconds / 2) - (2 * Dt));
        var halfway = _shop.GetGunOffset(0);

        var events = Wait(_shop, 1);

        Assert.Equal(ShopEvents.CustomerEntered | ShopEvents.GunThrown, thrown);
        Assert.Equal(Shop.ThrowFrom.X, start.X, 3f);
        Assert.Equal(Shop.ThrowFrom.Y, start.Y, 10f);
        Assert.True(halfway.Y < (Shop.ThrowFrom.Y / 2) - (Shop.ThrowHeight / 2), $"Halfway at {halfway}.");
        Assert.Equal(ShopEvents.GunLanded, events);
        Assert.Equal(Vector2.Zero, _shop.GetGunOffset(0));
        Assert.True(_shop.HasGun);
    }

    [Fact]
    public void Step_HammerDown_PartsWaitForHalfCock()
    {
        Arrive(_shop);

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
        Arrive(_shop);

        _shop.Step(Press(BenchLayout.PinSeated), Dt);

        Assert.Equal(BenchEvents.HammerFirst, _shop.Bench!.Events);
        Assert.Equal(_shop.Bench.IndexOf(RevolverParts.BasePin), _shop.Bench.EventPart);
    }

    [Fact]
    public void Step_WhileTheRevolverFlies_PartsCannotBeTouched()
    {
        TakeTheRevolver(_shop);

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
        var shop = new Shop(new OrderBook([s_shipped.Orders[0]]));
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
        Wait(_shop, Shop.WaitSeconds + Shop.WalkSeconds + 1);
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
    public void Step_AfterThanking_TheSecondShippedCustomerComesInWithTheGripScrewOrder()
    {
        Repair(_shop);
        FireTest(_shop);
        HandBack(_shop);

        var events = Wait(_shop, Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 0.2f);

        Assert.Equal(ShopEvents.CustomerLeft | ShopEvents.CustomerEntered, events);
        Assert.Equal(ShopPhase.Arriving, _shop.Phase);
        Assert.Equal(1, _shop.OrderIndex);
        Assert.Equal("robe", _shop.Bench!.Order.Id);
        Assert.Equal(0, _shop.Bench.IndexOf(RevolverParts.GripScrew));
    }

    [Fact]
    public void Step_AfterTheLastOrder_CustomerLeavesAndTheShopCloses()
    {
        var shop = new Shop(new OrderBook([s_shipped.Orders[0]]));
        Repair(shop);
        FireTest(shop);
        HandBack(shop);

        var events = Wait(shop, Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 0.2f);

        Assert.Equal(ShopEvents.CustomerLeft, events);
        Assert.Equal(ShopPhase.Closed, shop.Phase);
        Assert.Equal(1, shop.OrderIndex);
        Assert.Null(shop.Bench);
        Assert.False(shop.HasCustomer);
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
    public void Pose_ThroughAVisit_PresentsWaitsPresentsAndIsPleased()
    {
        Wait(_shop, Shop.WaitSeconds + Dt);
        var arriving = _shop.Pose;
        Wait(_shop, Shop.WalkSeconds + 0.5f);
        var waitingToHandOver = _shop.Pose;
        _shop.Step(Press(CustomerMiddle), Dt);
        var justHandedOver = _shop.Pose;
        Wait(_shop, Shop.PresentSeconds);
        var working = _shop.Pose;
        RepairWithoutWaiting(_shop);
        var testing = _shop.Pose;
        FireTest(_shop);
        var ready = _shop.Pose;
        HandBack(_shop);

        Assert.Equal(CustomerPose.Presenting, arriving);
        Assert.Equal(CustomerPose.Presenting, waitingToHandOver);
        Assert.Equal(CustomerPose.Presenting, justHandedOver);
        Assert.Equal(CustomerPose.Waiting, working);
        Assert.Equal(CustomerPose.Waiting, testing);
        Assert.Equal(CustomerPose.Presenting, ready);
        Assert.Equal(CustomerPose.Pleased, _shop.Pose);
    }

    [Fact]
    public void Expression_WhileWorking_IsNeutral()
    {
        Arrive(_shop);

        Assert.Equal(Expression.Neutral, _shop.Expression);
        Assert.False(_shop.IsHandOut);
    }

    [Fact]
    public void Restart_WhileWorking_ThatOrdersCustomerComesInOnTheNextStep()
    {
        Arrive(_shop);
        var oldBench = _shop.Bench;

        _shop.Restart(2);
        Assert.Null(_shop.Bench);
        _shop.Step(s_idle, Dt);

        Assert.Equal(ShopEvents.CustomerEntered, _shop.Events);
        Assert.Equal(ShopPhase.Arriving, _shop.Phase);
        Assert.Equal(2, _shop.OrderIndex);
        Assert.Equal("dog-walker", _shop.Bench!.Order.Id);
        Assert.NotSame(oldBench, _shop.Bench);
        Assert.Equal(HammerState.Down, _shop.Test!.State);
    }

    [Fact]
    public void Restart_SameOrder_StartsItsVisitOverWithAFreshBench()
    {
        Arrive(_shop);
        var oldBench = _shop.Bench;

        _shop.Restart(_shop.OrderIndex);
        _shop.Step(s_idle, Dt);

        Assert.Equal(0, _shop.OrderIndex);
        Assert.Equal(ShopPhase.Arriving, _shop.Phase);
        Assert.Equal(s_shipped.Orders[0].Id, _shop.Bench!.Order.Id);
        Assert.NotSame(oldBench, _shop.Bench);
    }

    [Fact]
    public void Restart_AfterClosing_OpensTheShopAgain()
    {
        var shop = new Shop(new OrderBook([s_shipped.Orders[0]]));
        Repair(shop);
        FireTest(shop);
        HandBack(shop);
        Wait(shop, Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds + 0.2f);
        Assert.Equal(ShopPhase.Closed, shop.Phase);

        shop.Restart(0);
        shop.Step(s_idle, Dt);

        Assert.Equal(ShopPhase.Arriving, shop.Phase);
        Assert.True(shop.HasCustomer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Restart_OutsideTheBook_Throws(int orderIndex)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _shop.Restart(orderIndex));
}
