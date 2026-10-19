using System.Numerics;
using System.Text;
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

    // Texture ids in creation order: the two placeholder sheets, the hoodie, robe, dog-walker and parcel customers'
    // sheets with their glows on the first frame, the rust when the first customer comes in. With the background, the
    // room and the street come right after the placeholder sheets.
    private const int CustomersTexture = 1;
    private const int CustomerTexture = 2;
    private const int CustomerGlowTexture = 3;
    private const int RobeCustomerTexture = 4;
    private const int RustTexture = 10;
    private const int RoomTexture = 2;
    private const int StreetTexture = 3;
    private const int DogWalkerTextureWithBackground = 8;
    private const int ParcelTextureWithBackground = 10;

    // Textures the first frame creates without the background: the two placeholder sheets, and a sheet and its glow per
    // drawn customer.
    private static readonly int s_startTextures = 2 + (2 * AssetPaths.CustomerSheets.Count);

    // Sound ids in creation order.
    private const int DoorBellSound = 2;

    // The shipped data without the background, which has its own tests; so the shop must not wait for it.
    private readonly ManualPlatform _platform = new() { Assets = new ShippedAssets(withBackground: false) };
    private readonly LudumDare60Game _game = new() { WaitsForAssets = false, WaitsForPlayer = false };

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

    private static RawInputEvent KeyDown(Key key) => new(RawInputEventKind.KeyDown, 0, (ushort)key, 0, 0, 0, 0, 0);

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

    /// <summary>
    /// The texture the first instance drawn from <paramref name="source"/> in <paramref name="frame"/> came from, or -1.
    /// </summary>
    private static int TextureOf(SubmittedFrame frame, IntRect source)
    {
        foreach (var command in frame.Decode())
        {
            if (command.Kind != RenderCommandKind.DrawSprites)
            {
                continue;
            }

            for (var i = command.Arguments[1]; i < command.Arguments[1] + command.Arguments[2]; i++)
            {
                var instance = frame.Instances[i];
                if (instance.SourceX == source.X && instance.SourceY == source.Y && instance.SourceWidth == source.Width && instance.SourceHeight == source.Height)
                {
                    return command.Arguments[0];
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// The instance drawn from <paramref name="source"/> of texture <paramref name="texture"/>: several sheets share
    /// rectangles.
    /// </summary>
    private static SpriteInstance DrawnOn(SubmittedFrame frame, int texture, IntRect source)
    {
        foreach (var command in frame.Decode())
        {
            if (command.Kind != RenderCommandKind.DrawSprites || command.Arguments[0] != texture)
            {
                continue;
            }

            for (var i = command.Arguments[1]; i < command.Arguments[1] + command.Arguments[2]; i++)
            {
                var instance = frame.Instances[i];
                if (instance.SourceX == source.X && instance.SourceY == source.Y && instance.SourceWidth == source.Width && instance.SourceHeight == source.Height)
                {
                    return instance;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Nothing was drawn from {source} of texture {texture}.");
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
        ComeToTheCounterWithSound();
        ClickTheCustomer();
        for (var i = 0; i < 600 && !(Shop.Phase == ShopPhase.Working && Shop.PhaseTime > Shop.ThrowSeconds); i++)
        {
            Idle(1);
        }

        Assert.Equal(ShopPhase.Working, Shop.Phase);
    }

    /// <summary>
    /// Starts at one logical pixel per virtual pixel, with every sound decoded, and waits for the first customer to
    /// stand at the counter with the revolver.
    /// </summary>
    private void ComeToTheCounterWithSound()
    {
        _platform.Frame(0, Resize(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight, 1));
        _platform.AudioBackend.ReportReady(0, 1, 2);
        _platform.Frame(Step);
        for (var i = 0; i < 600 && Shop.Phase != ShopPhase.Offering; i++)
        {
            Idle(1);
        }

        Assert.Equal(ShopPhase.Offering, Shop.Phase);
    }

    private static Vector2 CustomerMiddle => new(Shop.CounterX, Shop.CustomerBottom - 60);

    private void ClickTheCustomer()
    {
        _platform.Frame(Step, PointerDown(CustomerMiddle));
        _platform.Frame(Step, PointerUp(CustomerMiddle));
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
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);
        Drag(BenchLayout.CylinderSeated, BenchLayout.CylinderOut);
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

            // Put the brush back where it lies out of the way, not on the cylinder.
            _platform.Frame(Step, PointerMove(BenchLayout.BrushRest));
            _platform.Frame(Step, PointerUp(BenchLayout.BrushRest));
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

        // No customer and no revolver yet; the tray and the tools already lie on the bench, unlit.
        Assert.Null(Sprite(frame, PlaceholderParts.Body));
        Assert.Equal(BenchLayout.TrayMin, Drawn(frame, PlaceholderParts.Tray).Position);
        Assert.Equal(BenchLayout.BrushRest, Drawn(frame, PlaceholderParts.Brush).Position);
        Assert.Equal(BenchLayout.OilCanRest, Drawn(frame, PlaceholderParts.OilCan).Position);
        Assert.Equal(0, Drawn(frame, PlaceholderParts.BrushGlow).Tint.A);
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
    public void FirstFrame_NoAssetFiles_StopsTheGameNamingTheFirstRequested()
    {
        var platform = new ManualPlatform();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(new LudumDare60Game { WaitsForPlayer = false }, platform);

        var error = Assert.Throws<InvalidOperationException>(() => platform.Frame(0));

        Assert.Equal($"{AssetPaths.Bench} did not load.", error.Message);
        Assert.IsType<AssetLoadException>(error.InnerException);
    }

    [Theory]
    [InlineData(AssetPaths.Orders)]
    [InlineData(AssetPaths.Street)]
    public void FirstFrame_OneAssetFails_StopsTheGameNamingIt(string path)
    {
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true, failing: path) };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(new LudumDare60Game { WaitsForPlayer = false }, platform);

        var error = Assert.Throws<InvalidOperationException>(() => platform.Frame(0));

        Assert.Equal($"{path} did not load.", error.Message);
    }

    [Fact]
    public void FirstFrame_CustomerSheetFails_StopsTheGameNamingIt()
    {
        var sheet = AssetPaths.CustomerSheets[0].Value;
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true, failing: sheet) };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(new LudumDare60Game { WaitsForPlayer = false }, platform);

        var error = Assert.Throws<InvalidOperationException>(() => platform.Frame(0));

        Assert.Equal($"{sheet} did not load.", error.Message);
    }

    [Fact]
    public void Frame_AFileStillComing_ShowsLoadingBarAndKeepsShopClosed()
    {
        var assets = new ShippedAssets(withBackground: true, held: AssetPaths.Bench);
        var platform = new ManualPlatform { Assets = assets };
        var game = new LudumDare60Game { WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);

        platform.Frame(0);

        var assetCount = 3 + AssetPaths.CustomerSheets.Count;
        Assert.True(game.IsLoading);
        Assert.Equal((float)(assetCount - 1) / assetCount, game.LoadProgress, 5);
        Assert.Null(game.Shop);
        var frame = platform.Backend.Frames[^1];
        Assert.NotNull(Sprite(frame, PlaceholderParts.Pixel));
        Assert.Null(Sprite(frame, PlaceholderParts.Tray));
    }

    [Fact]
    public void Frame_LastFileArrives_OpensShopAndDrawsScene()
    {
        var assets = new ShippedAssets(withBackground: true, held: AssetPaths.Bench);
        var platform = new ManualPlatform { Assets = assets };
        var game = new LudumDare60Game { WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        platform.Frame(0);

        assets.Release();
        platform.Frame(0.1);

        Assert.False(game.IsLoading);
        Assert.Equal(1f, game.LoadProgress);
        Assert.NotNull(game.Shop);
        var frame = platform.Backend.Frames[^1];
        Assert.Null(Sprite(frame, PlaceholderParts.Pixel));
        Assert.NotNull(Sprite(frame, PlaceholderParts.Tray));
    }

    [Fact]
    public void Frame_AllFilesInFirstFrame_OpensShopAtOnce()
    {
        var (platform, game) = StartWithShippedAssets();

        platform.Frame(0);

        Assert.False(game.IsLoading);
        Assert.NotNull(game.Shop);
    }

    [Fact]
    public void Frame_AllFilesIn_ShowsTitleOverTheRoomAndKeepsShopClosed()
    {
        var (platform, game) = StartOnTitle();

        platform.Frame(0);
        platform.Frame(Step);

        Assert.False(game.IsLoading);
        Assert.True(game.IsOnTitle);
        Assert.Null(game.Shop);
        var frame = platform.Backend.Frames[^1];
        Assert.NotNull(Sprite(frame, PlaceholderParts.PressAnyKey));
        // The street and the room under the title.
        Assert.Equal(2, frame.Instances.Count(i => i.SourceWidth == LudumDare60Game.VirtualWidth));
        Assert.Null(Sprite(frame, PlaceholderParts.Tray));
    }

    [Fact]
    public void Frame_FileStillComing_NoTitleYet()
    {
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true, held: AssetPaths.Bench) };
        var game = new LudumDare60Game();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);

        platform.Frame(0, KeyDown(Key.Space));

        Assert.False(game.IsOnTitle);
        Assert.Null(Sprite(platform.Backend.Frames[^1], PlaceholderParts.PressAnyKey));
    }

    [Fact]
    public void Title_KeyPressed_OpensShopAndHidesTitle()
    {
        var (platform, game) = StartOnTitle();
        platform.Frame(0);

        platform.Frame(Step, KeyDown(Key.Space));

        Assert.False(game.IsOnTitle);
        Assert.NotNull(game.Shop);
        Assert.Equal(ShopPhase.Waiting, game.Shop.Phase);
        Assert.Null(Sprite(platform.Backend.Frames[^1], PlaceholderParts.PressAnyKey));
    }

    [Fact]
    public void Title_PointerPressed_OpensShop()
    {
        var (platform, game) = StartOnTitle();
        platform.Frame(0);

        platform.Frame(Step, PointerDown(new Vector2(320, 180)));

        Assert.NotNull(game.Shop);
    }

    [Fact]
    public void Title_TouchPressed_OpensShop()
    {
        var (platform, game) = StartOnTitle();
        platform.Frame(0);

        platform.Frame(Step, new RawInputEvent(RawInputEventKind.PointerDown, 0, (ushort)PointerType.Touch, 7, 0, 320, 180, 0));

        Assert.NotNull(game.Shop);
    }

    [Fact]
    public void Title_KeyHeldFromLoading_DoesNotStart()
    {
        var assets = new ShippedAssets(withBackground: true, held: AssetPaths.Bench);
        var platform = new ManualPlatform { Assets = assets };
        var game = new LudumDare60Game();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        platform.Frame(0, KeyDown(Key.Space));

        assets.Release();
        platform.Frame(Step);
        platform.Frame(Step);

        Assert.True(game.IsOnTitle);
        Assert.Null(game.Shop);
    }

    [Fact]
    public void Title_NoPress_ShopStaysClosed()
    {
        var (platform, game) = StartOnTitle();

        for (var i = 0; i < 120; i++)
        {
            platform.Frame(Step, PointerMove(new Vector2(320, 180)));
        }

        Assert.True(game.IsOnTitle);
        Assert.Null(game.Shop);
    }

    [Fact]
    public void Frame_NotWaitingForAssets_OpensShopWithoutBackground()
    {
        _platform.Frame(0);

        Assert.False(_game.IsLoading);
        Assert.False(_game.HasBackground);
        Assert.NotNull(_game.Shop);
    }

    [Fact]
    public void Frames_BackgroundStillLoading_GameGoesOn()
    {
        _platform.Frame(0);
        _platform.Frame(0.1);

        Assert.False(_game.HasBackground);
        Assert.False(_game.HasStreet);
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

        Assert.Equal(s_startTextures, _platform.Backend.Textures.Count);
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
    public void CustomersTexture_EveryBustHasAGlowAroundItOpenAtTheBottom()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[CustomersTexture];
        foreach (var look in PlaceholderCustomers.Looks)
        {
            var bust = PlaceholderCustomers.Bust(look);
            var glow = PlaceholderCustomers.BustGlow(look);

            // The bust's bottom middle is solid: its glow neither covers it nor runs under it.
            var middle = bust.Width / 2;
            Assert.Equal(0, AlphaAt(pixels, PlaceholderCustomers.Width, glow.X + 1 + middle, glow.Y + bust.Height));
            Assert.Equal(0, AlphaAt(pixels, PlaceholderCustomers.Width, glow.X + 1 + middle, glow.Bottom - 1));

            // At half height, the pixel just left of the bust glows and the bust's own first pixel does not.
            var row = bust.Height / 2;
            var first = Enumerable.Range(0, bust.Width).First(x => AlphaAt(pixels, PlaceholderCustomers.Width, bust.X + x, bust.Y + row) >= 128);
            Assert.Equal(255, AlphaAt(pixels, PlaceholderCustomers.Width, glow.X + first, glow.Y + 1 + row));
            Assert.Equal(0, AlphaAt(pixels, PlaceholderCustomers.Width, glow.X + 1 + first, glow.Y + 1 + row));
        }
    }

    [Fact]
    public void ParcelGlow_IsClosedUnderTheBoxWhileABustsGlowIsOpen()
    {
        _platform.Frame(0);

        // Glow ids: a customer sheet's glow comes right after it; the parcel is the fourth sheet.
        var glowWidth = CustomerSheet.PoseCount * (CustomerSheet.FrameWidth + 2);
        var lastRow = CustomerSheet.FrameHeight + 1;
        var parcelGlow = _platform.Backend.Textures[CustomerTexture + 7];
        var hoodieGlow = _platform.Backend.Textures[CustomerGlowTexture];
        Assert.Contains(Enumerable.Range(0, glowWidth), x => AlphaAt(parcelGlow, glowWidth, x, lastRow) == 255);
        Assert.DoesNotContain(Enumerable.Range(0, glowWidth), x => AlphaAt(hoodieGlow, glowWidth, x, lastRow) == 255);
    }

    [Fact]
    public void CustomerSheet_LoadedWithItsGlow()
    {
        _platform.Frame(0);

        Assert.Equal($"create {CustomerGlowTexture} {3 * (CustomerSheet.FrameWidth + 2)}x{CustomerSheet.FrameHeight + 2}", _platform.Backend.Calls[CustomerGlowTexture]);
    }

    [Fact]
    public void AtTheCounter_TheCustomerGlowsAndKeepsTheRevolverUntilClicked()
    {
        ComeToTheCounterWithSound();
        Idle(120);
        var glow = DrawnOn(LastFrame, CustomerGlowTexture, CustomerSheet.Glow(CustomerPose.Presenting));
        var bust = DrawnOn(LastFrame, CustomerTexture, CustomerSheet.Frame(CustomerPose.Presenting));

        HoldAt(CustomerMiddle, 1);
        var hovered = DrawnOn(LastFrame, CustomerGlowTexture, CustomerSheet.Glow(CustomerPose.Presenting));

        Assert.Equal(ShopPhase.Offering, Shop.Phase);
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Body));
        Assert.Null(Sprite(LastFrame, PlaceholderCustomers.Bubble));
        Assert.Equal(bust.Position, glow.Position);
        Assert.InRange(glow.Tint.A, 1, 254);
        Assert.Equal(255, hovered.Tint.A);
    }

    [Fact]
    public void ClickTheCustomer_TheRevolverFliesFromTheirHandsAndLandsWithAThud()
    {
        ComeToTheCounterWithSound();
        var played = PlayCount();

        ClickTheCustomer();
        var flying = Drawn(LastFrame, PlaceholderParts.Body).Position;
        Idle((int)(Shop.ThrowSeconds * 60) + 2);

        Assert.Equal(ShopPhase.Working, Shop.Phase);
        Assert.Equal([0.5, 0.35], PlayedPitches(played).Select(p => Math.Round(p, 3)));
        Assert.True(flying.Y < BenchLayout.GunPosition.Y - 50, $"Flying at {flying}.");
        Assert.Equal(BenchLayout.GunPosition, Drawn(LastFrame, PlaceholderParts.Body).Position);
        Assert.Null(Sprite(LastFrame, CustomerSheet.Glow(CustomerPose.Presenting)));
    }

    [Fact]
    public void FirstCustomer_ComesInWithTheDoorBellAndWalksToTheCounter()
    {
        _platform.Frame(0);
        _platform.AudioBackend.ReportReady(0, 1, 2);

        HoldAt(new Vector2(600, 40), (int)(Shop.WaitSeconds * 60) + 2);
        var entering = DrawnOn(LastFrame, CustomerTexture, CustomerSheet.Frame(CustomerPose.Presenting));
        HoldAt(new Vector2(600, 40), (int)(Shop.WalkSeconds * 60) + 2);
        var arrived = DrawnOn(LastFrame, CustomerTexture, CustomerSheet.Frame(CustomerPose.Presenting));

        Assert.Contains(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.Play && (int)c.Arguments[0] == DoorBellSound);
        Assert.InRange(entering.Position.X, Shop.CounterX + 50, Shop.DoorX);
        Assert.Equal(new Vector2(Shop.CounterX, Shop.CustomerBottom), arrived.Position);
        Assert.Contains($"create {RustTexture} {PlaceholderParts.Cylinder.Width}x{PlaceholderParts.Cylinder.Height}", _platform.Backend.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void AtTheCounter_TheRevolverLandsWithAThudAndTheBubbleShowsTheSymptom()
    {
        StartWithSound();

        Assert.Contains(0.35, PlayedPitches().Select(p => Math.Round(p, 3)));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Bubble));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Icon(Symptom.JammedCylinder)));
        Assert.Null(Sprite(LastFrame, PlaceholderCustomers.Face(Expression.Neutral)));
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
        HoldAt(BenchLayout.PinOut, 60);

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
    public void AtTheCounter_PresentsTheRevolverThenWaitsOnThePhone()
    {
        ArriveWithSound();
        var presenting = TextureOf(LastFrame, CustomerSheet.Frame(CustomerPose.Presenting));

        Idle((int)(Shop.PresentSeconds * 60) + 5);

        Assert.Equal(CustomerTexture, presenting);
        Assert.Equal(CustomerTexture, TextureOf(LastFrame, CustomerSheet.Frame(CustomerPose.Waiting)));
        Assert.NotNull(Sprite(LastFrame, PlaceholderCustomers.Bubble));
    }

    [Fact]
    public void Repaired_TheCustomerPresentsAgainWithAHandOut()
    {
        StartWithSound();
        RepairAndTest();
        Idle(1);

        Assert.Equal(CustomerTexture, TextureOf(LastFrame, CustomerSheet.Frame(CustomerPose.Presenting)));
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

        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);

        Assert.Equal(PartStop.End, Bench.Part(RevolverParts.BasePin).Stop);
        Assert.Equal(BenchLayout.PinOut, Drawn(LastFrame, PlaceholderParts.Pin).Position);
        Assert.Equal([0.55, 1.25], PlayedPitches(before), (a, b) => Math.Abs(a - b) < 1e-6);
    }

    [Fact]
    public void PushPinBackIn_SeatsItWithALowClick()
    {
        StartWithSound();
        Drag(BenchLayout.PinSeated, BenchLayout.PinOut);

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
        HoldAt(BenchLayout.CylinderOut, 3);

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
    public void RustyCylinderOut_DoesNotGoBackAndTheBrushFlashes()
    {
        StartWithSound();
        TakeApart();
        var before = PlayCount();

        // Carried across its seat: it does not drop in, and the brush flashes when it gets there.
        var framesBefore = _platform.Backend.Frames.Count;
        _platform.Frame(Step, PointerDown(BenchLayout.CylinderOut));
        HoldAt(BenchLayout.CylinderSeated - new Vector2(0, 20), 90);
        var flash = _platform.Backend.Frames.Skip(framesBefore).Max(frame => Drawn(frame, PlaceholderParts.BrushGlow).Tint.A);

        Assert.True(Bench.Part(RevolverParts.Cylinder).IsFree);
        Assert.Equal(Rust.InitialDirt, Rust.Dirt);
        Assert.Equal([0.4], PlayedPitches(before), (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.Equal(255, flash);
    }

    [Fact]
    public void LoosePartOnTheRevolver_IsDrawnOverIt()
    {
        StartWithSound();
        TakeApart();

        Drag(BenchLayout.CylinderOut, BenchLayout.GunPosition + new Vector2(60, 20));

        var instances = LastFrame.Instances;
        var body = Array.FindIndex(instances, i => i.SourceY == PlaceholderParts.Body.Y && i.SourceWidth == PlaceholderParts.Body.Width);
        var cylinder = Array.FindIndex(instances, i => i.SourceY == PlaceholderParts.Cylinder.Y && i.SourceWidth == PlaceholderParts.Cylinder.Width && i.SourceX == PlaceholderParts.Cylinder.X);
        Assert.True(cylinder > body, "The loose cylinder lies on top of the revolver.");
    }

    [Fact]
    public void CleanCylinderBroughtNearItsSeat_DropsInWithAClick()
    {
        StartWithSound();
        TakeApart();
        ScrubCylinderClean();
        Drag(BenchLayout.CylinderOut, new Vector2(150, 300));
        var before = PlayCount();

        _platform.Frame(Step, PointerDown(new Vector2(150, 300)));
        HoldAt(BenchLayout.CylinderSeated + new Vector2(4, 3), 60);

        Assert.False(Bench.Part(RevolverParts.Cylinder).IsFree);
        Assert.Equal(PartStop.Start, Bench.Part(RevolverParts.Cylinder).Stop);
        Assert.Equal(BenchLayout.CylinderSeated, Drawn(LastFrame, PlaceholderParts.Cylinder).Position);
        Assert.Contains(Math.Round(0.75 * 0.8, 3), PlayedPitches(before).Select(p => Math.Round(p, 3)));
    }

    [Fact]
    public void CleanCylinderAlongItsOldWayOut_DoesNotStick()
    {
        StartWithSound();
        TakeApart();
        ScrubCylinderClean();
        Drag(BenchLayout.CylinderOut, new Vector2(150, 300));

        Drag(new Vector2(150, 300), BenchLayout.CylinderSeated + new Vector2(0, 30));

        Assert.True(Bench.Part(RevolverParts.Cylinder).IsFree);
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
    public void PutBrushDown_StopsTheLoopAndItLiesWhereItWasPut()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));
        HoldAt(new Vector2(400, 200), 3);

        _platform.Frame(Step, PointerUp(new Vector2(400, 200)));
        Idle(60);

        Assert.False(Bench.Brush.IsHeld);
        Assert.Single(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.StopVoice);
        Assert.Contains(0.7, PlayedPitches().Select(p => Math.Round(p, 3)));
        Assert.Equal(new Vector2(400, 200), Drawn(LastFrame, PlaceholderParts.Brush).Position);
    }

    [Fact]
    public void BrushInHand_PartsDoNotLightUpOrMove()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(BenchLayout.BrushRest));

        HoldAt(BenchLayout.PinSeated, 3);
        HoldAt(BenchLayout.PinOut, 60);

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
        Assert.Equal(new Vector2(Shop.CounterX, Shop.CustomerBottom), DrawnOn(LastFrame, CustomerTexture, CustomerSheet.Frame(CustomerPose.Waiting)).Position);
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
        Assert.Equal(CustomerTexture, TextureOf(LastFrame, CustomerSheet.Frame(CustomerPose.Pleased)));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Body));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Hammer));

        // The tools stay on the bench where the player left them.
        Assert.Equal(Shop.Brush.Position, Drawn(LastFrame, PlaceholderParts.Brush).Position);
        Assert.Equal(BenchLayout.TrayMin, Drawn(LastFrame, PlaceholderParts.Tray).Position);
    }

    [Fact]
    public void Tools_BetweenCustomers_StayWhereTheyWereLeft()
    {
        StartWithSound();

        // Out of the way of every repair step: the bottom-left corner of the mat.
        var spot = new Vector2(100, 300);
        _platform.Frame(Step, PointerDown(BenchLayout.OilCanRest + new Vector2(5, 3)));
        HoldAt(spot, 2);
        _platform.Frame(Step, PointerUp(spot));
        var oilCan = Shop.OilCan;
        Assert.Equal(spot, oilCan.Position);
        RepairAndTest();
        HandBack();
        var brushLeftAt = Shop.Brush.Position;

        // The counter is empty: no revolver, but the tools and the tray are still there.
        for (var i = 0; i < 600 && Shop.Phase != ShopPhase.Waiting; i++)
        {
            Idle(1);
        }

        Assert.Equal(ShopPhase.Waiting, Shop.Phase);
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Body));
        Assert.Equal(spot, Drawn(LastFrame, PlaceholderParts.OilCan).Position);
        Assert.Equal(brushLeftAt, Drawn(LastFrame, PlaceholderParts.Brush).Position);
        Assert.Equal(BenchLayout.TrayMin, Drawn(LastFrame, PlaceholderParts.Tray).Position);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.OilCanGlow).Tint.A);

        // The next customer's bench gets the same tools where they lie.
        for (var i = 0; i < 600 && Shop.Phase != ShopPhase.Arriving; i++)
        {
            Idle(1);
        }

        Assert.Equal("robe", Bench.Order.Id);
        Assert.Same(oilCan, Bench.OilCan);
        Assert.Equal(spot, Bench.OilCan.Position);
        Assert.Equal(brushLeftAt, Bench.Brush.Position);
    }

    [Fact]
    public void AfterHandBack_TheCustomerLeavesWithTheBellAndTheNextOneComesIn()
    {
        StartWithSound();
        RepairAndTest();
        HandBack();

        Idle((int)((Shop.ThankSeconds + Shop.WalkSeconds + Shop.WaitSeconds) * 60) + 10);

        var bells = _platform.AudioBackend.AllCommands().Count(c => c.Kind == AudioCommandKind.Play && (int)c.Arguments[0] == DoorBellSound);
        Assert.Equal(3, bells);
        Assert.True(Shop.Phase is ShopPhase.Arriving or ShopPhase.Offering, $"The shop is {Shop.Phase}.");
        Assert.Equal("robe", Bench.Order.Id);
        Assert.Contains($"destroy {RustTexture}", _platform.Backend.Calls, StringComparer.Ordinal);

        // The robe customer has a sheet of its own: it comes in presenting, without the revolver yet.
        _ = DrawnOn(LastFrame, RobeCustomerTexture, CustomerSheet.Frame(CustomerPose.Presenting));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.Body));
    }

    [Fact]
    public void RobeCustomer_WhileWaiting_SitsUnderThePaintingWithTheBubble()
    {
        StartWithSound();
        RepairAndTest();
        HandBack();
        for (var i = 0; i < 1200 && !(Shop.Phase == ShopPhase.Offering && string.Equals(Shop.Bench?.Order.Id, "robe", StringComparison.Ordinal)); i++)
        {
            Idle(1);
        }

        ClickTheCustomer();
        for (var i = 0; i < 600 && Shop.Pose != CustomerPose.Waiting; i++)
        {
            Idle(1);
        }

        Assert.Equal(CustomerPose.Waiting, Shop.Pose);
        var seat = CustomerSeats.UnderThePainting;
        var sitting = DrawnOn(LastFrame, RobeCustomerTexture, CustomerSheet.Frame(CustomerPose.Waiting));
        Assert.Equal(seat.Bottom, sitting.Position);
        Assert.Equal(new Vector2(seat.Scale), sitting.Scale);

        // The bubble moves with the customer: up and to the right of the seated bust, not at the counter.
        var bubble = Drawn(LastFrame, PlaceholderCustomers.Bubble).Position;
        Assert.InRange(bubble.X, seat.Bottom.X, Shop.CounterX - Shop.CustomerHalfWidth);
        Assert.InRange(bubble.Y, 0, seat.Bottom.Y - (seat.Scale * CustomerSheet.FrameHeight / 2));
    }

    [Fact]
    public void DogWalker_WhileWaiting_StandsOutsideBetweenTheStreetAndTheRoom()
    {
        // The first order with the dog-walker coming in instead of hoodie, with the room and the street.
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true, firstLook: "dog-walker") };
        var game = new LudumDare60Game { WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        var away = PointerMove(new Vector2(600, 40));
        platform.Frame(0, Resize(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight, 1));
        platform.AudioBackend.ReportReady(0, 1, 2);
        for (var i = 0; i < 600 && game.Shop?.Phase != ShopPhase.Offering; i++)
        {
            platform.Frame(Step, away);
        }

        platform.Frame(Step, PointerDown(CustomerMiddle));
        platform.Frame(Step, PointerUp(CustomerMiddle));
        for (var i = 0; i < 600 && game.Shop!.Pose != CustomerPose.Waiting; i++)
        {
            platform.Frame(Step, away);
        }

        Assert.Equal(CustomerPose.Waiting, game.Shop!.Pose);
        var frame = platform.Backend.Frames[^1];
        var spot = CustomerSeats.OutsideTheWindow;
        var outside = DrawnOn(frame, DogWalkerTextureWithBackground, CustomerSheet.Frame(CustomerPose.Waiting));
        Assert.Equal(spot.Bottom, outside.Position);
        Assert.Equal(new Vector2(spot.Scale), outside.Scale);
        Assert.Equal(LudumDare60Game.OutsideTint, outside.Tint);

        // Street, then the customer, then the room over them; the bubble stays over the room.
        var textures = frame.Decode().Where(c => c.Kind == RenderCommandKind.DrawSprites).Select(c => c.Arguments[0]).ToList();
        Assert.Equal([StreetTexture, DogWalkerTextureWithBackground, RoomTexture], textures.Take(3));
        Assert.Contains(CustomersTexture, textures.Skip(3));
        Assert.NotNull(Sprite(frame, PlaceholderCustomers.Bubble));
    }

    [Fact]
    public void Parcel_ComingIn_IsDrawnOnTheCounterAndNotAtTheDoor()
    {
        // The first order with a parcel coming in instead of hoodie, with the room and the street.
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true, firstLook: "parcel") };
        var game = new LudumDare60Game { WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        var away = PointerMove(new Vector2(600, 40));
        platform.Frame(0, Resize(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight, 1));
        platform.AudioBackend.ReportReady(0, 1, 2);
        for (var i = 0; i < 600 && game.Shop?.Phase != ShopPhase.Arriving; i++)
        {
            platform.Frame(Step, away);
        }

        platform.Frame(Step, away);

        // Already at its place while a customer would still be walking in from the door.
        Assert.Equal(ShopPhase.Arriving, game.Shop!.Phase);
        var place = CustomerSeats.OnTheCounter;
        var parcel = DrawnOn(platform.Backend.Frames[^1], ParcelTextureWithBackground, CustomerSheet.Frame(CustomerPose.Presenting));
        Assert.Equal(place.Bottom, parcel.Position);
        Assert.Equal(new Vector2(place.Scale), parcel.Scale);
        Assert.Equal(Color.White, parcel.Tint);
    }

    [Fact]
    public void FirstOrder_PartsItKeepsFixed_AreDrawnInTheirSeatsWithoutGlow()
    {
        StartWithSound();

        Assert.Equal(BenchLayout.GripPanelSeated, Drawn(LastFrame, PlaceholderParts.GripPanel).Position);
        Assert.Equal(BenchLayout.MainspringSeated, Drawn(LastFrame, PlaceholderParts.Mainspring).Position);
        Assert.Equal(BenchLayout.GripScrewSeated, Drawn(LastFrame, PlaceholderParts.Screw).Position);
        Assert.Null(Sprite(LastFrame, PlaceholderParts.GripPanelGlow));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.ScrewGlow));
        Assert.Null(Sprite(LastFrame, PlaceholderParts.MainspringBroken));
    }

    [Fact]
    public void FirstOrder_TheGripPanelIsDrawnOverTheMainspringAndTheScrewOverThePanel()
    {
        StartWithSound();

        var instances = LastFrame.Instances;
        int drawnAt(IntRect sprite) => Array.FindIndex(instances, i => i.SourceX == sprite.X && i.SourceY == sprite.Y && i.SourceWidth == sprite.Width);
        var body = drawnAt(PlaceholderParts.Body);
        var spring = drawnAt(PlaceholderParts.Mainspring);
        var panel = drawnAt(PlaceholderParts.GripPanel);
        var screw = drawnAt(PlaceholderParts.Screw);
        Assert.True(body < spring && spring < panel && panel < screw, $"Body {body}, spring {spring}, panel {panel}, screw {screw}.");
    }

    [Fact]
    public void FirstOrder_TheTrayAndTheOilCanLieOnTheMatUnlit()
    {
        StartWithSound();

        Assert.Equal(BenchLayout.TrayMin, Drawn(LastFrame, PlaceholderParts.Tray).Position);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.TrayGlow).Tint.A);
        Assert.Equal(BenchLayout.OilCanRest, Drawn(LastFrame, PlaceholderParts.OilCan).Position);
        Assert.Equal(0, Drawn(LastFrame, PlaceholderParts.OilCanGlow).Tint.A);
    }

    [Fact]
    public void PartsTexture_GripPanelScrewAndMainspringAreOpaqueWhereTheyGrab()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[0];
        var panel = PlaceholderParts.GripPanel;
        var spring = PlaceholderParts.Mainspring;
        var screw = PlaceholderParts.Screw;
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, panel.X + (int)PlaceholderParts.GripPanelOrigin.X, panel.Y + (int)PlaceholderParts.GripPanelOrigin.Y));
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, spring.X + (int)PlaceholderParts.MainspringOrigin.X, spring.Y + (int)PlaceholderParts.MainspringOrigin.Y));
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, screw.X + 3, screw.Y + 3));
        Assert.Equal(0, AlphaAt(pixels, PlaceholderParts.Width, screw.X, screw.Y));

        // Under the panel the body shows the grip's channel, not a hole.
        var underPanel = PlaceholderParts.GripPanelInBody;
        Assert.Equal(255, AlphaAt(pixels, PlaceholderParts.Width, (int)underPanel.X, (int)underPanel.Y));
    }

    [Fact]
    public void FirstFrame_NoBackgroundFile_DrawsOnlyTheMatColor()
    {
        _platform.Frame(0);

        Assert.False(_game.HasBackground);
        Assert.False(_game.HasStreet);
        Assert.Equal(s_startTextures, _platform.Backend.Textures.Count);
    }

    [Fact]
    public void FirstFrame_BackgroundFileShipped_CreatesScreenSizedTexture()
    {
        var (platform, game) = StartWithShippedAssets();

        platform.Frame(0);

        Assert.True(game.HasBackground);
        Assert.True(game.HasStreet);
        Assert.Contains($"create 2 {LudumDare60Game.VirtualWidth}x{LudumDare60Game.VirtualHeight}", platform.Backend.Calls, StringComparer.Ordinal);
        Assert.Contains($"create 3 {LudumDare60Game.VirtualWidth}x{LudumDare60Game.VirtualHeight}", platform.Backend.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void BackgroundTexture_WindowIsSeeThroughAndTheMatIsOpaque()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);

        // A window pane, the door glass and the middle of the mat.
        var room = platform.Backend.Textures[2];
        Assert.Equal(0, AlphaAt(room, LudumDare60Game.VirtualWidth, 300, 40));
        Assert.Equal(0, AlphaAt(room, LudumDare60Game.VirtualWidth, 510, 60));
        Assert.Equal(255, AlphaAt(room, LudumDare60Game.VirtualWidth, 300, 250));
        Assert.Equal(255, AlphaAt(platform.Backend.Textures[3], LudumDare60Game.VirtualWidth, 300, 40));
    }

    [Fact]
    public void Render_BackgroundLoaded_DrawsTheStreetFirstThenTheRoomAtTheOrigin()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);

        var frame = Assert.Single(platform.Backend.Frames);
        Assert.Equal(LudumDare60Game.StreetOffset, frame.Instances[0].Position);
        var room = frame.Instances[1];
        Assert.Equal(Vector2.Zero, room.Position);
        Assert.Equal(LudumDare60Game.VirtualWidth, room.SourceWidth);
        Assert.Equal(LudumDare60Game.VirtualHeight, room.SourceHeight);
        var commands = frame.Decode();
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [3, 0, 1]), commands[2]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [2, 1, 1]), commands[3]);
    }

    [Fact]
    public void Frames_BackgroundLoaded_CreatesItsTextureOnce()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);
        platform.Frame(0.01);

        Assert.Equal(s_startTextures + 2, platform.Backend.Textures.Count);

        // The street and the room, once each per frame.
        Assert.Equal(2, platform.Backend.Frames[1].Instances.Count(i => i.SourceWidth == LudumDare60Game.VirtualWidth));
    }

    private static (ManualPlatform Platform, LudumDare60Game Game) StartOnTitle()
    {
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true) };
        var game = new LudumDare60Game();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        return (platform, game);
    }

    private static (ManualPlatform Platform, LudumDare60Game Game) StartWithShippedAssets()
    {
        var platform = new ManualPlatform { Assets = new ShippedAssets(withBackground: true) };
        var game = new LudumDare60Game { WaitsForPlayer = false };
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        return (platform, game);
    }

    /// <summary>
    /// Reads the files of <c>wwwroot/assets/</c>, copied next to the test assembly.
    /// </summary>
    /// <param name="withBackground">Whether the room and the street load. Without them, their reads never finish: a
    /// failed read would stop the game.</param>
    /// <param name="firstLook">When set, the first order's customer has this look instead of hoodie.</param>
    /// <param name="failing">When set, the read of this file fails.</param>
    /// <param name="held">When set, the read of this file finishes only on <see cref="Release"/>.</param>
    private sealed class ShippedAssets(bool withBackground, string? firstLook = null, string? failing = null, string? held = null) : IAssetSource
    {
        private readonly TaskCompletionSource<byte[]> _held = new();

        /// <summary>
        /// Finishes the read of the held file.
        /// </summary>
        public void Release() => _held.SetResult(Read(held!));

        // Reads finish at once, so a file requested in Initialize is loaded in the first frame.
        public Task<byte[]> ReadAsync(string path)
        {
            if (string.Equals(path, failing, StringComparison.Ordinal))
            {
                return Task.FromException<byte[]>(new FileNotFoundException(path));
            }

            if (string.Equals(path, held, StringComparison.Ordinal))
            {
                return _held.Task;
            }

            if (!withBackground && (string.Equals(path, AssetPaths.Bench, StringComparison.Ordinal) || string.Equals(path, AssetPaths.Street, StringComparison.Ordinal)))
            {
                return new TaskCompletionSource<byte[]>().Task;
            }

            return Task.FromResult(firstLook is not null && string.Equals(path, AssetPaths.Orders, StringComparison.Ordinal) ? FirstLookAs(firstLook) : Read(path));
        }

        private static byte[] FirstLookAs(string look)
        {
            var json = Encoding.UTF8.GetString(Read(AssetPaths.Orders));
            var hoodie = "\"look\": \"hoodie\"";
            var at = json.IndexOf(hoodie, StringComparison.Ordinal);
            Assert.True(at >= 0, "The first order's look is not hoodie any more.");
            return Encoding.UTF8.GetBytes(string.Concat(json.AsSpan(0, at), $"\"look\": \"{look}\"", json.AsSpan(at + hoodie.Length)));
        }

        private static byte[] Read(string path) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", path));
    }
}
