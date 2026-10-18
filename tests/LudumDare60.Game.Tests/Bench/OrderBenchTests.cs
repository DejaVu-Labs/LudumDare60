using System.Numerics;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Bench;

public sealed class OrderBenchTests
{
    private const float Dt = 1f / 60;

    private static readonly OrderBook s_shipped = new OrderBookLoader().Load(
        AssetPaths.Orders, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", AssetPaths.Orders)));

    private readonly OrderBench _bench = new(s_shipped.Orders[0]);

    private static Order MakeOrder(string[] parts, DirtSpec[]? dirty = null, string[]? broken = null)
        => new(
            "test",
            new Customer("hoodie", "Test customer"),
            Symptom.JammedCylinder,
            parts,
            new Find("note", parts[^1]),
            "Thanks.",
            targetSeconds: 60,
            dirty,
            broken);

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    private int Cylinder => _bench.IndexOf(RevolverParts.Cylinder);

    private DirtLayer Rust => _bench.Dirt(RevolverParts.Cylinder)!;

    /// <summary>
    /// Presses at <paramref name="from"/>, holds at <paramref name="to"/> for 90 steps and lets go.
    /// </summary>
    private BenchEvents Drag(Vector2 from, Vector2 to)
    {
        var seen = BenchEvents.None;
        _bench.Step(Press(from), Dt);
        seen |= _bench.Events;
        for (var i = 0; i < 90; i++)
        {
            _bench.Step(Hold(to), Dt);
            seen |= _bench.Events;
        }

        _bench.Step(Release(to), Dt);
        return seen | _bench.Events;
    }

    private void TakeApart()
    {
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);
        Drag(BenchLayout.CylinderSeated, BenchLayout.CylinderOut);
    }

    /// <summary>
    /// With the brush in the hand, rubs it across the cylinder lying out, at <paramref name="y"/> pixels from its middle.
    /// </summary>
    private BenchEvents Rub(float y)
    {
        var seen = BenchEvents.None;
        for (var x = -13; x <= 13; x += 2)
        {
            _bench.Step(Hold(BenchLayout.CylinderOut + new Vector2(x, y)), Dt);
            seen |= _bench.Events;
        }

        return seen;
    }

    [Fact]
    public void Constructor_FirstShippedOrder_LaysOutPinAndRustyCylinder()
    {
        Assert.Equal(2, _bench.Count);
        Assert.Equal(RevolverParts.BasePin, _bench.IdAt(0));
        Assert.Equal(BenchLayout.PinSeated, _bench.Part(RevolverParts.BasePin).Position);
        Assert.Equal(BenchLayout.CylinderSeated, _bench.Part(RevolverParts.Cylinder).Position);
        Assert.True(_bench.Gun.Holds(_bench.IndexOf(RevolverParts.BasePin), Cylinder));
        Assert.Null(_bench.Dirt(RevolverParts.BasePin));
        Assert.Equal(0.9f, Rust.CleanThreshold);
        Assert.False(_bench.Brush.IsHeld);
        Assert.Equal(BenchLayout.BrushRest, _bench.Brush.Position);
    }

    [Theory]
    [InlineData(1, "grip-screw")]
    [InlineData(2, "grip-screw")]
    [InlineData(3, "hammer-screw")]
    [InlineData(4, "grip-screw")]
    public void CanSetUp_LaterShippedOrders_NotYetNamingTheMissingPart(int index, string part)
    {
        Assert.False(OrderBench.CanSetUp(s_shipped.Orders[index], out var reason));
        Assert.Contains(part, reason, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => new OrderBench(s_shipped.Orders[index]));
    }

    [Fact]
    public void CanSetUp_BrokenPart_NotYet()
    {
        var order = MakeOrder([RevolverParts.BasePin, RevolverParts.Cylinder], broken: [RevolverParts.Cylinder]);

        Assert.False(OrderBench.CanSetUp(order, out var reason));
        Assert.Contains("drawer", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CanSetUp_DirtWithoutADrawing_NotYet()
    {
        var order = MakeOrder([RevolverParts.BasePin], dirty: [new DirtSpec(RevolverParts.BasePin, DirtKind.Grime)]);

        Assert.False(OrderBench.CanSetUp(order, out var reason));
        Assert.Contains("Grime", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_OnlyThePin_HasNoBlocksAndNoDirt()
    {
        var bench = new OrderBench(MakeOrder([RevolverParts.BasePin]));

        Assert.Equal(1, bench.Count);
        Assert.True(bench.Gun.CanMove(0));
        Assert.Null(bench.DirtAt(0));
    }

    [Fact]
    public void IndexOf_PartNotInTheOrder_IsMinusOne()
    {
        Assert.Equal(-1, _bench.IndexOf(RevolverParts.Hammer));
        Assert.Throws<KeyNotFoundException>(() => _bench.Part(RevolverParts.Hammer));
    }

    [Fact]
    public void Step_DirtyPartStillInTheGun_DragsAsUsual()
    {
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);

        Drag(BenchLayout.CylinderSeated, BenchLayout.CylinderOut);

        Assert.Equal(PartStop.End, _bench.Part(RevolverParts.Cylinder).Stop);
        Assert.True(_bench.NeedsScrubbing(Cylinder));
    }

    [Fact]
    public void Step_DirtyPartOut_MovesAnywhereButDoesNotGoBack()
    {
        TakeApart();
        var elsewhere = new Vector2(200, 300);
        Drag(BenchLayout.CylinderOut, elsewhere);
        Assert.Equal(elsewhere.X, _bench.Part(RevolverParts.Cylinder).Position.X, 0.5f);

        _bench.Step(Press(_bench.Part(RevolverParts.Cylinder).Position), Dt);
        var refused = DragEvents.None;
        for (var i = 0; i < 90; i++)
        {
            _bench.Step(Hold(BenchLayout.CylinderSeated), Dt);
            refused |= _bench.Gun.Events[Cylinder];
        }

        _bench.Step(Release(BenchLayout.CylinderSeated), Dt);

        Assert.True((refused & DragEvents.DockRefused) != DragEvents.None);
        Assert.True(_bench.Part(RevolverParts.Cylinder).IsFree);
        Assert.Equal(PartStop.End, _bench.Part(RevolverParts.Cylinder).Stop);
        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
    }

    [Fact]
    public void Step_PinBackWhileTheCylinderIsOut_IsRefused()
    {
        TakeApart();

        _bench.Step(Press(BenchLayout.PinOut), Dt);
        var events = _bench.Gun.Events[_bench.IndexOf(RevolverParts.BasePin)];

        Assert.True((events & DragEvents.DockRefused) != DragEvents.None);
        Assert.True(_bench.Part(RevolverParts.BasePin).IsFree);
    }

    [Fact]
    public void Step_BrushOverTheDirtyPart_ScrubsAndMeasuresItsSpeed()
    {
        TakeApart();
        _bench.Step(Press(BenchLayout.BrushRest), Dt);

        Rub(-3);

        Assert.Equal(Cylinder, _bench.ScrubbedPart);
        Assert.True(Rust.Dirt < Rust.InitialDirt);
        // Rub moves 2 px per step, 120 px/s; the smoothed speed still carries some of the jump onto the part.
        Assert.InRange(_bench.BrushSpeed, 80, 160);
    }

    [Fact]
    public void Step_BrushOverTheMat_ScrubsNothing()
    {
        TakeApart();
        _bench.Step(Press(BenchLayout.BrushRest), Dt);

        for (var i = 0; i < 20; i++)
        {
            _bench.Step(Hold(new Vector2(380 + (i % 2 * 30), 200)), Dt);
        }

        Assert.Equal(-1, _bench.ScrubbedPart);
        Assert.Equal(0, _bench.BrushSpeed);
        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
    }

    [Fact]
    public void Step_BrushOnAPartInTheGun_ScrubsNothing()
    {
        _bench.Step(Press(BenchLayout.BrushRest), Dt);

        for (var x = -10; x <= 10; x += 2)
        {
            _bench.Step(Hold(BenchLayout.CylinderSeated + new Vector2(x, 0)), Dt);
        }

        Assert.Equal(-1, _bench.ScrubbedPart);
        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
    }

    [Fact]
    public void Step_BrushStopsOnThePart_SpeedFallsToNothing()
    {
        TakeApart();
        _bench.Step(Press(BenchLayout.BrushRest), Dt);
        Rub(0);

        for (var i = 0; i < 30; i++)
        {
            _bench.Step(Hold(BenchLayout.CylinderOut), Dt);
        }

        Assert.InRange(_bench.BrushSpeed, 0, 1);
    }

    [Fact]
    public void Step_ScrubbedPastTheThreshold_ReportsCleanedOnceAndUnlocks()
    {
        TakeApart();
        _bench.Step(Press(BenchLayout.BrushRest), Dt);

        var cleaned = 0;
        for (var y = -10; y <= 10; y++)
        {
            if ((Rub(y) & BenchEvents.Cleaned) != BenchEvents.None)
            {
                cleaned++;
            }
        }

        _bench.Step(Hold(BenchLayout.BrushRest), Dt);
        _bench.Step(Release(BenchLayout.BrushRest), Dt);
        Assert.True(Rust.IsClean);
        Assert.Equal(1, cleaned);
        Assert.False(_bench.NeedsScrubbing(Cylinder));
        Assert.True(_bench.Gun.CanMove(Cylinder));
    }

    [Fact]
    public void IsDone_Untouched_IsFalse()
        => Assert.False(_bench.IsDone);

    [Fact]
    public void IsDone_TakenApartAndBackButRusty_IsFalse()
    {
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);
        Drag(BenchLayout.PinOut, BenchLayout.PinSeated + new Vector2(20, 0));

        Assert.True(_bench.WasOut(_bench.IndexOf(RevolverParts.BasePin)));
        Assert.False(_bench.WasOut(Cylinder));
        Assert.False(_bench.IsDone);
    }

    [Fact]
    public void IsDone_EveryPartOutCleanedAndBack_IsTrue()
    {
        TakeApart();
        _bench.Step(Press(BenchLayout.BrushRest), Dt);
        for (var y = -10; y <= 10 && !Rust.IsClean; y++)
        {
            Rub(y);
        }

        _bench.Step(Hold(BenchLayout.BrushRest), Dt);
        _bench.Step(Release(BenchLayout.BrushRest), Dt);
        Assert.False(_bench.IsDone);

        Drag(BenchLayout.CylinderOut, BenchLayout.CylinderSeated - new Vector2(0, 20));
        Drag(BenchLayout.PinOut, BenchLayout.PinSeated + new Vector2(20, 0));

        Assert.True(_bench.IsDone);
    }

    [Fact]
    public void Step_HammerDown_EveryPartStaysAndAPressSaysHammerFirst()
    {
        _bench.Step(Press(BenchLayout.PinSeated), Dt, hammerOpen: false);
        Assert.Equal(BenchEvents.HammerFirst, _bench.Events);

        for (var i = 0; i < 60; i++)
        {
            _bench.Step(Hold(BenchLayout.PinOut), Dt, hammerOpen: false);
        }

        Assert.Equal(PartStop.Start, _bench.Part(RevolverParts.BasePin).Stop);
        Assert.False(_bench.Gun.CanMove(_bench.IndexOf(RevolverParts.BasePin)));
    }

    [Fact]
    public void Step_HammerDown_BrushCanStillBeTaken()
    {
        _bench.Step(Press(BenchLayout.BrushRest), Dt, hammerOpen: false);

        Assert.True(_bench.Brush.IsHeld);
        Assert.Equal(BenchEvents.None, _bench.Events);
    }

    [Fact]
    public void Step_BrushInTheHand_PartsDoNotSeeThePointer()
    {
        _bench.Step(Press(BenchLayout.BrushRest), Dt);

        _bench.Step(Hold(BenchLayout.PinSeated), Dt);

        Assert.False(_bench.Part(RevolverParts.BasePin).IsHovered);
        Assert.True(_bench.Brush.IsHeld);
    }

    [Fact]
    public void Step_PartInTheHand_BrushCannotBeTaken()
    {
        _bench.Step(Press(BenchLayout.PinSeated), Dt);

        _bench.Step(Hold(BenchLayout.BrushRest), Dt);
        _bench.Step(Press(BenchLayout.BrushRest), Dt);

        Assert.False(_bench.Brush.IsHeld);
    }
}
