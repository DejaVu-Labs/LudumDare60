using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Game;

/// <summary>
/// The glow drawn behind something the player can click: a one pixel outline around the outside of a sprite, as
/// large as the sprite plus one pixel on every side, so it is drawn one pixel up and left of it.
/// </summary>
internal static class SpriteOutline
{
    /// <summary>
    /// The glow color, tinted with the glow's strength when drawn.
    /// </summary>
    public static readonly Color Color = Color.FromRgb(0xfbe099);

    // A pixel at least this opaque belongs to the sprite.
    private const byte SolidAlpha = 128;

    /// <summary>
    /// Writes the outline of <paramref name="frame"/> of <paramref name="source"/> into <paramref name="target"/>, a
    /// sheet <paramref name="targetWidth"/> pixels wide, with its top-left corner at
    /// (<paramref name="x"/>, <paramref name="y"/>). With <paramref name="openBottom"/>, the row under the frame is left
    /// out: a bust cut off at its bottom edge has no outline there.
    /// </summary>
    public static void Draw(
        ReadOnlySpan<byte> source, int sourceWidth, IntRect frame, Span<byte> target, int targetWidth, int x, int y, bool openBottom)
    {
        var rows = frame.Height + (openBottom ? 1 : 2);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < frame.Width + 2; column++)
            {
                if (IsOutline(source, sourceWidth, frame, column - 1, row - 1))
                {
                    var i = (((y + row) * targetWidth) + x + column) * Image.BytesPerPixel;
                    target[i] = Color.R;
                    target[i + 1] = Color.G;
                    target[i + 2] = Color.B;
                    target[i + 3] = Color.A;
                }
            }
        }
    }

    private static bool IsOutline(ReadOnlySpan<byte> source, int sourceWidth, IntRect frame, int x, int y)
    {
        if (IsSolid(source, sourceWidth, frame, x, y))
        {
            return false;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (IsSolid(source, sourceWidth, frame, x + dx, y + dy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSolid(ReadOnlySpan<byte> source, int sourceWidth, IntRect frame, int x, int y)
        => x >= 0 && y >= 0 && x < frame.Width && y < frame.Height
            && source[((((frame.Y + y) * sourceWidth) + frame.X + x) * Image.BytesPerPixel) + 3] >= SolidAlpha;
}
