using System.Numerics;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine.Tests;

public sealed class PixelViewportTests
{
    private const int W = 640;
    private const int H = 360;
    private const float Tolerance = 1e-4f;

    [Theory]
    [InlineData(640, 360, 1, 1, 0, 0, 640, 360)]
    [InlineData(1280, 720, 1, 2, 0, 0, 1280, 720)]
    [InlineData(1920, 1080, 1, 3, 0, 0, 1920, 1080)]
    [InlineData(1280, 720, 1.5f, 3, 0, 0, 1920, 1080)]
    [InlineData(1400, 800, 1, 2, 60, 40, 1280, 720)]
    [InlineData(1279, 720, 1, 1, 319, 180, 640, 360)]
    [InlineData(1281, 721, 1, 2, 0, 0, 1280, 720)]
    [InlineData(800, 1000, 2, 2, 160, 640, 1280, 720)]
    public void Fit_SurfaceAtLeastVirtualSize_UsesLargestWholeScaleCentered(
        float surfaceWidth, float surfaceHeight, float pixelRatio, float scale, int x, int y, int width, int height)
    {
        var viewport = PixelViewport.Fit(W, H, surfaceWidth, surfaceHeight, pixelRatio);

        Assert.Equal(scale, viewport.Scale);
        Assert.Equal(x, viewport.X);
        Assert.Equal(y, viewport.Y);
        Assert.Equal(width, viewport.Width);
        Assert.Equal(height, viewport.Height);
    }

    [Fact]
    public void Fit_PixelRatio_SurfaceSizeIsInRoundedDevicePixels()
    {
        var viewport = PixelViewport.Fit(W, H, 1001, 700.5f, 1.25f);

        Assert.Equal(1251, viewport.SurfaceWidth);
        Assert.Equal(876, viewport.SurfaceHeight);
        Assert.Equal(1.25f, viewport.PixelRatio);
    }

    [Fact]
    public void Fit_SurfaceSmallerThanVirtualSize_ScalesDownToFit()
    {
        var viewport = PixelViewport.Fit(W, H, 600, 300, 1);

        Assert.Equal(300f / H, viewport.Scale, Tolerance);
        Assert.Equal(533, viewport.Width);
        Assert.Equal(300, viewport.Height);
        Assert.Equal(33, viewport.X);
        Assert.Equal(0, viewport.Y);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 100)]
    [InlineData(float.NaN, float.NaN)]
    public void Fit_EmptyOrInvalidSurface_KeepsMappingFinite(float surfaceWidth, float surfaceHeight)
    {
        var viewport = PixelViewport.Fit(W, H, surfaceWidth, surfaceHeight, 1);
        var position = viewport.ToVirtual(10, 10);

        Assert.True(float.IsFinite(position.X));
        Assert.True(float.IsFinite(position.Y));
        Assert.True(viewport.Width >= 1);
        Assert.True(viewport.Height >= 1);
    }

    [Fact]
    public void Fit_HugeSurface_ClampsToMaxSurfaceSize()
    {
        var viewport = PixelViewport.Fit(W, H, float.MaxValue / 2, 1e12f, 1);

        Assert.Equal(PixelViewport.MaxSurfaceSize, viewport.SurfaceWidth);
        Assert.Equal(PixelViewport.MaxSurfaceSize, viewport.SurfaceHeight);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Fit_InvalidPixelRatio_UsesOne(float pixelRatio)
    {
        var viewport = PixelViewport.Fit(W, H, 1280, 720, pixelRatio);

        Assert.Equal(1f, viewport.PixelRatio);
        Assert.Equal(2f, viewport.Scale);
    }

    [Theory]
    [InlineData(0, 360)]
    [InlineData(640, 0)]
    public void Fit_NonPositiveVirtualSize_Throws(int virtualWidth, int virtualHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelViewport.Fit(virtualWidth, virtualHeight, 1280, 720, 1));
    }

    [Theory]
    [InlineData(60, 40, 0, 0)]
    [InlineData(63, 45, 1.5f, 2.5f)]
    [InlineData(700, 400, 320, 180)]
    [InlineData(1340, 760, 640, 360)]
    [InlineData(0, 0, -30, -20)]
    public void ToVirtual_Letterboxed_SubtractsOffsetAndDividesByScale(float surfaceX, float surfaceY, float x, float y)
    {
        var viewport = PixelViewport.Fit(W, H, 1400, 800, 1);

        var position = viewport.ToVirtual(surfaceX, surfaceY);

        Assert.Equal(x, position.X, Tolerance);
        Assert.Equal(y, position.Y, Tolerance);
    }

    [Fact]
    public void ToVirtual_PixelRatio_ConvertsLogicalToDevicePixelsFirst()
    {
        // 1280x720 logical at ratio 1.5 is 1920x1080 device pixels: scale 3, no bars.
        var viewport = PixelViewport.Fit(W, H, 1280, 720, 1.5f);

        var position = viewport.ToVirtual(640, 360);

        Assert.Equal(320, position.X, Tolerance);
        Assert.Equal(180, position.Y, Tolerance);
    }

    [Fact]
    public void ToVirtual_ScaledDown_MapsCornersToScreenCorners()
    {
        var viewport = PixelViewport.Fit(W, H, 600, 300, 1);

        var topLeft = viewport.ToVirtual(viewport.X, viewport.Y);
        var bottomRight = viewport.ToVirtual(viewport.X + viewport.Width, viewport.Y + viewport.Height);

        Assert.Equal(Vector2.Zero, topLeft);
        Assert.Equal(W, bottomRight.X, Tolerance);
        Assert.Equal(H, bottomRight.Y, Tolerance);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(639.9f, 359.9f, true)]
    [InlineData(640, 100, false)]
    [InlineData(100, 360, false)]
    [InlineData(-0.1f, 100, false)]
    [InlineData(100, -0.1f, false)]
    public void Contains_ChecksHalfOpenScreenBounds(float x, float y, bool expected)
    {
        var viewport = PixelViewport.Fit(W, H, 1280, 720, 1);

        Assert.Equal(expected, viewport.Contains(new Vector2(x, y)));
    }
}
