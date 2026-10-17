using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Rendering;

/// <summary>
/// A rectangle in whole pixels: <see cref="X"/> and <see cref="Y"/> are its top-left corner.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct IntRect(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// The first column right of the rectangle: <c>X + Width</c>.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// The first row below the rectangle: <c>Y + Height</c>.
    /// </summary>
    public int Bottom => Y + Height;
}
