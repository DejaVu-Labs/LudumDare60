using LudumDare60.Engine.Assets;
using LudumDare60.Game.Customers;

namespace LudumDare60.Game.Tests.Customers;

public sealed class CustomerSheetTests
{
    // A sheet with one solid block in the first frame, standing on the frame's bottom edge.
    private static Image BlockOnTheBottom()
    {
        var width = CustomerSheet.PoseCount * CustomerSheet.FrameWidth;
        var pixels = new byte[width * CustomerSheet.FrameHeight * Image.BytesPerPixel];
        for (var y = 80; y < CustomerSheet.FrameHeight; y++)
        {
            for (var x = 20; x < 50; x++)
            {
                pixels[(((y * width) + x) * Image.BytesPerPixel) + 3] = 255;
            }
        }

        return new Image(width, CustomerSheet.FrameHeight, pixels);
    }

    private static int AlphaAt(Image image, int x, int y) => image.Pixels[(((y * image.Width) + x) * Image.BytesPerPixel) + 3];

    [Fact]
    public void CreateGlow_Default_IsOpenUnderTheFrame()
    {
        var glow = CustomerSheet.CreateGlow(BlockOnTheBottom());

        // One pixel right and down of the sheet: the block's bottom middle is at glow (36, 96), the row under it is 97.
        Assert.Equal(0, AlphaAt(glow, 36, CustomerSheet.FrameHeight + 1));
        Assert.Equal(255, AlphaAt(glow, 20, 90));
    }

    [Fact]
    public void CreateGlow_ClosedBottom_RunsUnderTheFrame()
    {
        var glow = CustomerSheet.CreateGlow(BlockOnTheBottom(), openBottom: false);

        Assert.Equal(255, AlphaAt(glow, 36, CustomerSheet.FrameHeight + 1));
        Assert.Equal(255, AlphaAt(glow, 20, 90));
        Assert.Equal(0, AlphaAt(glow, 36, 90));
    }
}
