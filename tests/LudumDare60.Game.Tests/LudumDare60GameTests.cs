using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Testing;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Customers;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests;

public sealed class LudumDare60GameTests
{
    private const double Step = 1.0 / 60;

    // Texture ids in creation order: the two sheets on the first frame, the rust when the first customer comes in.
    private const int RustTexture = 2;

    // Sound ids in creation order.
    private const int DoorBellSound = 2;

    // The shipped data without the background, which has its own tests.
    private readonly ManualPlatform _platform = new() { Assets = new ShippedAssets(withBackground: false) };
    private readonly LudumDare60Game _game = new();

    public LudumDare60GameTests()
        => _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(_game, _platform);

    private Shop Shop => _game.Shop!;

    private OrderBench Bench => _game.Bench!;

    private DirtLayer Rust => Bench.Dirt(RevolverParts.Cylinder)!;

    private SubmittedFrame LastFrame => _platform.Backend.Frames[^1];

    private static RawInputEvent Resize(float width, float height, float ratio)
        => new(RawInputEventKind.Resize, 0, 0, 0, 0, width, height, ratio);

    private static RawInputEvent PointerDown(Vector2 at)
        => new(RawInputEventKind.PointerDown, 0, (ushort)PointerType.Mouse, 1, 0, at.X, at.Y, 0);

    private static RawInputEvent PointerMove(Vector2 at)
        => new(RawInputEventKind.PointerMove, 0, (ushort)PointerType.Mouse, 1, 0, at.X, at.Y, 0);

    private static RawInputEvent PointerUp(Vector2 at)
        => new(RawInputEventKind.PointerUp, 0, (ushort)PointerType.Mouse, 1, 0, at.X, at.Y, 0);

    private static int AlphaAt(byte[] pixels, int width, int x, int y) => pixels[(((y * width) + x) * 4) + 3];

    /// <summary>
    /// The instance drawn from <paramref name="source"/> in <paramref name="frame"/>, or null. Every sprite of the
    /// sheets has its own rectangle.
    /// </summary>
    private static SpriteInstance? Sprite(SubmittedFrame frame, IntRect source)
    {
        foreach (var instance in frame.Instances)
        {
            if (instance.SourceX == source.X && instance.SourceY == source.Y && instance.SourceWidth == source.Width && instance.SourceHeight == source.Height)
            {
                return instance;
            }
        }

        return null;
    }

    private static SpriteInstance Drawn(SubmittedFrame frame, IntRect source)
        => Sprite(frame, source) ?? throw new Xunit.Sdk.XunitException($"Nothing was drawn from {source}.");

    /// <summary>
    /// Starts at one logical pixel per virtual pixel, with every sound decoded, waits for the first customer's revolver
    /// to land on the mat, and puts its hammer at half cock, the first step of every repair.
    /// </summary>
    private void StartWithSound()
    {
        ArriveWithSound();
        PullHammer();
        Assert.Equal(HammerState.HalfCock, Shop.Test!.State);
    }

    /// <summary>
    /// <see cref="StartWithSound"/> without the half cock: the hammer is still down.
    /// </summary>
    private void ArriveWithSound()
    {
        _platform.Frame(0, Resize(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight, 1));
        _platform.AudioBackend.ReportReady(0, 1, 2);
        _platform.Frame(Step);
        for (var i = 0; i < 600 && !(Shop.Phase == ShopPhase.Working && Shop.PhaseTime > Shop.DropSeconds); i++)
        {
            Idle(1);
        }

        Assert.Equal(ShopPhase.Working, Shop.Phase);
    }

    /// <summary>
    /// Holds the pointer at <paramref name="at"/> for <paramref name="frames"/> frames of one fixed step each.
    /// </summary>
    private void HoldAt(Vector2 at, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            _platform.Frame(Step, PointerMove(at));
        }
    }

    /// <summary>
    /// Frames with the pointer up, far from everything.
    /// </summary>
    private void Idle(int frames) => HoldAt(new Vector2(600, 40), frames);

    /// <summary>
    /// Presses at <paramref name="from"/>, holds at <paramref name="to"/> for 90 frames and lets go.
    /// </summary>
    private void Drag(Vector2 from, Vector2 to)
    {
        _platform.Frame(Step, PointerDown(from));
        HoldAt(to, 90);
        _platform.Frame(Step, PointerUp(to));
    }

    private int PlayCount() => _platform.AudioBackend.AllCommands().Count(c => c.Kind == AudioCommandKind.Play);

    /// <summary>
    /// The pitches of the one-shot sounds played after the first <paramref name="skip"/>.
    /// </summary>
    private List<double> PlayedPitches(int skip = 0)
        => [.. _platform.AudioBackend.AllCommands().Where(c => c.Kind == AudioCommandKind.Play).Skip(skip).Select(c => c.Arguments[3])];

    private List<double> VoiceVolumes()
        => [.. _platform.AudioBackend.AllCommands().Where(c => c.Kind == AudioCommandKind.SetVoice).Select(c => c.Arguments[1])];

    private void TakeApart()
    {
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut - new Vector2(20, 0));
        Drag(BenchLayout.CylinderSeated, BenchLayout.CylinderOut + new Vector2(0, 20));
    }

    private void PutBack()
    {
        Drag(BenchLayout.CylinderOut, BenchLayout.CylinderSeated - new Vector2(0, 20));
        Drag(BenchLayout.PinOut, BenchLayout.PinSeated + new Vector2(20, 0));
    }

    /// <summary>
    /// With the brush in the hand, rubs it back and forth across the cylinder at <paramref name="y"/> pixels from its
    /// middle, about 180 px/s.
    /// </summary>
    private void RubAcrossCylinder(float y)
    {
        var center = BenchLayout.CylinderOut;
        for (var x = -12; x <= 12; x += 3)
        {
            _platform.Frame(Step, PointerMove(center + new Vector2(x, y)));
        }

        for (var x = 12; x >= -12; x -= 3)
        {
            _platform.Frame(Step, PointerMove(center + new Vector2(x, y)));
        }
    }

    /// <summary>
    /// Takes the brush and rubs the cylinder row by row until the rust counts as clean, then puts the brush back.
    /// </summary>
    private void ScrubCylinderClean()
    {
        var center = BenchLayout.CylinderOut;
        for (var pass = 0; pass < 4 && !Rust.IsClean; pass++)
        {
            _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));
            for (var y = -9; y <= 9 && !Rust.IsClean; y += 2)
            {
                _platform.Frame(Step, PointerMove(center + new Vector2(-13, y)));
                _platform.Frame(Step, PointerMove(center + new Vector2(13, y)));
            }

            _platform.Frame(Step, PointerUp(center));
            Idle(30);
        }

        Assert.True(Rust.IsClean);
    }

    private void Repair()
    {
        TakeApart();
        ScrubCylinderClean();
        PutBack();
        Assert.Equal(ShopPhase.Testing, Shop.Phase);
    }

    /// <summary>
    /// Takes the hammer's spur where it is and pulls it back past full cock: half cock while repairing, full cock in
    /// the test.
    /// </summary>
    private void PullHammer()
    {
        var test = Shop.Test!;
        Drag(test.Spur, test.SpurCocked + (test.CockDirection * 10));
    }

    private void CockHammer() => PullHammer();

    private void PullTrigger()
    {
        _platform.Frame(Step, PointerDown(ActionTest.TriggerCenter));
        _platform.Frame(Step, PointerUp(ActionTest.TriggerCenter));
    }

    private void RepairAndTest()
    {
        Repair();
        CockHammer();
        PullTrigger();
        Assert.Equal(ShopPhase.Ready, Shop.Phase);
    }

    private void HandBack() => Drag(Shop.GunGrip, Shop.GunGrip + Shop.HandBackOffset - new Vector2(0, 30));

    [Fact]
    public void FirstFrame_CreatesTheSheetsAndDrawsAnEmptyShop()
    {
        _platform.Frame(0);

        Assert.Equal($"create 0 {PlaceholderParts.Width}x{PlaceholderParts.Height}", _platform.Backend.Calls[0]);
        Assert.Equal($"create 1 {PlaceholderCustomers.Width}x{PlaceholderCustomers.Height}", _platform.Backend.Calls[1]);
        var frame = Assert.Single(_platform.Backend.Frames);
        var commands = frame.Decode();
        Assert.Equal(RenderCommandKind.Clear, commands[1].Kind);
        Assert.Equal(BitConverter.ToInt32([0x0b, 0x4f, 0x45, 0xff]), commands[1].Arguments[0]);
        Assert.Empty(frame.Instances);
    }

    [Fact]
    public void FirstFrame_OpensTheShopWithTheCounterEmpty()
    {
        _platform.Frame(0);

        Assert.Equal(5, _game.Orders!.Orders.Count);
        Assert.Equal(ShopPhase.Waiting, Shop.Phase);
        Assert.Null(_game.Bench);
    }

    [Fact]
    public void FirstFrame_NoOrdersFile_StopsTheGameNamingIt()
    {
        var platform = new ManualPlatform();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(new LudumDare60Game(), platform);

        var error = Assert.Throws<InvalidOperationException>(() => platform.Frame(0));

        Assert.Contains(AssetPaths.Orders, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstFrame_CreatesTheClickTheScrubLoopAndTheDoorBellAsWav()
    {
        _platform.Frame(0);

        Assert.Equal(3, _platform.AudioBackend.Sounds.Count);
        var click = _platform.AudioBackend.Sounds[0];
        Assert.Equal("RIFF"u8.ToArray(), click[..4]);
        Assert.Equal("WAVE"u8.ToArray(), click[8..12]);
        Assert.Equal(44 + (PlaceholderSounds.SampleRate * 60 / 1000 * 2), click.Length);
        Assert.Equal(PlaceholderSounds.CreateDoorBell(), _platform.AudioBackend.Sounds[DoorBellSound]);
    }

    [Fact]
    public void Frames_CreateTheSheetsAndSoundsOnce()
    {
        _platform.Frame(0);
        _platform.Frame(0.01);

        Assert.Equal(2, _platform.Backend.Textures.Count);
        Assert.Equal(3, _platform.AudioBackend.Sounds.Count);
        Assert.Equal(2, _platform.Backend.Frames.Count);
    }

    [Fact]
    public void PartsTexture_PinHeadIsOpaqueAndItsGlowSurroundsIt()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[0];
        var pin = PlaceholderParts.Pin;
        var glow = PlaceholderParts.PinGlow;
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, pin.X + 2, pin.Y + 3));
        Assert.Equal(0, AlphaAt(pixels, PlaceholderParts.Width, pin.X + 20, pin.Y));
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, glow.X, glow.Y + 1));
        Assert.Equal(0, AlphaAt(pixels, PlaceholderParts.Width, glow.X + 3, glow.Y + 4));
    }

    [Fact]
    public void PartsTexture_WindowAndPinGapAreOpenAndTheGlowGoesAroundTheRevolver()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[0];
        var window = PlaceholderParts.CylinderInBody;
        var pinHead = PlaceholderParts.PinInBody;
        var glow = PlaceholderParts.BodyGlow;
        Assert.Equal(0, AlphaAt(pixels, PlaceholderParts.Width, (int)window.X, (int)window.Y));
        Assert.Equal(0, AlphaAt(pixels, PlaceholderParts.Width, (int)pinHead.X, (int)pinHead.Y));
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, 50, 18));

        // The barrel's top edge is at row 14: its glow one row above, none inside.
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, glow.X + 51, glow.Y + 14));
        Assert.Equal(0, AlphaAt(pixels, PlaceholderParts.Width, glow.X + 51, glow.Y + 19));
    }

    [Fact]
    public void CustomersTexture_EveryShippedLookHasABustStandingOnItsBottomEdge()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[1];
        foreach (var order in _game.Orders!.Orders)
        {
            Assert.True(PlaceholderCustomers.HasLook(order.Customer.Look), order.Customer.Look);
            var bust = PlaceholderCustomers.Bust(order.Customer.Look);
            Assert.Equal(255, AlphaAt(pixels, PlaceholderCustomers.Width, bust.X + (bust.Width / 2), bust.Bottom - 1));
            Assert.Equal(0, AlphaAt(pixels, PlaceholderCustomers.Width, bust.X, bust.Y));
        }
    }

    [Fact]
    public void FirstCustomer_ComesInWithTheDoorBellAndWalksToTheCounter()
    {
        _platform.Frame(0);
        _platform.AudioBackend.ReportReady(0, 1, 2);

        HoldAt(new Vector2(600, 40), (int)(Shop.WaitSeconds * 60) + 2);
        var entering = Drawn(LastFrame, PlaceholderCustomers.Bust("hoodie"));
        HoldAt(new Vector2(600, 40), (int)(Shop.WalkSeconds * 60) + 2);
        var arrived = Drawn(LastFrame, PlaceholderCustomers.Bust("hoodie"));

        Assert.Contains(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.Play && (int)c.Arguments[0] == DoorBellSound);
        Assert.InRange(entering.Position.X, Shop.CounterX + 50, Shop.DoorX);
        Assert.Equal(new Vector2(Shop.CounterX, LudumDare60Game.CustomerBottom), arrived.Position);
        Assert.Contains($"create {RustTexture} {PlaceholderParts.Cylinder.Width}x{PlaceholderParts.Cylinder.Height}", _platform.Backend.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void AtTheCounter_TheRevolverLandsWithAThudAndTheBubbleShowsTheSymptom()
    {
        StartWithSound();

        Assert.Contains(0.35, PlayedPitches().Select(p => Math.Round(p, 3)));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Bubble));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Icon(Symptom.JammedCylinder)));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Face(Expression.Neutral)));
        Assert.Equal(BenchLayout.PinSeated, Drawn(LastFrame, PlaceholderParts.Pin).Position);
    }

    [Fact]
    public void HammerDown_OnlyTheHammerGlows()
    {
        ArriveWithSound();
        Idle(1);

        Assert.InRange(Drawn(LastFrame, PlaceholderParts.HammerGlow).Tint.A, 1, 255);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.PinGlow).Tint.A);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.CylinderGlow).Tint.A);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.Hammer).Rotation);
    }

    [Fact]
    public void HammerDown_PressThePin_DullTickAndTheHammerFlashes()
    {
        ArriveWithSound();
        var before = PlayCount();

        _platform.Frame(Step, PointerDown(BenchLayout.PinSeated));
        var flash = Drawn(LastFrame, PlaceholderParts.HammerGlow).Tint.A;
        HoldAt(BenchLayout.PinOut - new Vector2(20, 0), 60);

        Assert.Equal(255, flash);
        Assert.Equal([0.4], PlayedPitches(before), (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.Equal(PartStop.Start, Bench.Part(RevolverParts.BasePin).Stop);
    }

    [Fact]
    public void HalfCock_AtTwelveFramesPerSecond_StillOpensTheRevolver()
    {
        ArriveWithSound();
        var test = Shop.Test!;
        const double Frame = 1.0 / 12;
        var to = test.SpurCocked + (test.CockDirection * 10);

        // Like a slow browser frame: several pointer events per frame, spread over it.
        _platform.Frame(Frame, PointerDownAt(test.Spur, 0.01f), PointerMoveAt(Vector2.Lerp(test.Spur, to, 0.3f), 0.05f));
        for (var i = 1; i <= 6; i++)
        {
            _platform.Frame(Frame, PointerMoveAt(Vector2.Lerp(test.Spur, to, 0.3f + (i * 0.1f)), 0.02f), PointerMoveAt(to, 0.06f));
        }

        _platform.Frame(Frame, PointerUpAt(to, 0.03f));
        Idle(5);

        Assert.Equal(HammerState.HalfCock, test.State);
    }

    private static RawInputEvent PointerDownAt(Vector2 at, float time)
        => new(RawInputEventKind.PointerDown, 0, (ushort)PointerType.Mouse, 1, time, at.X, at.Y, 0);

    private static RawInputEvent PointerMoveAt(Vector2 at, float time)
        => new(RawInputEventKind.PointerMove, 0, (ushort)PointerType.Mouse, 1, time, at.X, at.Y, 0);

    private static RawInputEvent PointerUpAt(Vector2 at, float time)
        => new(RawInputEventKind.PointerUp, 0, (ushort)PointerType.Mouse, 1, time, at.X, at.Y, 0);

    [Fact]
    public void HalfCock_ClicksStaysBackAndThePinStartsToGlow()
    {
        ArriveWithSound();
        var before = PlayCount();

        PullHammer();
        Idle(1);

        Assert.Contains(1.1, PlayedPitches(before).Select(p => Math.Round(p, 3)));
        Assert.Equal(ActionTest.HalfCock * ActionTest.CockAngle, Drawn(LastFrame, PlaceholderParts.Hammer).Rotation, 1e-4);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.HammerGlow).Tint.A);
        Assert.InRange(Drawn(LastFrame, PlaceholderParts.PinGlow).Tint.A, 1, 255);
        Assert.Null(Sprite(LastFrame, PlaceholderParts.CylinderTurned));
    }

    [Fact]
    public void Render_Assembled_DrawsPinFrameAndCylinderWithoutCylinderGlow()
    {
        StartWithSound();

        Assert.Equal(BenchLayout.GunPosition, Drawn(LastFrame, PlaceholderParts.Body).Position);
        Assert.Equal(BenchLayout.CylinderSeated, Drawn(LastFrame, PlaceholderParts.Cylinder).Position);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.CylinderGlow).Tint.A);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.BodyGlow).Tint.A);
        Assert.Equal(BenchLayout.BrushRest, Drawn(LastFrame, PlaceholderParts.Brush).Position);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.BrushGlow).Tint.A);
    }

    [Fact]
    public void PointerOverPin_GlowsAtFullStrength()
    {
        StartWithSound();

        _platform.Frame(Step, PointerMove(BenchLayout.PinSeated + new Vector2(0, 2)));

        Assert.True(Bench.Part(RevolverParts.BasePin).IsHovered);
        Assert.Equal(255, Drawn(LastFrame, PlaceholderParts.PinGlow).Tint.A);
    }

    [Fact]
    public void PointerAwayFromPin_GlowsFaintly()
    {
        StartWithSound();

        _platform.Frame(Step, PointerMove(new Vector2(500, 300)));

        Assert.False(Bench.Part(RevolverParts.BasePin).IsHovered);
        Assert.InRange(Drawn(LastFrame, PlaceholderParts.PinGlow).Tint.A, 1, 254);
    }

    [Fact]
    public void DragPinOut_SnapsItFreeWithClicks()
    {
        StartWithSound();
        var before = PlayCount();

        Drag(BenchLayout.PinSeated, BenchLayout.PinOut - new Vector2(20, 0));

        Assert.Equal(PartStop.End, Bench.Part(RevolverParts.BasePin).Stop);
        Assert.Equal(BenchLayout.PinOut, Drawn(LastFrame, PlaceholderParts.Pin).Position);
        Assert.Equal([0.55, 1.25], PlayedPitches(before), (a, b) => Math.Abs(a - b) < 1e-6);
    }

    [Fact]
    public void PushPinBackIn_SeatsItWithALowClick()
    {
        StartWithSound();
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut - new Vector2(20, 0));

        Drag(BenchLayout.PinOut, BenchLayout.PinSeated + new Vector2(20, 0));

        Assert.Equal(PartStop.Start, Bench.Part(RevolverParts.BasePin).Stop);
        Assert.Equal(BenchLayout.PinSeated, Drawn(LastFrame, PlaceholderParts.Pin).Position);
        Assert.Equal(0.75, PlayedPitches()[^1], 1e-6);
    }

    [Fact]
    public void PullPinLessThanBreakaway_KeepsItSeatedAndSilent()
    {
        StartWithSound();
        var before = PlayCount();

        _platform.Frame(Step, PointerDown(BenchLayout.PinSeated));
        HoldAt(BenchLayout.PinSeated - new Vector2(4, 0), 30);

        Assert.Equal(PartStop.Start, Bench.Part(RevolverParts.BasePin).Stop);
        Assert.True(Bench.Part(RevolverParts.BasePin).IsHeld);
        Assert.Empty(PlayedPitches(before));
    }

    [Fact]
    public void PressOnCylinderFirst_RefusesWithADullTickAndFlashesThePin()
    {
        StartWithSound();
        var before = PlayCount();

        _platform.Frame(Step, PointerDown(BenchLayout.CylinderSeated));
        var flash = Drawn(LastFrame, PlaceholderParts.PinGlow).Tint.A;
        HoldAt(BenchLayout.CylinderOut + new Vector2(0, 20), 3);

        Assert.Equal(PartStop.Start, Bench.Part(RevolverParts.Cylinder).Stop);
        Assert.False(Bench.Part(RevolverParts.Cylinder).IsHeld);
        Assert.Equal([0.4], PlayedPitches(before), (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.Equal(255, flash);
    }

    [Fact]
    public void RustyCylinderSeated_IsDraggedNotScrubbed()
    {
        StartWithSound();

        TakeApart();

        Assert.Equal(PartStop.End, Bench.Part(RevolverParts.Cylinder).Stop);
        Assert.Equal(BenchLayout.CylinderOut, Drawn(LastFrame, PlaceholderParts.Cylinder).Position);
        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
    }

    [Fact]
    public void RustyCylinderOut_BareHands_NeitherDragNorScrubButFlashTheBrush()
    {
        StartWithSound();
        TakeApart();

        _platform.Frame(Step, PointerDown(BenchLayout.CylinderOut));
        var flash = Drawn(LastFrame, PlaceholderParts.BrushGlow).Tint.A;
        HoldAt(BenchLayout.CylinderSeated - new Vector2(0, 20), 90);

        Assert.True(Bench.Gun.IsLocked(Bench.IndexOf(RevolverParts.Cylinder)));
        Assert.Equal(PartStop.End, Bench.Part(RevolverParts.Cylinder).Stop);
        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
        Assert.Equal(0.4, PlayedPitches()[^1], 1e-6);
        Assert.Equal(255, flash);
    }

    [Fact]
    public void PickUpBrush_FollowsThePointerAndStartsASilentScrubLoop()
    {
        StartWithSound();

        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest + new Vector2(10, 1)));
        HoldAt(new Vector2(400, 200), 2);

        Assert.True(Bench.Brush.IsHeld);
        Assert.Equal(new Vector2(400, 200), Drawn(LastFrame, PlaceholderParts.Brush).Position);
        var loop = Assert.Single(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.PlayLoop);
        Assert.Equal(1, loop.Arguments[1]);
        Assert.All(VoiceVolumes(), volume => Assert.Equal(0, volume));
    }

    [Fact]
    public void BrushOverRustyCylinder_ScrubsUploadsRowsAndRasps()
    {
        StartWithSound();
        TakeApart();

        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));
        RubAcrossCylinder(-3);

        Assert.True(Rust.Dirt < Rust.InitialDirt);
        Assert.Contains(_platform.Backend.Calls, call => call.StartsWith($"update {RustTexture} rows ", StringComparison.Ordinal));
        Assert.Contains(VoiceVolumes(), volume => volume > 0.3);
        Assert.Equal(PartStop.End, Bench.Part(RevolverParts.Cylinder).Stop);
    }

    [Fact]
    public void BrushMovedOverTheMat_ErasesNothingAndStaysSilent()
    {
        StartWithSound();
        TakeApart();

        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));
        for (var i = 0; i < 20; i++)
        {
            _platform.Frame(Step, PointerMove(new Vector2(380 + (i % 2 * 30), 200)));
        }

        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
        Assert.All(VoiceVolumes(), volume => Assert.Equal(0, volume));
    }

    [Fact]
    public void BrushHeldStill_OnTheCylinder_FallsSilent()
    {
        StartWithSound();
        TakeApart();
        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));
        RubAcrossCylinder(0);

        HoldAt(BenchLayout.CylinderOut, 30);

        Assert.Equal(Bench.IndexOf(RevolverParts.Cylinder), Bench.ScrubbedPart);
        Assert.InRange(VoiceVolumes()[^1], 0, 0.05);
    }

    [Fact]
    public void PutBrushDown_StopsTheLoopAndItSlidesHome()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));
        HoldAt(new Vector2(400, 200), 3);

        _platform.Frame(Step, PointerUp(new Vector2(400, 200)));
        Idle(60);

        Assert.True(Bench.Brush.IsResting);
        Assert.Single(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.StopVoice);
        Assert.Contains(0.7, PlayedPitches().Select(p => Math.Round(p, 3)));
        Assert.Equal(BenchLayout.BrushRest, Drawn(LastFrame, PlaceholderParts.Brush).Position);
    }

    [Fact]
    public void BrushInHand_PartsDoNotLightUpOrMove()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));

        HoldAt(BenchLayout.PinSeated, 3);
        HoldAt(BenchLayout.PinOut - new Vector2(20, 0), 60);

        Assert.False(Bench.Part(RevolverParts.BasePin).IsHovered);
        Assert.Equal(PartStop.Start, Bench.Part(RevolverParts.BasePin).Stop);
    }

    [Fact]
    public void ScrubCylinderClean_ChimesFadesTheRustAndUnlocksIt()
    {
        StartWithSound();
        TakeApart();

        ScrubCylinderClean();
        Idle(40);

        Assert.False(Bench.Gun.IsLocked(Bench.IndexOf(RevolverParts.Cylinder)));
        Assert.Contains(2.0, PlayedPitches());
        Assert.Null(Sprite(LastFrame, new IntRect(0, 0, PlaceholderParts.Cylinder.Width, PlaceholderParts.Cylinder.Height)));
    }

    [Fact]
    public void BackTogether_TheHammerGlowsForTheTest()
    {
        StartWithSound();
        var before = PlayCount();

        Repair();
        Idle(1);

        var cylinderClicks = PlayedPitches(before).Where(p => Math.Abs(p - 1.0) < 1e-6 || Math.Abs(p - 0.6) < 1e-6);
        Assert.Equal([1.0, 0.6], cylinderClicks, (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.InRange(Drawn(LastFrame, PlaceholderParts.HammerGlow).Tint.A, 1, 255);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.TriggerGlow).Tint.A);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.PinGlow).Tint.A);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.BodyGlow).Tint.A);
        Assert.Null(Sprite(LastFrame, PlaceholderCustomers.Hand));
        Assert.Equal(new Vector2(Shop.CounterX, LudumDare60Game.CustomerBottom), Drawn(LastFrame, PlaceholderCustomers.Bust("hoodie")).Position);
        Assert.Equal(BenchLayout.HammerPivot, Drawn(LastFrame, PlaceholderParts.Hammer).Position);
        Assert.Equal(ActionTest.HalfCock * ActionTest.CockAngle, Drawn(LastFrame, PlaceholderParts.Hammer).Rotation, 1e-4);
    }

    [Fact]
    public void TriggerBeforeTheHammer_GivesADullTickAndFlashesTheHammer()
    {
        StartWithSound();
        Repair();
        var before = PlayCount();

        _platform.Frame(Step, PointerDown(ActionTest.TriggerCenter));
        var flash = Drawn(LastFrame, PlaceholderParts.HammerGlow).Tint.A;

        Assert.Equal([0.4], PlayedPitches(before), (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.Equal(255, flash);
        Assert.Equal(ShopPhase.Testing, Shop.Phase);
    }

    [Fact]
    public void CockTheHammer_RatchetClicksTurnTheCylinderAndTheTriggerGlows()
    {
        StartWithSound();
        Repair();
        var before = PlayCount();

        CockHammer();

        // From half cock, where the repair left it: no second half-cock click.
        var pitches = PlayedPitches(before).Select(p => Math.Round(p, 3)).ToList();
        Assert.DoesNotContain(1.1, pitches);
        Assert.Contains(0.9, pitches);
        Assert.Contains(1.3, pitches);
        Assert.Equal(ActionTest.CockAngle, Drawn(LastFrame, PlaceholderParts.Hammer).Rotation, 1e-4);
        Assert.Equal(BenchLayout.CylinderSeated, Drawn(LastFrame, PlaceholderParts.CylinderTurned).Position);
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Cylinder));
        Assert.InRange(Drawn(LastFrame, PlaceholderParts.TriggerGlow).Tint.A, 1, 255);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.HammerGlow).Tint.A);
    }

    [Fact]
    public void PullTheTriggerWhenCocked_DryFiresAndTheCustomerHoldsOutAHand()
    {
        StartWithSound();
        Repair();
        CockHammer();
        var before = PlayCount();

        PullTrigger();

        var pitches = PlayedPitches(before).Select(p => Math.Round(p, 3)).ToList();
        Assert.Contains(1.7, pitches);
        Assert.Contains(1.5, pitches);
        Assert.Equal(ShopPhase.Ready, Shop.Phase);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.Hammer).Rotation);
        Assert.True(Drawn(LastFrame, PlaceholderParts.Trigger).Rotation < 0, "The trigger is still pulled back.");
        Assert.InRange(Drawn(LastFrame, PlaceholderParts.BodyGlow).Tint.A, 1, 255);
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Hand));
        Assert.Null(Sprite(LastFrame, PlaceholderCustomers.Bubble));
    }

    [Fact]
    public void HandingBack_TheRevolverRisesWithItsSeatedPartsHammerAndTrigger()
    {
        StartWithSound();
        RepairAndTest();

        _platform.Frame(Step, PointerDown(Shop.GunGrip));
        HoldAt(Shop.GunGrip + new Vector2(0, -30), 20);

        var lift = Drawn(LastFrame, PlaceholderParts.Body).Position - BenchLayout.GunPosition;
        Assert.InRange(lift.Y, -40, -10);
        Assert.Equal(lift, Drawn(LastFrame, PlaceholderParts.Pin).Position - BenchLayout.PinSeated);
        Assert.Equal(lift, Drawn(LastFrame, PlaceholderParts.Hammer).Position - BenchLayout.HammerPivot);
        Assert.Equal(lift, Drawn(LastFrame, PlaceholderParts.Trigger).Position - BenchLayout.TriggerPivot);
    }

    [Fact]
    public void HandBack_TheCustomerTakesTheRevolverAndSmiles()
    {
        StartWithSound();
        RepairAndTest();
        var before = PlayCount();

        HandBack();

        Assert.Equal(ShopPhase.Thanking, Shop.Phase);
        Assert.Contains(0.9, PlayedPitches(before).Select(p => Math.Round(p, 3)));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Face(Expression.Thankful)));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Body));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Hammer));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Brush));
    }

    [Fact]
    public void AfterHandBack_TheCustomerLeavesWithTheBellAndTheShopCloses()
    {
        StartWithSound();
        RepairAndTest();
        HandBack();

        Idle((int)((Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds) * 60) + 10);

        var bells = _platform.AudioBackend.AllCommands().Count(c => c.Kind == AudioCommandKind.Play && (int)c.Arguments[0] == DoorBellSound);
        Assert.Equal(2, bells);
        Assert.Equal(ShopPhase.Closed, Shop.Phase);
        Assert.Contains($"destroy {RustTexture}", _platform.Backend.Calls, StringComparer.Ordinal);
        Assert.Empty(LastFrame.Instances);
    }

    [Fact]
    public void FirstFrame_NoBackgroundFile_DrawsOnlyTheMatColor()
    {
        _platform.Frame(0);

        Assert.False(_game.HasBackground);
        Assert.Equal(2, _platform.Backend.Textures.Count);
    }

    [Fact]
    public void FirstFrame_BackgroundFileShipped_CreatesScreenSizedTexture()
    {
        var (platform, game) = StartWithShippedAssets();

        platform.Frame(0);

        Assert.True(game.HasBackground);
        Assert.Contains($"create 2 {LudumDare60Game.VirtualWidth}x{LudumDare60Game.VirtualHeight}", platform.Backend.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void Render_BackgroundLoaded_DrawsItFirstAtTheOrigin()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);

        var frame = Assert.Single(platform.Backend.Frames);
        var background = Assert.Single(frame.Instances);
        Assert.Equal(Vector2.Zero, background.Position);
        Assert.Equal(LudumDare60Game.VirtualWidth, background.SourceWidth);
        Assert.Equal(LudumDare60Game.VirtualHeight, background.SourceHeight);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [2, 0, 1]), frame.Decode()[2]);
    }

    [Fact]
    public void Frames_BackgroundLoaded_CreatesItsTextureOnce()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);
        platform.Frame(0.01);

        Assert.Equal(3, platform.Backend.Textures.Count);
        Assert.Single(platform.Backend.Frames[1].Instances);
    }

    private static (ManualPlatform Platform, LudumDare60Game Game) StartWithShippedAssets()
    {
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true) };
        var game = new LudumDare60Game();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        return (platform, game);
    }

    /// <summary>
    /// Reads the files of <c>wwwroot/assets/</c>, copied next to the test assembly.
    /// </summary>
    private sealed class ShippedAssets(bool withBackground) : IAssetSource
    {
        // Reads finish at once, so a file requested in Initialize is loaded in the first frame.
        public Task<byte[]> ReadAsync(string path)
            => !withBackground && string.Equals(path, AssetPaths.Bench, StringComparison.Ordinal)
                ? Task.FromException<byte[]>(new FileNotFoundException(path))
                : Task.FromResult(Read(path));

        private static byte[] Read(string path) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", path));
    }
}
