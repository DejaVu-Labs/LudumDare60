using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// A rectangle in device pixels from the top-left corner.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// The part of this rectangle inside a <paramref name="width"/> by <paramref name="height"/> surface; empty
    /// (zero width or height) when they do not overlap.
    /// </summary>
    public PixelRect ClampTo(int width, int height)
    {
        var left = Math.Clamp(X, 0, width);
        var top = Math.Clamp(Y, 0, height);
        var right = Math.Clamp((long)X + Width, left, width);
        var bottom = Math.Clamp((long)Y + Height, top, height);
        return new PixelRect(left, top, (int)(right - left), (int)(bottom - top));
    }
}
