using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Game.Cleaning;

namespace LudumDare60.Game.Tests.Cleaning;

public sealed class DirtLayerTests
{
    private static Image Solid(int width, int height, byte alpha = 255)
    {
        var pixels = new byte[width * height * Image.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += Image.BytesPerPixel)
        {
            pixels[i] = 0x82;
            pixels[i + 3] = alpha;
        }

        return new Image(width, height, pixels);
    }

    private static byte AlphaAt(DirtLayer layer, int x, int y) => layer.Image.Pixels[(((y * layer.Image.Width) + x) * 4) + 3];

    [Fact]
    public void Constructor_CountsOpaquePixelsAsDirt()
    {
        var image = Solid(4, 3);
        var pixels = image.Pixels.ToArray();
        pixels[3] = 0;
        pixels[7] = 0;

        var layer = new DirtLayer(new Image(4, 3, pixels));

        Assert.Equal(10, layer.InitialDirt);
        Assert.Equal(10, layer.Dirt);
        Assert.Equal(0, layer.Cleanliness);
        Assert.False(layer.IsClean);
    }

    [Fact]
    public void Constructor_NoDirt_IsCleanAlready()
    {
        var layer = new DirtLayer(Solid(4, 3, alpha: 0));

        Assert.True(layer.IsClean);
        Assert.Equal(1, layer.Cleanliness);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(float.NaN)]
    public void Constructor_ThresholdOutOfRange_Throws(float threshold)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DirtLayer(Solid(2, 2), threshold));

    [Fact]
    public void Scrub_ChangesTheLayerNotTheSource()
    {
        var source = Solid(8, 8);
        var layer = new DirtLayer(source);

        layer.Scrub(new Vector2(4, 4), new Vector2(4, 4), 2);

        Assert.Equal(255, source.Pixels[(((4 * 8) + 4) * 4) + 3]);
        Assert.Equal(0, AlphaAt(layer, 4, 4));
    }

    [Fact]
    public void Scrub_OneStamp_ErasesADiscWithADitheredEdge()
    {
        var layer = new DirtLayer(Solid(20, 20));

        var erased = layer.Scrub(new Vector2(10, 10), new Vector2(10, 10), 3);

        // Centers within radius - 0.5: always erased.
        Assert.Equal(0, AlphaAt(layer, 10, 10));
        Assert.Equal(0, AlphaAt(layer, 11, 11));

        // Centers between radius - 0.5 and radius + 0.5: only where x + y is even. Both are 2.5 to 3 px away.
        Assert.Equal(0, AlphaAt(layer, 12, 10));
        Assert.Equal(255, AlphaAt(layer, 12, 11));

        // Centers beyond radius + 0.5: kept.
        Assert.Equal(255, AlphaAt(layer, 13, 10));
        Assert.Equal(255, AlphaAt(layer, 14, 10));
        Assert.Equal(400 - erased, layer.Dirt);
        Assert.InRange(erased, 20, 50);
    }

    [Fact]
    public void Scrub_Segment_ErasesAlongTheWholeWay()
    {
        var layer = new DirtLayer(Solid(20, 20));

        layer.Scrub(new Vector2(3, 10), new Vector2(17, 10), 1.5f);

        for (var x = 3; x < 17; x++)
        {
            Assert.Equal(0, AlphaAt(layer, x, 10));
        }

        Assert.Equal(255, AlphaAt(layer, 10, 13));
    }

    [Fact]
    public void Scrub_SameSpotTwice_CountsEachPixelOnce()
    {
        var layer = new DirtLayer(Solid(10, 10));
        var first = layer.Scrub(new Vector2(5, 5), new Vector2(5, 5), 2);

        var second = layer.Scrub(new Vector2(5, 5), new Vector2(5, 5), 2);

        Assert.Equal(0, second);
        Assert.Equal(100 - first, layer.Dirt);
    }

    [Fact]
    public void Scrub_PartlyOutsideTheLayer_ErasesOnlyInside()
    {
        var layer = new DirtLayer(Solid(4, 4));

        var erased = layer.Scrub(new Vector2(-10, -10), new Vector2(0.5f, 0.5f), 1);

        Assert.Equal(0, AlphaAt(layer, 0, 0));
        Assert.Equal(16 - erased, layer.Dirt);
    }

    [Fact]
    public void Scrub_PastTheThreshold_IsCleanAndStopsErasing()
    {
        var layer = new DirtLayer(Solid(10, 10), cleanThreshold: 0.9f);

        for (var y = 0; y < 10 && !layer.IsClean; y++)
        {
            layer.Scrub(new Vector2(0, y + 0.5f), new Vector2(10, y + 0.5f), 1);
        }

        var left = layer.Dirt;
        var later = layer.Scrub(new Vector2(0, 0), new Vector2(10, 10), 5);

        Assert.True(layer.IsClean);
        Assert.InRange(layer.Cleanliness, 0.9f, 1f);
        Assert.True(left > 0, "The last pixels are left for the game to fade.");
        Assert.Equal(0, later);
        Assert.Equal(left, layer.Dirt);
    }

    [Fact]
    public void TryTakeChangedRows_AfterScrub_GivesOneRangeOnce()
    {
        var layer = new DirtLayer(Solid(20, 20));
        layer.Scrub(new Vector2(5, 10), new Vector2(15, 12), 1.5f);

        Assert.True(layer.TryTakeChangedRows(out var first, out var count));
        Assert.False(layer.TryTakeChangedRows(out _, out _));
        // Stamps run from y = 10 to 12; the dithered edge, 2 px out, reaches the centers of rows 8 and 13.
        Assert.Equal(8, first);
        Assert.Equal(6, count);
    }

    [Fact]
    public void TryTakeChangedRows_NothingErased_IsFalse()
    {
        var layer = new DirtLayer(Solid(4, 4, alpha: 0));
        layer.Scrub(new Vector2(2, 2), new Vector2(2, 2), 2);

        Assert.False(layer.TryTakeChangedRows(out _, out _));
    }
}
