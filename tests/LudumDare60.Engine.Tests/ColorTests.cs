using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

public sealed class ColorTests
{
    [Fact]
    public void Layout_IsRgbaBytes()
    {
        var color = new Color(1, 2, 3, 4);

        Assert.Equal(4, Unsafe.SizeOf<Color>());
        Assert.Equal([1, 2, 3, 4], MemoryMarshal.AsBytes(new ReadOnlySpan<Color>(in color)).ToArray());
    }

    [Fact]
    public void Constructor_AlphaDefaultsToOpaque() => Assert.Equal(255, new Color(1, 2, 3).A);

    [Fact]
    public void FromRgb_SplitsHexIntoOpaqueColor()
        => Assert.Equal(new Color(0x24, 0x5c, 0x48, 255), Color.FromRgb(0x245c48));

    [Fact]
    public void WithAlpha_KeepsRgb() => Assert.Equal(new Color(1, 2, 3, 9), new Color(1, 2, 3).WithAlpha(9));
}
