using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests;

public sealed class LudumDare60GameTests
{
    private const double Step = 1.0 / 60;

    private readonly ManualPlatform _platform = new();
    private readonly LudumDare60Game _game = new();

    public LudumDare60GameTests()
        => _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(_game, _platform);

    private static RawInputEvent Resize(float width, float height, float ratio)
        => new(RawInputEventKind.Resize, 0, 0, 0, 0, width, height, ratio);

    private static RawInputEvent PointerDown(Vector2 at)
        => new(RawInputEventKind.PointerDown, 0, (ushort)PointerType.Mouse, 1, 0, at.X, at.Y, 0);

    private static RawInputEvent PointerMove(Vector2 at)
        => new(RawInputEventKind.PointerMove, 0, (ushort)PointerType.Mouse, 1, 0, at.X, at.Y, 0);

    private static RawInputEvent PointerUp(Vector2 at)
        => new(RawInputEventKind.PointerUp, 0, (ushort)PointerType.Mouse, 1, 0, at.X, at.Y, 0);

    /// <summary>
    /// Starts at one logical pixel per virtual pixel, with the click sound decoded.
    /// </summary>
    private void StartWithSound()
    {
        _platform.Frame(0, Resize(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight, 1));
        _platform.AudioBackend.ReportReady(0, 1);
        _platform.Frame(Step);
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

    private static int AlphaAt(byte[] pixels, int x, int y) => pixels[(((y * PlaceholderParts.Width) + x) * 4) + 3];

    private List<double> PlayedPitches()
        => [.. _platform.AudioBackend.AllCommands().Where(c => c.Kind == AudioCommandKind.Play).Select(c => c.Arguments[3])];

    [Fact]
    public void FirstFrame_CreatesThePartsTextureAndDrawsOnMat()
    {
        _platform.Frame(0);

        Assert.Equal("create 0 64x80", _platform.Backend.Calls[0]);
        var frame = Assert.Single(_platform.Backend.Frames);
        var commands = frame.Decode();
        Assert.Equal(RenderCommandKind.Clear, commands[1].Kind);
        Assert.Equal(BitConverter.ToInt32([0x0b, 0x4f, 0x45, 0xff]), commands[1].Arguments[0]);
        Assert.Equal(8, frame.Instances.Length);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [0, 0, 5]), commands[2]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [1, 5, 1]), commands[3]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [0, 6, 2]), commands[4]);
        Assert.Equal("create 1 24x18", _platform.Backend.Calls[1]);
    }

    [Fact]
    public void FirstFrame_CreatesTheClickAndTheScrubLoopAsWav()
    {
        _platform.Frame(0);

        Assert.Equal(2, _platform.AudioBackend.Sounds.Count);
        var click = _platform.AudioBackend.Sounds[0];
        Assert.Equal("RIFF"u8.ToArray(), click[..4]);
        Assert.Equal("WAVE"u8.ToArray(), click[8..12]);
        Assert.Equal(44 + (PlaceholderSounds.SampleRate * 60 / 1000 * 2), click.Length);
    }

    [Fact]
    public void PartsTexture_PinHeadIsOpaqueAndItsGlowSurroundsIt()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[0];
        var pin = PlaceholderParts.Pin;
        var glow = PlaceholderParts.PinGlow;
        Assert.Equal(255, AlphaAt(pixels, pin.X + 2, pin.Y + 3));
        Assert.Equal(0, AlphaAt(pixels, pin.X + 20, pin.Y));
        Assert.Equal(255, AlphaAt(pixels, glow.X, glow.Y + 1));
        Assert.Equal(0, AlphaAt(pixels, glow.X + 3, glow.Y + 4));
    }

    [Fact]
    public void Frames_CreateTheTextureAndSoundOnce()
    {
        _platform.Frame(0);
        _platform.Frame(0.01);

        Assert.Equal(2, _platform.Backend.Textures.Count);
        Assert.Equal(2, _platform.AudioBackend.Sounds.Count);
        Assert.Equal(2, _platform.Backend.Frames.Count);
    }

    [Fact]
    public void Render_PinSeated_DrawsGlowPinThenFrame()
    {
        _platform.Frame(0);

        var instances = _platform.Backend.Frames[0].Instances;
        Assert.Equal(LudumDare60Game.PinSeated, instances[0].Position);
        Assert.Equal(PlaceholderParts.PinGlow.Height, instances[0].SourceHeight);
        Assert.Equal(LudumDare60Game.PinSeated, instances[1].Position);
        Assert.Equal(PlaceholderParts.Pin.Width, instances[1].SourceWidth);
        Assert.Equal(LudumDare60Game.FramePosition, instances[2].Position);
    }

    [Fact]
    public void PointerOverPin_GlowsAtFullStrength()
    {
        StartWithSound();

        _platform.Frame(Step, PointerMove(LudumDare60Game.PinSeated + new Vector2(0, 2)));

        Assert.True(_game.BasePin.IsHovered);
        Assert.Equal(255, _platform.Backend.Frames[^1].Instances[0].Tint.A);
    }

    [Fact]
    public void PointerAwayFromPin_GlowsFaintly()
    {
        StartWithSound();

        _platform.Frame(Step, PointerMove(new Vector2(500, 300)));

        Assert.False(_game.BasePin.IsHovered);
        Assert.InRange(_platform.Backend.Frames[^1].Instances[0].Tint.A, 1, 254);
    }

    [Fact]
    public void DragPinOut_SnapsItFreeWithClicks()
    {
        StartWithSound();

        _platform.Frame(Step, PointerDown(LudumDare60Game.PinSeated));
        HoldAt(LudumDare60Game.PinOut - new Vector2(20, 0), 90);

        Assert.Equal(PartStop.End, _game.BasePin.Stop);
        Assert.False(_game.BasePin.IsHeld);
        Assert.Equal(LudumDare60Game.PinOut, _platform.Backend.Frames[^1].Instances[1].Position);
        Assert.Equal([0.55, 1.25], PlayedPitches(), (a, b) => Math.Abs(a - b) < 1e-6);
    }

    [Fact]
    public void PushPinBackIn_SeatsItWithALowClick()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(LudumDare60Game.PinSeated));
        HoldAt(LudumDare60Game.PinOut - new Vector2(20, 0), 90);
        _platform.Frame(Step, PointerUp(LudumDare60Game.PinOut - new Vector2(20, 0)));

        _platform.Frame(Step, PointerDown(LudumDare60Game.PinOut));
        HoldAt(LudumDare60Game.PinSeated + new Vector2(20, 0), 90);

        Assert.Equal(PartStop.Start, _game.BasePin.Stop);
        Assert.Equal(LudumDare60Game.PinSeated, _platform.Backend.Frames[^1].Instances[1].Position);
        Assert.Equal(0.75, PlayedPitches()[^1], 1e-6);
    }

    [Fact]
    public void PartsTexture_FrameWindowIsOpen()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[0];
        var inWindow = PlaceholderParts.CylinderInFrame + new Vector2(5, 5);
        Assert.Equal(0, AlphaAt(pixels, (int)inWindow.X, (int)inWindow.Y));
        Assert.Equal(255, AlphaAt(pixels, 5, 5));
    }

    [Fact]
    public void Render_Assembled_DrawsTheCylinderInTheWindowWithoutGlow()
    {
        _platform.Frame(0);

        var instances = _platform.Backend.Frames[0].Instances;
        Assert.Equal(LudumDare60Game.CylinderSeated, instances[4].Position);
        Assert.Equal(PlaceholderParts.Cylinder.Width, instances[4].SourceWidth);
        Assert.Equal(0, instances[3].Tint.A);
        Assert.Equal(LudumDare60Game.PinSeated.Y, LudumDare60Game.CylinderSeated.Y);
    }

    [Fact]
    public void PressOnCylinderFirst_RefusesWithADullTickAndFlashesThePin()
    {
        StartWithSound();

        _platform.Frame(Step, PointerDown(LudumDare60Game.CylinderSeated));
        HoldAt(LudumDare60Game.CylinderOut + new Vector2(0, 20), 3);

        Assert.Equal(PartStop.Start, _game.Cylinder.Stop);
        Assert.False(_game.Cylinder.IsHeld);
        Assert.Equal([0.4], PlayedPitches(), (a, b) => Math.Abs(a - b) < 1e-6);
        Assert.Equal(255, _platform.Backend.Frames[^4].Instances[0].Tint.A);
    }

    [Fact]
    public void TakeApartAndPutBack_PinThenCylinderAndBackInReverse()
    {
        StartWithSound();
        Drag(LudumDare60Game.PinSeated, LudumDare60Game.PinOut - new Vector2(20, 0));
        Drag(LudumDare60Game.CylinderSeated, LudumDare60Game.CylinderOut + new Vector2(0, 20));

        Assert.True(_game.Gun.AllAt(PartStop.End));
        Assert.Equal(LudumDare60Game.CylinderOut, _platform.Backend.Frames[^1].Instances[4].Position);
        ScrubCylinderClean();

        Drag(LudumDare60Game.CylinderOut, LudumDare60Game.CylinderSeated - new Vector2(0, 20));
        Drag(LudumDare60Game.PinOut, LudumDare60Game.PinSeated + new Vector2(20, 0));

        Assert.True(_game.Gun.AllAt(PartStop.Start));
        var cylinderClicks = PlayedPitches().Where(p => Math.Abs(p - 1.0) < 1e-6 || Math.Abs(p - 0.6) < 1e-6);
        Assert.Equal([1.0, 0.6], cylinderClicks, (a, b) => Math.Abs(a - b) < 1e-6);
    }

    [Fact]
    public void RustyCylinderOut_BareHands_NeitherDragNorScrubButFlashTheBrush()
    {
        StartWithSound();
        TakeApart();

        Drag(LudumDare60Game.CylinderOut, LudumDare60Game.CylinderSeated - new Vector2(0, 20));

        Assert.True(_game.Gun.IsLocked(LudumDare60Game.CylinderIndex));
        Assert.Equal(PartStop.End, _game.Cylinder.Stop);
        Assert.Equal(_game.CylinderRust.InitialDirt, _game.CylinderRust.Dirt);
        Assert.Equal(0.4, PlayedPitches()[^1], 1e-6);
    }

    [Fact]
    public void RustyCylinderOut_PressOnIt_FlashesTheBrush()
    {
        StartWithSound();
        TakeApart();

        _platform.Frame(Step, PointerDown(LudumDare60Game.CylinderOut));

        Assert.Equal(255, BrushGlow(_platform.Backend.Frames[^1]).Tint.A);
    }

    [Fact]
    public void RustyCylinderSeated_IsDraggedNotScrubbed()
    {
        StartWithSound();
        Drag(LudumDare60Game.PinSeated, LudumDare60Game.PinOut - new Vector2(20, 0));

        Drag(LudumDare60Game.CylinderSeated, LudumDare60Game.CylinderOut + new Vector2(0, 20));

        Assert.Equal(PartStop.End, _game.Cylinder.Stop);
        Assert.Equal(_game.CylinderRust.InitialDirt, _game.CylinderRust.Dirt);
    }

    [Fact]
    public void Render_BrushAtItsPlace_DrawnLastAtBrushRest()
    {
        _platform.Frame(0);

        var instances = _platform.Backend.Frames[0].Instances;
        Assert.Equal(LudumDare60Game.BrushRest, instances[^1].Position);
        Assert.Equal(PlaceholderParts.Brush.Width, instances[^1].SourceWidth);
        Assert.Equal(0, BrushGlow(_platform.Backend.Frames[0]).Tint.A);
    }

    [Fact]
    public void PickUpBrush_FollowsThePointerAndStartsASilentScrubLoop()
    {
        StartWithSound();

        _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest + new Vector2(10, 1)));
        HoldAt(new Vector2(400, 200), 2);

        Assert.True(_game.Brush.IsHeld);
        Assert.Equal(new Vector2(400, 200), _platform.Backend.Frames[^1].Instances[^1].Position);
        var loop = Assert.Single(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.PlayLoop);
        Assert.Equal(1, loop.Arguments[1]);
        Assert.All(VoiceVolumes(), volume => Assert.Equal(0, volume));
    }

    [Fact]
    public void BrushOverRustyCylinder_ScrubsUploadsRowsAndRasps()
    {
        StartWithSound();
        TakeApart();

        _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest));
        RubAcrossCylinder(-3);

        Assert.True(_game.CylinderRust.Dirt < _game.CylinderRust.InitialDirt);
        Assert.Contains(_platform.Backend.Calls, call => call.StartsWith("update 1 rows ", StringComparison.Ordinal));
        Assert.Contains(VoiceVolumes(), volume => volume > 0.3);
        Assert.Equal(PartStop.End, _game.Cylinder.Stop);
    }

    [Fact]
    public void BrushMovedOverTheMat_ErasesNothingAndStaysSilent()
    {
        StartWithSound();
        TakeApart();

        _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest));
        for (var i = 0; i < 20; i++)
        {
            _platform.Frame(Step, PointerMove(new Vector2(380 + (i % 2 * 30), 200)));
        }

        Assert.Equal(_game.CylinderRust.InitialDirt, _game.CylinderRust.Dirt);
        Assert.All(VoiceVolumes(), volume => Assert.Equal(0, volume));
    }

    [Fact]
    public void BrushHeldStill_OnTheCylinder_FallsSilent()
    {
        StartWithSound();
        TakeApart();
        _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest));
        RubAcrossCylinder(0);

        HoldAt(LudumDare60Game.CylinderOut, 30);

        Assert.True(_game.IsBrushOnCylinder);
        Assert.InRange(VoiceVolumes()[^1], 0, 0.05);
    }

    [Fact]
    public void PutBrushDown_StopsTheLoopAndItSlidesHome()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest));
        HoldAt(new Vector2(400, 200), 3);

        _platform.Frame(Step, PointerUp(new Vector2(400, 200)));
        Idle(60);

        Assert.False(_game.Brush.IsHeld);
        Assert.True(_game.Brush.IsResting);
        Assert.Single(_platform.AudioBackend.AllCommands(), c => c.Kind == AudioCommandKind.StopVoice);
        Assert.Contains(0.7, PlayedPitches().Select(p => Math.Round(p, 3)));
        Assert.Equal(LudumDare60Game.BrushRest, _platform.Backend.Frames[^1].Instances[^1].Position);
    }

    [Fact]
    public void BrushInHand_PartsDoNotLightUpOrMove()
    {
        StartWithSound();
        _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest));

        HoldAt(LudumDare60Game.PinSeated, 3);
        HoldAt(LudumDare60Game.PinOut - new Vector2(20, 0), 60);

        Assert.False(_game.BasePin.IsHovered);
        Assert.Equal(PartStop.Start, _game.BasePin.Stop);
    }

    [Fact]
    public void ScrubCylinderClean_ChimesFadesTheRustAndUnlocksIt()
    {
        StartWithSound();
        TakeApart();

        ScrubCylinderClean();
        Idle(40);

        Assert.True(_game.CylinderRust.IsClean);
        Assert.False(_game.Gun.IsLocked(LudumDare60Game.CylinderIndex));
        Assert.Contains(2.0, PlayedPitches());
        Assert.Equal(7, _platform.Backend.Frames[^1].Instances.Length);
    }

    private static SpriteInstance BrushGlow(SubmittedFrame frame) => frame.Instances[^2];

    private List<double> VoiceVolumes()
        => [.. _platform.AudioBackend.AllCommands().Where(c => c.Kind == AudioCommandKind.SetVoice).Select(c => c.Arguments[1])];

    /// <summary>
    /// Frames with the pointer up, far from everything.
    /// </summary>
    private void Idle(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            _platform.Frame(Step, PointerMove(new Vector2(600, 40)));
        }
    }

    private void TakeApart()
    {
        Drag(LudumDare60Game.PinSeated, LudumDare60Game.PinOut - new Vector2(20, 0));
        Drag(LudumDare60Game.CylinderSeated, LudumDare60Game.CylinderOut + new Vector2(0, 20));
    }

    /// <summary>
    /// With the brush in the hand, rubs it back and forth across the cylinder at <paramref name="y"/> pixels from its
    /// middle, about 180 px/s.
    /// </summary>
    private void RubAcrossCylinder(float y)
    {
        var center = LudumDare60Game.CylinderOut;
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
        var center = LudumDare60Game.CylinderOut;
        for (var pass = 0; pass < 4 && !_game.CylinderRust.IsClean; pass++)
        {
            _platform.Frame(Step, PointerDown(LudumDare60Game.BrushRest));
            for (var y = -9; y <= 9 && !_game.CylinderRust.IsClean; y += 2)
            {
                _platform.Frame(Step, PointerMove(center + new Vector2(-13, y)));
                _platform.Frame(Step, PointerMove(center + new Vector2(13, y)));
            }

            _platform.Frame(Step, PointerUp(center));
            Idle(30);
        }

        Assert.True(_game.CylinderRust.IsClean);
    }

    /// <summary>
    /// Presses at <paramref name="from"/>, holds at <paramref name="to"/> for 90 frames and lets go.
    /// </summary>
    private void Drag(Vector2 from, Vector2 to)
    {
        _platform.Frame(Step, PointerDown(from));
        HoldAt(to, 90);
        _platform.Frame(Step, PointerUp(to));
    }

    [Fact]
    public void PullPinLessThanBreakaway_KeepsItSeatedAndSilent()
    {
        StartWithSound();

        _platform.Frame(Step, PointerDown(LudumDare60Game.PinSeated));
        HoldAt(LudumDare60Game.PinSeated - new Vector2(4, 0), 30);

        Assert.Equal(PartStop.Start, _game.BasePin.Stop);
        Assert.True(_game.BasePin.IsHeld);
        Assert.Empty(PlayedPitches());
    }

    [Fact]
    public void FirstFrame_NoBackgroundFile_DrawsMatAndPartsOnly()
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
        Assert.Equal(9, frame.Instances.Length);
        var background = frame.Instances[0];
        Assert.Equal(Vector2.Zero, background.Position);
        Assert.Equal(LudumDare60Game.VirtualWidth, background.SourceWidth);
        Assert.Equal(LudumDare60Game.VirtualHeight, background.SourceHeight);
        var commands = frame.Decode();
        Assert.Equal(RenderCommandKind.Clear, commands[1].Kind);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [2, 0, 1]), commands[2]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [0, 1, 5]), commands[3]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [1, 6, 1]), commands[4]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [0, 7, 2]), commands[5]);
    }

    [Fact]
    public void Frames_BackgroundLoaded_CreatesItsTextureOnce()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);
        platform.Frame(0.01);

        Assert.Equal(3, platform.Backend.Textures.Count);
        Assert.Equal(9, platform.Backend.Frames[1].Instances.Length);
    }

    private static (ManualPlatform Platform, LudumDare60Game Game) StartWithShippedAssets()
    {
        var platform = new ManualPlatform { Assets = new ShippedAssets() };
        var game = new LudumDare60Game();
        _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(game, platform);
        return (platform, game);
    }

    /// <summary>
    /// Reads the files of <c>wwwroot/assets/</c>, copied next to the test assembly.
    /// </summary>
    private sealed class ShippedAssets : IAssetSource
    {
        // Reads finish at once, so a file requested in Initialize is loaded in the first frame.
        public Task<byte[]> ReadAsync(string path) => Task.FromResult(Read(path));

        private static byte[] Read(string path) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", path));
    }
}
