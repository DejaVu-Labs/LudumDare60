using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Game.Tests;

public sealed class LudumDare60GameTests
{
    private readonly ManualPlatform _platform = new();
    private readonly LudumDare60Game _game = new();

    public LudumDare60GameTests()
        => _ = new GameEngine(LudumDare60Game.VirtualWidth, LudumDare60Game.VirtualHeight).RunAsync(_game, _platform);

    private static RawInputEvent Resize(float width, float height, float ratio)
        => new(RawInputEventKind.Resize, 0, 0, 0, 0, width, height, ratio);

    private static RawInputEvent PointerDown(float x, float y)
        => new(RawInputEventKind.PointerDown, 0, (ushort)PointerType.Mouse, 1, 0, x, y, 0);

    [Fact]
    public void FirstFrame_CreatesPlaceholderTextureAndDrawsOnMat()
    {
        _platform.Frame(0);

        Assert.Equal("create 0 32x32", _platform.Backend.Calls[0]);
        var frame = Assert.Single(_platform.Backend.Frames);
        var commands = frame.Decode();
        Assert.Equal(RenderCommandKind.Clear, commands[1].Kind);
        Assert.Equal(BitConverter.ToInt32([0x0b, 0x4f, 0x45, 0xff]), commands[1].Arguments[0]);
        Assert.Equal(3, frame.Instances.Length);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [0, 0, 3]), commands[2]);
    }

    [Fact]
    public void PlaceholderTexture_HasTransparentCornersAndOpaqueInside()
    {
        _platform.Frame(0);

        var pixels = _platform.Backend.Textures[0];
        Assert.Equal(0, pixels[3]);
        Assert.Equal(255, pixels[(((16 * 32) + 16) * 4) + 3]);
    }

    [Fact]
    public void PointerDown_MovesSpriteToVirtualPosition()
    {
        // 1400x800 at ratio 1: scale 2 with a 60 px bar left and a 40 px bar on top.
        _platform.Frame(0, Resize(1400, 800, 1), PointerDown(60 + 200, 40 + 100));

        Assert.Equal(new Vector2(100, 50), _game.PointerPosition);
        Assert.Equal(new Vector2(100, 50), _platform.Backend.Frames[0].Instances[^1].Position);
    }

    [Fact]
    public void PrimaryButtonDown_DoublesTheCursorSprite()
    {
        _platform.Frame(0, PointerDown(100, 100));

        Assert.True(_game.IsHolding);
        Assert.Equal(new Vector2(2, 2), _platform.Backend.Frames[0].Instances[^1].Scale);
    }

    [Fact]
    public void Frames_CreateTheTextureOnce()
    {
        _platform.Frame(0);
        _platform.Frame(0.01);

        Assert.Single(_platform.Backend.Textures);
        Assert.Equal(2, _platform.Backend.Frames.Count);
    }

    [Fact]
    public void FirstFrame_NoBackgroundFile_DrawsMatAndPlaceholdersOnly()
    {
        _platform.Frame(0);

        Assert.False(_game.HasBackground);
        Assert.Single(_platform.Backend.Textures);
    }

    [Fact]
    public void FirstFrame_BackgroundFileShipped_CreatesScreenSizedTexture()
    {
        var (platform, game) = StartWithShippedAssets();

        platform.Frame(0);

        Assert.True(game.HasBackground);
        Assert.Contains($"create 1 {LudumDare60Game.VirtualWidth}x{LudumDare60Game.VirtualHeight}", platform.Backend.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void Render_BackgroundLoaded_DrawsItFirstAtTheOrigin()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);

        var frame = Assert.Single(platform.Backend.Frames);
        Assert.Equal(4, frame.Instances.Length);
        var background = frame.Instances[0];
        Assert.Equal(Vector2.Zero, background.Position);
        Assert.Equal(LudumDare60Game.VirtualWidth, background.SourceWidth);
        Assert.Equal(LudumDare60Game.VirtualHeight, background.SourceHeight);
        var commands = frame.Decode();
        Assert.Equal(RenderCommandKind.Clear, commands[1].Kind);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [1, 0, 1]), commands[2]);
        Assert.Equal(new RenderCommand(RenderCommandKind.DrawSprites, [0, 1, 3]), commands[3]);
    }

    [Fact]
    public void Frames_BackgroundLoaded_CreatesItsTextureOnce()
    {
        var (platform, _) = StartWithShippedAssets();

        platform.Frame(0);
        platform.Frame(0.01);

        Assert.Equal(2, platform.Backend.Textures.Count);
        Assert.Equal(4, platform.Backend.Frames[1].Instances.Length);
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
