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
        Assert.Equal(BitConverter.ToInt32([0x24, 0x5c, 0x48, 0xff]), commands[1].Arguments[0]);
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
    public void Frames_CreateTheTextureOnce()
    {
        _platform.Frame(0);
        _platform.Frame(0.01);

        Assert.Single(_platform.Backend.Textures);
        Assert.Equal(2, _platform.Backend.Frames.Count);
    }
}
