using System.Numerics;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Game;

/// <summary>
/// Revolver parts generated in code until the real sprites exist, in one sheet: a block of frame steel with the bore
/// of the base pin, the base pin (knurled head on the left, rod to the right) and the pin's glow outline. Colors come
/// from the palette (docs/gamedesign/art-direction.md).
/// </summary>
internal static class PlaceholderParts
{
    public const int Width = 64;
    public const int Height = 48;

    /// <summary>
    /// The frame block. The bore opens in its left edge, at row <see cref="BoreRow"/>.
    /// </summary>
    public static readonly IntRect Frame = new(0, 0, 64, 28);

    public static readonly IntRect Pin = new(0, 28, 40, 7);

    /// <summary>
    /// One pixel around <see cref="Pin"/>, so it is drawn one pixel up and left of the pin.
    /// </summary>
    public static readonly IntRect PinGlow = new(0, 35, Pin.Width + 2, Pin.Height + 2);

    /// <summary>
    /// The pin's grab point, the middle of its head, in pin pixels.
    /// </summary>
    public static readonly Vector2 PinOrigin = new(2, 3);

    public static readonly Vector2 PinGlowOrigin = PinOrigin + Vector2.One;

    /// <summary>
    /// The row of <see cref="Frame"/> the pin's axis runs along.
    /// </summary>
    public const int BoreRow = 13;

    private const int HeadWidth = 4;

    private static readonly Color s_ink = Color.FromRgb(0x0d0a0a);
    private static readonly Color s_steelDark = Color.FromRgb(0x2c201b);
    private static readonly Color s_steel = Color.FromRgb(0x453a30);
    private static readonly Color s_steelMid = Color.FromRgb(0x5f5247);
    private static readonly Color s_steelLight = Color.FromRgb(0xb3a99b);
    private static readonly Color s_glow = Color.FromRgb(0xfbe099);

    public static Image Create()
    {
        var pixels = new byte[Width * Height * Image.BytesPerPixel];
        for (var y = 0; y < Frame.Height; y++)
        {
            for (var x = 0; x < Frame.Width; x++)
            {
                Set(pixels, Frame.X + x, Frame.Y + y, FrameColor(x, y));
            }
        }

        for (var y = 0; y < Pin.Height; y++)
        {
            for (var x = 0; x < Pin.Width; x++)
            {
                Set(pixels, Pin.X + x, Pin.Y + y, PinColor(x, y));
            }
        }

        for (var y = 0; y < PinGlow.Height; y++)
        {
            for (var x = 0; x < PinGlow.Width; x++)
            {
                if (IsGlow(x - 1, y - 1))
                {
                    Set(pixels, PinGlow.X + x, PinGlow.Y + y, s_glow);
                }
            }
        }

        return new Image(Width, Height, pixels);
    }

    private static Color FrameColor(int x, int y)
    {
        if (x == 0 && Math.Abs(y - BoreRow) <= 1)
        {
            return s_ink;
        }

        if (x == 0 || y == 0 || x == Frame.Width - 1 || y == Frame.Height - 1)
        {
            return s_steelDark;
        }

        return y == 1 ? s_steelMid : s_steel;
    }

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

    private static bool IsGlow(int x, int y)
    {
        if (IsPin(x, y))
        {
            return false;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (IsPin(x + dx, y + dy))
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
