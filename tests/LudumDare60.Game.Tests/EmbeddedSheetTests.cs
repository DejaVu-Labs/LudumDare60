using System.Globalization;

namespace LudumDare60.Game.Tests;

public sealed class EmbeddedSheetTests
{
    public static TheoryData<string> Sheets => ["revolver", "tools"];

    private static EmbeddedSheet Sheet(string name) => string.Equals(name, "tools", StringComparison.Ordinal) ? EmbeddedSheet.Tools : EmbeddedSheet.Revolver;

    [Fact]
    public void Get_Revolver_HasAnOpaqueBodyAndTransparentSpaceAround()
    {
        // The barrel in the middle of the body, and the free corner right of the body above the parts row.
        Assert.Equal(255, EmbeddedSheet.Revolver.Get(40, 12).A);
        Assert.Equal(0, EmbeddedSheet.Revolver.Get(188, 0).A);
    }

    [Fact]
    public void Get_Tools_HasTheOilCanAndTheBrushApart()
    {
        // The middle of the oil can's body, the middle of the brush's handle, and the gap between the two.
        Assert.Equal(255, EmbeddedSheet.Tools.Get(26, 28).A);
        Assert.Equal(255, EmbeddedSheet.Tools.Get(51 + 34, 10).A);
        Assert.Equal(0, EmbeddedSheet.Tools.Get(50, 0).A);
    }

    [Theory]
    [MemberData(nameof(Sheets))]
    public void Get_EverySheet_UsesOnlyPaletteColorsAndHardAlpha(string name)
    {
        var sheet = Sheet(name);
        var palette = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "six-chambers.gpl"))
            .Skip(4)
            .Where(static line => line.Trim().Length > 0)
            .Select(static line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(static parts => (byte.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture), byte.Parse(parts[2], CultureInfo.InvariantCulture)))
            .ToHashSet();

        for (var y = 0; y < sheet.Height; y++)
        {
            for (var x = 0; x < sheet.Width; x++)
            {
                var color = sheet.Get(x, y);
                Assert.True(color.A is 0 or 255, $"({x}, {y})");
                Assert.True(color.A == 0 || palette.Contains((color.R, color.G, color.B)), $"({x}, {y})");
            }
        }
    }
}
