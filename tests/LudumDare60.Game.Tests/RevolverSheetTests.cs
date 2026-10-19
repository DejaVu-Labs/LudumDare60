using System.Globalization;

namespace LudumDare60.Game.Tests;

public sealed class RevolverSheetTests
{
    [Fact]
    public void Get_EmbeddedSheet_HasAnOpaqueBodyAndTransparentSpaceAround()
    {
        // The barrel in the middle of the body, and the free corner right of the body above the parts row.
        Assert.Equal(255, RevolverSheet.Get(40, 12).A);
        Assert.Equal(0, RevolverSheet.Get(188, 0).A);
    }

    [Fact]
    public void Get_EmbeddedSheet_UsesOnlyPaletteColorsAndHardAlpha()
    {
        var palette = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "six-chambers.gpl"))
            .Skip(4)
            .Where(static line => line.Trim().Length > 0)
            .Select(static line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(static parts => (byte.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture), byte.Parse(parts[2], CultureInfo.InvariantCulture)))
            .ToHashSet();

        for (var y = 0; y < 175; y++)
        {
            for (var x = 0; x < 189; x++)
            {
                var color = RevolverSheet.Get(x, y);
                Assert.True(color.A is 0 or 255, $"({x}, {y})");
                Assert.True(color.A == 0 || palette.Contains((color.R, color.G, color.B)), $"({x}, {y})");
            }
        }
    }
}
