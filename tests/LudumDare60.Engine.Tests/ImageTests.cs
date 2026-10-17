using LudumDare60.Engine.Assets;

namespace LudumDare60.Engine.Tests;

public sealed class ImageTests
{
    [Fact]
    public void Constructor_ValidPixels_KeepsArrayAndSize()
    {
        byte[] pixels = [1, 2, 3, 4, 5, 6, 7, 8];

        var image = new Image(2, 1, pixels);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(8, image.Stride);
        Assert.Equal(pixels, image.Pixels.ToArray());
        pixels[0] = 9;
        Assert.Equal(9, image.Pixels[0]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void Constructor_SizeBelowOne_Throws(int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new Image(width, height, []));

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    public void Constructor_WrongPixelCount_Throws(int length)
        => Assert.Throws<ArgumentException>(() => new Image(2, 1, new byte[length]));

    [Fact]
    public void Constructor_NullPixels_Throws() => Assert.Throws<ArgumentNullException>(() => new Image(1, 1, null!));
}
