using LudumDare60.Engine.Platform.Desktop.Rendering;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class PixelRectTests
{
    [Theory]
    [InlineData(60, 40, 1280, 720, 1400, 800, 60, 40, 1280, 720)]
    [InlineData(60, 40, 1280, 720, 1300, 700, 60, 40, 1240, 660)]
    [InlineData(-10, -5, 100, 50, 200, 200, 0, 0, 90, 45)]
    [InlineData(300, 300, 10, 10, 200, 200, 200, 200, 0, 0)]
    [InlineData(0, 0, int.MaxValue, int.MaxValue, 64, 32, 0, 0, 64, 32)]
    public void ClampTo_KeepsThePartInsideTheSurface(
        int x, int y, int width, int height, int surfaceWidth, int surfaceHeight,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
        => Assert.Equal(
            new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight),
            new PixelRect(x, y, width, height).ClampTo(surfaceWidth, surfaceHeight));
}
