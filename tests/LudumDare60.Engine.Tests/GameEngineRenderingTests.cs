using System.Numerics;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Engine.Tests;

public sealed class GameEngineRenderingTests
{
    private readonly GameEngine _engine = new(640, 360);
    private readonly RecordingGame _game = new();

    private static RawInputEvent Resize(float width, float height, float ratio)
        => new(RawInputEventKind.Resize, 0, 0, 0, 0, width, height, ratio);

    [Theory]
    [InlineData(0, 360)]
    [InlineData(640, 0)]
    public void Constructor_SizeBelowOne_Throws(int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new GameEngine(width, height));

    [Fact]
    public async Task Initialize_GetsGraphicsOnPlatformBackend()
    {
        var platform = new ScriptedPlatform(0.01);
        _game.OnUpdate = static graphics => graphics.CreateTexture(GraphicsTests.SolidImage(1, 1));

        await _engine.RunAsync(_game, platform);

        Assert.NotNull(_game.Graphics);
        Assert.Contains("create 0 1x1", platform.Backend.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Initialize_CreateTexture_Throws()
    {
        _game.OnInitialize = static (_, graphics) => graphics.CreateTexture(GraphicsTests.SolidImage(1, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RunAsync(_game, new ScriptedPlatform(0.01)));
    }

    [Fact]
    public async Task Update_NoResizeYet_ViewportIsVirtualScreenAtScaleOne()
    {
        await _engine.RunAsync(_game, new ScriptedPlatform(0.01));

        Assert.Equal(PixelViewport.Fit(640, 360, 640, 360, 1), _game.Updates[0].Viewport);
    }

    [Fact]
    public async Task Update_Resize_SetsViewportForFixedStepsAndUpdate()
    {
        var platform = ScriptedPlatform.FromFrames(new ScriptedFrame(GameEngine.FixedDeltaTime, [Resize(1280, 720, 1.5f)]));

        await _engine.RunAsync(_game, platform);

        var expected = PixelViewport.Fit(640, 360, 1280, 720, 1.5f);
        Assert.Equal(3, expected.Scale);
        Assert.Equal(expected, Assert.Single(_game.FixedUpdates).Viewport);
        Assert.Equal(expected, _game.Updates[0].Viewport);
    }

    [Fact]
    public async Task Update_SeveralResizes_LastOneWinsAndPersists()
    {
        var platform = ScriptedPlatform.FromFrames(
            new ScriptedFrame(0.01, [Resize(640, 360, 1), Resize(1400, 800, 1)]),
            new ScriptedFrame(0.01, []));

        await _engine.RunAsync(_game, platform);

        var expected = PixelViewport.Fit(640, 360, 1400, 800, 1);
        Assert.Equal(expected, _game.Updates[0].Viewport);
        Assert.Equal(expected, _game.Updates[1].Viewport);
    }

    [Fact]
    public async Task Render_SubmitsOneFrameAfterGameRender()
    {
        var platform = ScriptedPlatform.FromFrames(new ScriptedFrame(0.01, [Resize(1400, 800, 1)]));
        var submittedBeforeRender = -1;
        _game.OnRender = batch =>
        {
            submittedBeforeRender = platform.Backend.Frames.Count;
            batch.Clear(Color.White);
        };

        await _engine.RunAsync(_game, platform);

        Assert.Equal(0, submittedBeforeRender);
        var commands = Assert.Single(platform.Backend.Frames).Decode();
        Assert.Equal(new RenderCommand(RenderCommandKind.SetSurface, [1400, 800, 640, 360, 60, 40, 1280, 720]), commands[0]);
        Assert.Equal(RenderCommandKind.Clear, commands[1].Kind);
        Assert.Equal(RenderCommandKind.Present, commands[^1].Kind);
    }

    [Fact]
    public async Task Render_TextureDestroyedAfterDraw_IsDeletedAfterSubmit()
    {
        var platform = new ScriptedPlatform(0.01);
        Texture texture = default;
        _game.OnUpdate = graphics => texture = graphics.CreateTexture(GraphicsTests.SolidImage(1, 1));
        _game.OnRender = batch =>
        {
            batch.Draw(texture, Vector2.Zero);
            _game.Graphics!.DestroyTexture(texture);
        };

        await _engine.RunAsync(_game, platform);

        Assert.Equal(["create 0 1x1", "submit", "destroy 0"], platform.Backend.Calls);
    }

    [Fact]
    public async Task RunAsync_End_DestroysRemainingTexturesAfterLastFrame()
    {
        var platform = new ScriptedPlatform(0.01, 0.01);
        _game.OnUpdate = graphics =>
        {
            if (graphics.TextureCount == 0)
            {
                graphics.CreateTexture(GraphicsTests.SolidImage(1, 1));
            }
        };

        await _engine.RunAsync(_game, platform);

        Assert.Equal(["create 0 1x1", "submit", "submit", "destroy 0"], platform.Backend.Calls);
        Assert.Equal(0, _game.Graphics!.TextureCount);
    }

    [Fact]
    public async Task RunAsync_EachRun_GetsNewGraphics()
    {
        await _engine.RunAsync(_game, new ScriptedPlatform());
        var first = _game.Graphics;

        await _engine.RunAsync(_game, new ScriptedPlatform());

        Assert.NotNull(first);
        Assert.NotSame(first, _game.Graphics);
    }

    [Fact]
    public async Task RunAsync_PlatformFailsAndDestroyThrows_RethrowsRunException()
    {
        var platform = new ControlledPlatform();
        _game.OnUpdate = static graphics => graphics.CreateTexture(GraphicsTests.SolidImage(1, 1));
        var run = _engine.RunAsync(_game, platform);
        _engine.Update(0.01, []);
        platform.Backend.OnDestroy = static _ => throw new InvalidOperationException("lost");
        var failure = new InvalidOperationException("platform failure");

        platform.Fail(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => run));
    }

    [Fact]
    public async Task RunAsync_DestroyThrowsAfterCleanEnd_Throws()
    {
        var platform = new ScriptedPlatform(0.01);
        _game.OnUpdate = static graphics => graphics.CreateTexture(GraphicsTests.SolidImage(1, 1));
        platform.Backend.OnDestroy = static _ => throw new InvalidOperationException("lost");

        await Assert.ThrowsAsync<AggregateException>(() => _engine.RunAsync(_game, platform));
    }
}
