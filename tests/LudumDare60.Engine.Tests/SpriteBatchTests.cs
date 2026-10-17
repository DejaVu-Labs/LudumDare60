using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Engine.Tests;

public sealed class SpriteBatchTests
{
    private static readonly string s_fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "render-frame.json");
    private static readonly PixelViewport s_viewport = PixelViewport.Fit(640, 360, 1400, 800, 1);

    private readonly FakeGraphicsBackend _backend = new();
    private readonly Graphics _graphics;
    private readonly SpriteBatch _batch;
    private readonly Texture _a;
    private readonly Texture _b;

    public SpriteBatchTests()
    {
        _graphics = new Graphics(_backend);
        _graphics.BeginFrame();
        _batch = new SpriteBatch(_graphics);
        _a = _graphics.CreateTexture(GraphicsTests.SolidImage(32, 32));
        _b = _graphics.CreateTexture(GraphicsTests.SolidImage(16, 8));
    }

    private SubmittedFrame Frame(Action<SpriteBatch> render)
    {
        _batch.Begin(in s_viewport);
        render(_batch);
        _batch.End();
        return _backend.Frames[^1];
    }

    private static RenderCommand Draw(int texture, int first, int count)
        => new(RenderCommandKind.DrawSprites, [texture, first, count]);

    [Fact]
    public void Draw_OutsideFrame_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _batch.Draw(_a, Vector2.Zero));
        Assert.Throws<InvalidOperationException>(() => _batch.Clear(Color.Black));
    }

    [Fact]
    public void Draw_AfterEnd_Throws()
    {
        Frame(static _ => { });

        Assert.Throws<InvalidOperationException>(() => _batch.Draw(_a, Vector2.Zero));
    }

    [Fact]
    public void End_EmptyFrame_SubmitsSurfaceAndPresent()
    {
        var frame = Frame(static _ => { });

        Assert.Equal(
            [
                new RenderCommand(RenderCommandKind.SetSurface, [1400, 800, 640, 360, 60, 40, 1280, 720]),
                new RenderCommand(RenderCommandKind.Present, []),
            ],
            frame.Decode());
        Assert.Empty(frame.Instances);
    }

    [Fact]
    public void Draw_SameTexture_SharesOneDrawCall()
    {
        var frame = Frame(b =>
        {
            b.Draw(_a, Vector2.Zero);
            b.Draw(_a, Vector2.One);
            b.Draw(_a, Vector2.UnitX);
        });

        Assert.Equal([Draw(_a.Id, 0, 3)], frame.Decode().Where(static c => c.Kind == RenderCommandKind.DrawSprites));
        Assert.Equal(3, frame.Instances.Length);
    }

    [Fact]
    public void Draw_TextureChanges_SplitsRunsInCallOrder()
    {
        var count = 0;
        var frame = Frame(b =>
        {
            b.Draw(_a, Vector2.Zero);
            b.Draw(_a, Vector2.Zero);
            b.Draw(_b, Vector2.Zero);
            b.Draw(_a, Vector2.Zero);
            count = b.DrawCallCount;
        });

        Assert.Equal(
            [Draw(_a.Id, 0, 2), Draw(_b.Id, 2, 1), Draw(_a.Id, 3, 1)],
            frame.Decode().Where(static c => c.Kind == RenderCommandKind.DrawSprites));
        Assert.Equal(2, count);
    }

    [Fact]
    public void Clear_BetweenDraws_ClosesTheRunAndKeepsOrder()
    {
        var frame = Frame(b =>
        {
            b.Draw(_a, Vector2.Zero);
            b.Clear(Color.Black);
            b.Draw(_a, Vector2.Zero);
        });

        Assert.Equal(
            [
                RenderCommandKind.SetSurface,
                RenderCommandKind.DrawSprites,
                RenderCommandKind.Clear,
                RenderCommandKind.DrawSprites,
                RenderCommandKind.Present,
            ],
            frame.Decode().Select(static c => c.Kind));
    }

    [Fact]
    public void Begin_ForgetsThePreviousFrame()
    {
        Frame(b => b.Draw(_a, Vector2.Zero));

        var frame = Frame(b => b.Draw(_b, Vector2.Zero));

        Assert.Single(frame.Instances);
        Assert.Equal(Draw(_b.Id, 0, 1), frame.Decode()[1]);
    }

    [Fact]
    public void Draw_WholeTexture_UsesFullSourceAndDefaults()
    {
        var frame = Frame(b => b.Draw(_b, new Vector2(3.5f, 4)));

        Assert.Equal(
            new SpriteInstance(new Vector2(3.5f, 4), Vector2.Zero, Vector2.One, 0, Color.White, 0, 0, 16, 8),
            Assert.Single(frame.Instances));
    }

    [Fact]
    public void Draw_Options_LandInTheInstance()
    {
        var options = new SpriteOptions
        {
            Origin = new Vector2(8, 8),
            Scale = new Vector2(-2, 2),
            Rotation = 0.5f,
            Tint = new Color(255, 128, 0, 200),
        };

        var frame = Frame(b => b.Draw(_a, new IntRect(8, 8, 16, 16), new Vector2(100, 50), options));

        Assert.Equal(
            new SpriteInstance(new Vector2(100, 50), new Vector2(8, 8), new Vector2(-2, 2), 0.5f, new Color(255, 128, 0, 200), 8, 8, 16, 16),
            Assert.Single(frame.Instances));
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(31, 0, 2, 1)]
    [InlineData(0, 31, 1, 2)]
    public void Draw_SourceEmptyOrOutside_Throws(int x, int y, int width, int height)
    {
        var source = new IntRect(x, y, width, height);

        Frame(b => Assert.Throws<ArgumentOutOfRangeException>(() => b.Draw(_a, source, Vector2.Zero)));

        Assert.Empty(_backend.Frames[^1].Instances);
    }

    [Fact]
    public void Draw_DestroyedTexture_Throws()
    {
        _graphics.DestroyTexture(_b);

        Frame(b => Assert.Throws<InvalidOperationException>(() => b.Draw(_b, Vector2.Zero)));
    }

    [Fact]
    public void Draw_OverCapacity_Throws()
    {
        Frame(b =>
        {
            for (var i = 0; i < SpriteBatch.Capacity; i++)
            {
                b.Draw(_a, Vector2.Zero);
            }

            Assert.Throws<InvalidOperationException>(() => b.Draw(_a, Vector2.Zero));
        });

        Assert.Equal(SpriteBatch.Capacity, _backend.Frames[^1].Instances.Length);
    }

    [Fact]
    public void Draw_AlternatingTexturesAtCapacity_FitsTheCommandBuffer()
    {
        var frame = Frame(b =>
        {
            b.Clear(Color.Black);
            for (var i = 0; i < SpriteBatch.Capacity; i++)
            {
                b.Draw(i % 2 == 0 ? _a : _b, Vector2.Zero);
            }
        });

        Assert.Equal(SpriteBatch.Capacity, frame.Decode().Count(static c => c.Kind == RenderCommandKind.DrawSprites));
    }

    [Fact]
    public void Clear_TooMany_Throws()
    {
        Frame(b =>
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                for (var i = 0; i < SpriteBatch.CommandCapacity; i++)
                {
                    b.Clear(Color.Black);
                }
            });
        });
    }

    [Fact]
    public void Frame_MatchesSharedFixture()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(s_fixturePath));
        var root = fixture.RootElement;
        var clear = root.GetProperty("clear").EnumerateArray().Select(static e => e.GetByte()).ToArray();

        var frame = Frame(b =>
        {
            b.Clear(new Color(clear[0], clear[1], clear[2], clear[3]));
            b.Draw(_a, new Vector2(10.5f, 20));
            b.Draw(_a, new IntRect(8, 8, 16, 16), new Vector2(100, 50), new SpriteOptions
            {
                Origin = new Vector2(8, 8),
                Scale = new Vector2(-2, 2),
                Rotation = 0.5f,
                Tint = new Color(255, 128, 0, 200),
            });
            b.Draw(_b, Vector2.Zero);
        });

        Assert.Equal(root.GetProperty("commandsHex").GetString(), Convert.ToHexStringLower(frame.Commands));
        Assert.Equal(
            root.GetProperty("instancesHex").GetString(),
            Convert.ToHexStringLower(MemoryMarshal.AsBytes(frame.Instances.AsSpan())));
    }
}
