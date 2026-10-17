using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Game;

/// <summary>
/// A sprite generated in code until the real art exists: a brass checkerboard with an ink outline, cut corners
/// (transparent pixels) and a rust marker in the top-left cell, so flips and rotations are visible.
/// </summary>
internal static class PlaceholderSprite
{
    public const int Size = 32;

    private const int Cell = 4;

    private static readonly Color s_ink = Color.FromRgb(0x0e0f17);
    private static readonly Color s_light = Color.FromRgb(0xf5dc8a);
    private static readonly Color s_dark = Color.FromRgb(0xa8782b);
    private static readonly Color s_marker = Color.FromRgb(0xc45f2a);

    public static Image Create()
    {
        var pixels = new byte[Size * Size * Image.BytesPerPixel];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var color = ColorAt(x, y);
                var i = ((y * Size) + x) * Image.BytesPerPixel;
                pixels[i] = color.R;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.B;
                pixels[i + 3] = color.A;
            }
        }

        return new Image(Size, Size, pixels);
    }

    private static Color ColorAt(int x, int y)
    {
        var edgeX = x is 0 or Size - 1;
        var edgeY = y is 0 or Size - 1;
        if (edgeX && edgeY)
        {
            return Color.Transparent;
        }

        if (edgeX || edgeY)
        {
            return s_ink;
        }

        if (x < 1 + Cell && y < 1 + Cell)
        {
            return s_marker;
        }

        return ((x / Cell) + (y / Cell)) % 2 == 0 ? s_light : s_dark;
    }
}
