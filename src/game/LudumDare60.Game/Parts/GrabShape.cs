using System.Numerics;

namespace LudumDare60.Game.Parts;

/// <summary>
/// The pixels a part can be taken by, for a part whose shape a circle around its grab point does not fit: a wide
/// panel, a long slanted spring. It covers the part's pixels and <see cref="Margin"/> pixels around them, so that a
/// thin part is not fiddly to take.
/// </summary>
public sealed class GrabShape
{
    private readonly bool[] _pixels;
    private readonly int _width;
    private readonly int _height;

    // The part's grab point in shape pixels: the sprite's origin moved by the margin.
    private readonly Vector2 _origin;

    /// <param name="width">The part's sprite width in pixels.</param>
    /// <param name="height">The part's sprite height in pixels.</param>
    /// <param name="origin">The part's grab point in sprite pixels, as the sprite is drawn.</param>
    /// <param name="isPart">Whether sprite pixel (x, y) belongs to the part.</param>
    /// <param name="margin">How many pixels around the part also take it.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size or the margin is negative.</exception>
    public GrabShape(int width, int height, Vector2 origin, Func<int, int, bool> isPart, int margin)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(margin);
        ArgumentNullException.ThrowIfNull(isPart);
        Margin = margin;
        _width = width + (2 * margin);
        _height = height + (2 * margin);
        _origin = origin + new Vector2(margin);
        _pixels = new bool[_width * _height];
        var reach = margin * margin;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!isPart(x, y))
                {
                    continue;
                }

                // Every shape pixel within the margin of this part pixel, a disc around it.
                for (var dy = -margin; dy <= margin; dy++)
                {
                    for (var dx = -margin; dx <= margin; dx++)
                    {
                        if ((dx * dx) + (dy * dy) <= reach)
                        {
                            _pixels[((y + margin + dy) * _width) + x + margin + dx] = true;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// How many pixels around the part also take it.
    /// </summary>
    public int Margin { get; }

    /// <summary>
    /// Whether the point <paramref name="offset"/> away from the part's grab point lies on the shape.
    /// </summary>
    public bool Contains(Vector2 offset)
    {
        var local = offset + _origin;
        var x = (int)MathF.Floor(local.X);
        var y = (int)MathF.Floor(local.Y);
        return (uint)x < (uint)_width && (uint)y < (uint)_height && _pixels[(y * _width) + x];
    }
}
