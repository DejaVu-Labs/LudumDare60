using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Engine.Tests;

public sealed class GraphicsTests
{
    private readonly FakeGraphicsBackend _backend = new();
    private readonly Graphics _graphics;

    public GraphicsTests()
    {
        _graphics = new Graphics(_backend);
        _graphics.BeginFrame();
    }

    internal static Image SolidImage(int width, int height, byte value = 7)
    {
        var pixels = new byte[width * height * Image.BytesPerPixel];
        Array.Fill(pixels, value);
        return new Image(width, height, pixels);
    }

    [Fact]
    public void CreateTexture_BeforeFirstFrame_Throws()
    {
        var graphics = new Graphics(_backend);

        Assert.Throws<InvalidOperationException>(() => graphics.CreateTexture(SolidImage(2, 2)));
        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void CreateTexture_UploadsPixelsAndReturnsLiveHandle()
    {
        var image = SolidImage(3, 2, 42);

        var texture = _graphics.CreateTexture(image);

        Assert.Equal(["create 0 3x2"], _backend.Calls);
        Assert.Equal(image.Pixels.ToArray(), _backend.Textures[0]);
        Assert.Equal(3, texture.Width);
        Assert.Equal(2, texture.Height);
        Assert.True(_graphics.IsAlive(texture));
        Assert.Equal(1, _graphics.TextureCount);
    }

    [Fact]
    public void CreateTexture_Several_GetDistinctIds()
    {
        var first = _graphics.CreateTexture(SolidImage(1, 1));
        var second = _graphics.CreateTexture(SolidImage(1, 1));

        Assert.NotEqual(first, second);
        Assert.Equal(["create 0 1x1", "create 1 1x1"], _backend.Calls);
    }

    [Theory]
    [InlineData(Graphics.MaxTextureSize + 1, 1)]
    [InlineData(1, Graphics.MaxTextureSize + 1)]
    public void CreateTexture_TooLarge_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _graphics.CreateTexture(SolidImage(width, height)));
        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void UpdateTexture_Rows_UploadsOnlyThoseRows()
    {
        var texture = _graphics.CreateTexture(SolidImage(2, 4, 0));
        var changed = SolidImage(2, 4, 9);

        _graphics.UpdateTexture(texture, changed, 1, 2);

        Assert.Equal("update 0 rows 1+2", _backend.Calls[^1]);
        var pixels = _backend.Textures[0];
        Assert.All(pixels.AsSpan(0, 8).ToArray(), b => Assert.Equal(0, b));
        Assert.All(pixels.AsSpan(8, 16).ToArray(), b => Assert.Equal(9, b));
        Assert.All(pixels.AsSpan(24, 8).ToArray(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void UpdateTexture_WrongImageSize_Throws()
    {
        var texture = _graphics.CreateTexture(SolidImage(2, 4));

        Assert.Throws<ArgumentException>(() => _graphics.UpdateTexture(texture, SolidImage(4, 2), 0, 1));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void UpdateTexture_RowsOutsideTexture_Throws(int firstRow, int rowCount)
    {
        var texture = _graphics.CreateTexture(SolidImage(2, 4));

        Assert.Throws<ArgumentOutOfRangeException>(() => _graphics.UpdateTexture(texture, SolidImage(2, 4), firstRow, rowCount));
        Assert.Equal(["create 0 2x4"], _backend.Calls);
    }

    [Fact]
    public void UpdateTexture_StaleHandle_Throws()
    {
        var texture = _graphics.CreateTexture(SolidImage(2, 4));
        _graphics.DestroyTexture(texture);

        Assert.Throws<InvalidOperationException>(() => _graphics.UpdateTexture(texture, SolidImage(2, 4), 0, 1));
    }

    [Fact]
    public void IsAlive_Default_IsFalse() => Assert.False(_graphics.IsAlive(default));

    [Fact]
    public void DestroyTexture_HandleStaleAtOnce_BackendDeletesAtEndOfFrame()
    {
        var texture = _graphics.CreateTexture(SolidImage(1, 1));

        _graphics.DestroyTexture(texture);

        Assert.False(_graphics.IsAlive(texture));
        Assert.Equal(0, _graphics.TextureCount);
        Assert.DoesNotContain("destroy 0", _backend.Calls, StringComparer.Ordinal);

        _graphics.EndFrame();

        Assert.Equal(["create 0 1x1", "destroy 0"], _backend.Calls);
    }

    [Fact]
    public void DestroyTexture_Twice_Throws()
    {
        var texture = _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.DestroyTexture(texture);

        Assert.Throws<InvalidOperationException>(() => _graphics.DestroyTexture(texture));
    }

    [Fact]
    public void DestroyTexture_Default_Throws()
        => Assert.Throws<InvalidOperationException>(() => _graphics.DestroyTexture(default));

    [Fact]
    public void CreateTexture_BeforeDestroyIsApplied_DoesNotReuseId()
    {
        var old = _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.DestroyTexture(old);

        var fresh = _graphics.CreateTexture(SolidImage(2, 2));

        Assert.Equal("create 1 2x2", _backend.Calls[^1]);
        Assert.True(_graphics.IsAlive(fresh));
    }

    [Fact]
    public void CreateTexture_AfterDestroyIsApplied_ReusesIdWithNewGeneration()
    {
        var old = _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.DestroyTexture(old);
        _graphics.EndFrame();

        var fresh = _graphics.CreateTexture(SolidImage(2, 2));

        Assert.Equal("create 0 2x2", _backend.Calls[^1]);
        Assert.True(_graphics.IsAlive(fresh));
        Assert.False(_graphics.IsAlive(old));
        Assert.Throws<InvalidOperationException>(() => _graphics.DestroyTexture(old));
    }

    [Fact]
    public void DestroyAll_DeletesLiveAndPendingTextures_HandlesStale()
    {
        var kept = _graphics.CreateTexture(SolidImage(1, 1));
        var destroyed = _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.DestroyTexture(destroyed);

        _graphics.DestroyAll();

        Assert.Empty(_backend.Textures);
        Assert.False(_graphics.IsAlive(kept));
        Assert.Equal(0, _graphics.TextureCount);
        Assert.Throws<InvalidOperationException>(() => _graphics.CreateTexture(SolidImage(1, 1)));
    }

    [Fact]
    public void DestroyAll_TexturesAlreadyDeleted_AreNotDeletedAgain()
    {
        var texture = _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.DestroyTexture(texture);
        _graphics.EndFrame();

        _graphics.DestroyAll();

        Assert.Single(_backend.Calls, static c => string.Equals(c, "destroy 0", StringComparison.Ordinal));
    }

    [Fact]
    public void DestroyAll_BackendThrows_DeletesTheRestAndAggregates()
    {
        _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.CreateTexture(SolidImage(1, 1));
        _graphics.CreateTexture(SolidImage(1, 1));
        _backend.OnDestroy = static id =>
        {
            if (id == 1)
            {
                throw new InvalidOperationException("lost");
            }
        };

        var error = Assert.Throws<AggregateException>(_graphics.DestroyAll);

        Assert.Single(error.InnerExceptions);
        Assert.Empty(_backend.Textures);
    }
}
