using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Game;

/// <summary>
/// Revolver parts generated in code until the real sprites exist, in one sheet: a block of frame steel with the bore
/// of the base pin and an open window for the cylinder, the base pin (knurled head on the left, rod to the right), the
/// cylinder seen from the side, and a glow outline for each part that moves. Colors come from the palette
/// (docs/gamedesign/art-direction.md).
/// </summary>
internal static class PlaceholderParts
{
    public const int Width = 132;
    public const int Height = 80;

    /// <summary>
    /// The frame block. The bore opens in its left edge, at row <see cref="BoreRow"/>; the cylinder window is
    /// transparent.
    /// </summary>
    public static readonly IntRect Frame = new(0, 0, 64, 28);

    /// <summary>
    /// One pixel around the outside of <see cref="Frame"/>: the whole revolver glows when it is ready to hand back.
    /// </summary>
    public static readonly IntRect FrameGlow = Around(Frame, 64, 0);

    public static readonly IntRect Pin = new(0, 28, 40, 7);

    /// <summary>
    /// One pixel around <see cref="Pin"/>, so it is drawn one pixel up and left of the pin.
    /// </summary>
    public static readonly IntRect PinGlow = Around(Pin, 0, 35);

    public static readonly IntRect Cylinder = new(0, 44, CylinderWidth, CylinderHeight);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect CylinderGlow = Around(Cylinder, 24, 44);

    /// <summary>
    /// The scrubbing brush lying flat: wire bristles on the left, a brass ferrule, a walnut handle to the right.
    /// </summary>
    public static readonly IntRect Brush = new(0, 64, BrushWidth, 7);

    /// <inheritdoc cref="PinGlow"/>
    public static readonly IntRect BrushGlow = Around(Brush, 22, 64);

    /// <summary>
    /// The middle of the bristles, in brush pixels: the point that scrubs.
    /// </summary>
    public static readonly Vector2 BrushOrigin = new(3, 3);

    /// <summary>
    /// The pin's grab point, the middle of its head, in pin pixels.
    /// </summary>
    public static readonly Vector2 PinOrigin = new(2, 3);

    /// <summary>
    /// The cylinder's grab point, its middle, in cylinder pixels. It lies on the pin's axis.
    /// </summary>
    public static readonly Vector2 CylinderOrigin = new(12, 9);

    /// <summary>
    /// The row of <see cref="Frame"/> the pin's axis runs along.
    /// </summary>
    public const int BoreRow = 13;

    /// <summary>
    /// Where the seated cylinder's top-left corner lies in <see cref="Frame"/>. The window leaves one pixel around it.
    /// </summary>
    public static readonly Vector2 CylinderInFrame = new(12, BoreRow - 9);

    private const int HeadWidth = 4;
    private const int CylinderWidth = 24;
    private const int CylinderHeight = 18;
    private const int BrushWidth = 22;

    private static readonly Color s_ink = Color.FromRgb(0x0d0a0a);
    private static readonly Color s_steelDark = Color.FromRgb(0x2c201b);
    private static readonly Color s_steel = Color.FromRgb(0x453a30);
    private static readonly Color s_steelMid = Color.FromRgb(0x5f5247);
    private static readonly Color s_steelLight = Color.FromRgb(0xb3a99b);
    private static readonly Color s_glow = Color.FromRgb(0xfbe099);
    private static readonly Color s_brass = Color.FromRgb(0xe2ad5f);
    private static readonly Color s_brassDark = Color.FromRgb(0xd2834a);
    private static readonly Color s_walnutLight = Color.FromRgb(0xa1603b);
    private static readonly Color s_walnut = Color.FromRgb(0x823d1e);
    private static readonly Color s_walnutDark = Color.FromRgb(0x622a14);

    // Rust, dark to light, from the walnut, rust and copper ramp.
    private static readonly Color[] s_rust =
    [
        Color.FromRgb(0x622a14), Color.FromRgb(0x823d1e), Color.FromRgb(0x944f2d), Color.FromRgb(0xa1603b), Color.FromRgb(0xb37149),
    ];

    /// <summary>
    /// How much of the cylinder rust covers, from 0 (all of it) to 1 (none): pixels whose blotch value lies above it.
    /// </summary>
    private const float RustCutoff = 0.3f;

    public static Image Create()
    {
        var pixels = new byte[Width * Height * Image.BytesPerPixel];
        Fill(pixels, Frame, FrameColor);
        Fill(pixels, Pin, PinColor);
        Fill(pixels, Cylinder, CylinderColor);
        Fill(pixels, Brush, BrushColor);
        FillGlow(pixels, PinGlow, IsPin);
        FillGlow(pixels, CylinderGlow, IsCylinder);
        FillGlow(pixels, BrushGlow, IsBrush);
        FillGlow(pixels, FrameGlow, IsFrame);
        return new Image(Width, Height, pixels);
    }

    /// <summary>
    /// Rust over the cylinder, for a <see cref="Cleaning.DirtLayer"/>: blotches over most of it, the cylinder's size and
    /// origin. The same pixels every time.
    /// </summary>
    public static Image CreateCylinderRust()
    {
        var pixels = new byte[CylinderWidth * CylinderHeight * Image.BytesPerPixel];
        for (var y = 0; y < CylinderHeight; y++)
        {
            for (var x = 0; x < CylinderWidth; x++)
            {
                // Coarse blotches of 3x3 pixels, broken up per pixel.
                var value = (Hash(x / 3, y / 3) * 0.6f) + (Hash(x + 100, y + 100) * 0.4f);
                if (!IsCylinder(x, y) || value <= RustCutoff)
                {
                    continue;
                }

                var shade = Math.Min(s_rust.Length - 1, (int)((value - RustCutoff) / (1 - RustCutoff) * s_rust.Length));
                var i = ((y * CylinderWidth) + x) * Image.BytesPerPixel;
                var color = s_rust[shade];
                pixels[i] = color.R;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.B;
                pixels[i + 3] = color.A;
            }
        }

        return new Image(CylinderWidth, CylinderHeight, pixels);
    }

    /// <summary>
    /// A deterministic value in [0, 1) per integer point.
    /// </summary>
    private static float Hash(int x, int y)
    {
        var h = unchecked(((uint)x * 374761393u) + ((uint)y * 668265263u));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        h ^= h >> 16;
        return (h >> 8) / (float)(1 << 24);
    }

    private static IntRect Around(IntRect part, int x, int y) => new(x, y, part.Width + 2, part.Height + 2);

    private static Color FrameColor(int x, int y)
    {
        var windowX = (int)CylinderInFrame.X;
        var windowY = (int)CylinderInFrame.Y;

        // The window: the cylinder's rectangle plus a one pixel gap, inside a dark rim.
        var inWindow = x >= windowX - 1 && x <= windowX + Cylinder.Width && y >= windowY - 1 && y <= windowY + Cylinder.Height;
        if (inWindow)
        {
            return Color.Transparent;
        }

        var onRim = x >= windowX - 2 && x <= windowX + Cylinder.Width + 1 && y >= windowY - 2 && y <= windowY + Cylinder.Height + 1;
        if (x == 0 && Math.Abs(y - BoreRow) <= 1)
        {
            return s_ink;
        }

        if (onRim || x == 0 || y == 0 || x == Frame.Width - 1 || y == Frame.Height - 1)
        {
            return s_steelDark;
        }

        return y == 1 ? s_steelMid : s_steel;
    }

    // The frame's outline for its glow: the outside only, so the window gets none.
    private static bool IsFrame(int x, int y) => (uint)x < (uint)Frame.Width && (uint)y < (uint)Frame.Height;

    private static bool IsPin(int x, int y)
        => (uint)x < (uint)Pin.Width && (uint)y < (uint)Pin.Height && (x < HeadWidth || Math.Abs(y - (int)PinOrigin.Y) <= 1);

    private static Color PinColor(int x, int y)
    {
        if (!IsPin(x, y))
        {
            return Color.Transparent;
        }

        if (x < HeadWidth)
        {
            // Knurled head: dark grooves every other column, a dark rim around it.
            var rim = x == 0 || y == 0 || y == Pin.Height - 1;
            return rim ? s_steelDark : x % 2 == 0 ? s_steelMid : s_steelLight;
        }

        return y > PinOrigin.Y ? s_steelMid : s_steelLight;
    }

    private static bool IsCylinder(int x, int y)
    {
        if ((uint)x >= (uint)Cylinder.Width || (uint)y >= (uint)Cylinder.Height)
        {
            return false;
        }

        // Rounded ends: the corner pixels are cut.
        var cornerX = x is 0 or CylinderWidth - 1;
        var cornerY = y is 0 or CylinderHeight - 1;
        return !(cornerX && cornerY);
    }

    private static Color CylinderColor(int x, int y)
    {
        if (!IsCylinder(x, y))
        {
            return Color.Transparent;
        }

        if (x is 0 or CylinderWidth - 1 || y is 0 or CylinderHeight - 1)
        {
            return s_steelDark;
        }

        // Two flutes along the length, and light falling from the top.
        if (y is 6 or 12 && x is > 2 and < CylinderWidth - 3)
        {
            return s_ink;
        }

        return y switch
        {
            <= 3 => s_steelLight,
            <= 8 => s_steelMid,
            <= 13 => s_steel,
            _ => s_steelDark,
        };
    }

    private static bool IsBrush(int x, int y)
    {
        if ((uint)x >= (uint)Brush.Width || (uint)y >= (uint)Brush.Height)
        {
            return false;
        }

        return x switch
        {
            // Ragged bristle tips at the left end.
            0 => y is > 0 and < 6,
            <= 5 => true,
            <= 7 => y is > 0 and < 6,
            BrushWidth - 1 => y == 3,
            _ => y is > 1 and < 5,
        };
    }

    private static Color BrushColor(int x, int y)
    {
        if (!IsBrush(x, y))
        {
            return Color.Transparent;
        }

        if (x <= 5)
        {
            // Wire bristles: light and dark strands.
            return y % 2 == 0 ? s_steelLight : s_steelMid;
        }

        if (x <= 7)
        {
            return y <= 2 ? s_brass : s_brassDark;
        }

        return y switch
        {
            2 => s_walnutLight,
            3 => s_walnut,
            _ => s_walnutDark,
        };
    }

    private static void Fill(byte[] pixels, IntRect rect, Func<int, int, Color> color)
    {
        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                Set(pixels, rect.X + x, rect.Y + y, color(x, y));
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="glow"/> with a one pixel outline around a part's pixels, given in part coordinates.
    /// </summary>
    private static void FillGlow(byte[] pixels, IntRect glow, Func<int, int, bool> isPart)
    {
        for (var y = 0; y < glow.Height; y++)
        {
            for (var x = 0; x < glow.Width; x++)
            {
                if (IsOutline(x - 1, y - 1, isPart))
                {
                    Set(pixels, glow.X + x, glow.Y + y, s_glow);
                }
            }
        }
    }

    private static bool IsOutline(int x, int y, Func<int, int, bool> isPart)
    {
        if (isPart(x, y))
        {
            return false;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (isPart(x + dx, y + dy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Set(byte[] pixels, int x, int y, Color color)
    {
        var i = ((y * Width) + x) * Image.BytesPerPixel;
        pixels[i] = color.R;
        pixels[i + 1] = color.G;
        pixels[i + 2] = color.B;
        pixels[i + 3] = color.A;
    }
}
