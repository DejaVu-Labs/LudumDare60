using System.Numerics;
using LudumDare60.Engine.Assets;

namespace LudumDare60.Game.Cleaning;

/// <summary>
/// Dirt or rust on one part: an RGBA layer drawn over the part's sprite, at its size, whose opaque pixels are the
/// dirt. Scrubbing erases them with a slightly dithered round brush, and the layer counts exactly how many are left.
/// Past <see cref="CleanThreshold"/> the part counts as clean, so the player never hunts for the last pixel; the rest
/// is for the game to fade out.
/// </summary>
public sealed class DirtLayer
{
    private readonly byte[] _pixels;
    private int _changedTop = int.MaxValue;
    private int _changedBottom = -1;

    /// <param name="dirt">The dirt's pixels: every pixel with alpha above 0 is dirt. The layer copies them.</param>
    /// <param name="cleanThreshold">The share of the dirt, in (0, 1], that must be erased for the part to be clean.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cleanThreshold"/> is outside (0, 1].</exception>
    public DirtLayer(Image dirt, float cleanThreshold = 0.9f)
    {
        ArgumentNullException.ThrowIfNull(dirt);
        if (cleanThreshold is not (> 0 and <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(cleanThreshold), cleanThreshold, "The threshold must lie in (0, 1].");
        }

        _pixels = dirt.Pixels.ToArray();
        Image = new Image(dirt.Width, dirt.Height, _pixels);
        CleanThreshold = cleanThreshold;
        for (var i = Image.BytesPerPixel - 1; i < _pixels.Length; i += Image.BytesPerPixel)
        {
            if (_pixels[i] != 0)
            {
                InitialDirt++;
            }
        }

        Dirt = InitialDirt;
        IsClean = InitialDirt == 0;
    }

    /// <summary>
    /// The layer's current pixels, for <see cref="Engine.Rendering.Graphics.CreateTexture"/> and
    /// <see cref="Engine.Rendering.Graphics.UpdateTexture"/>. Scrubbing changes them in place.
    /// </summary>
    public Image Image { get; }

    public float CleanThreshold { get; }

    /// <summary>
    /// Dirt pixels at the start.
    /// </summary>
    public int InitialDirt { get; }

    /// <summary>
    /// Dirt pixels left.
    /// </summary>
    public int Dirt { get; private set; }

    /// <summary>
    /// The share of the dirt erased so far, from 0 to 1.
    /// </summary>
    public float Cleanliness => InitialDirt == 0 ? 1 : 1 - (Dirt / (float)InitialDirt);

    /// <summary>
    /// Whether <see cref="Cleanliness"/> has reached <see cref="CleanThreshold"/>. From then on scrubbing erases nothing.
    /// </summary>
    public bool IsClean { get; private set; }

    /// <summary>
    /// Rubs the brush from <paramref name="from"/> to <paramref name="to"/>, in layer pixels (pixel <c>(x, y)</c>
    /// covers <c>[x, x + 1)</c> by <c>[y, y + 1)</c>), stamping it every pixel along the way. The brush erases every
    /// pixel whose center lies within <c>radius - 0.5</c>, and every other one of those within <c>radius + 0.5</c>.
    /// Parts of the brush outside the layer erase nothing.
    /// </summary>
    /// <returns>How many dirt pixels were erased.</returns>
    public int Scrub(Vector2 from, Vector2 to, float radius)
    {
        if (IsClean)
        {
            return 0;
        }

        var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(from, to)));
        var erased = 0;
        for (var i = 0; i <= steps; i++)
        {
            erased += Stamp(Vector2.Lerp(from, to, i / (float)steps), radius);
        }

        Dirt -= erased;
        IsClean = Cleanliness >= CleanThreshold;
        return erased;
    }

    /// <summary>
    /// The rows changed since the last call, as one range, for <see cref="Engine.Rendering.Graphics.UpdateTexture"/>.
    /// </summary>
    /// <returns>False when nothing changed.</returns>
    public bool TryTakeChangedRows(out int firstRow, out int rowCount)
    {
        if (_changedBottom < 0)
        {
            firstRow = rowCount = 0;
            return false;
        }

        firstRow = _changedTop;
        rowCount = _changedBottom - _changedTop + 1;
        _changedTop = int.MaxValue;
        _changedBottom = -1;
        return true;
    }

    private int Stamp(Vector2 center, float radius)
    {
        var inner = MathF.Max(0, radius - 0.5f);
        var outer = radius + 0.5f;
        var left = Math.Max(0, (int)MathF.Floor(center.X - outer));
        var right = Math.Min(Image.Width - 1, (int)MathF.Floor(center.X + outer));
        var top = Math.Max(0, (int)MathF.Floor(center.Y - outer));
        var bottom = Math.Min(Image.Height - 1, (int)MathF.Floor(center.Y + outer));
        var erased = 0;
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var distanceSquared = Vector2.DistanceSquared(new Vector2(x + 0.5f, y + 0.5f), center);
                var hit = distanceSquared <= inner * inner || (distanceSquared <= outer * outer && ((x + y) & 1) == 0);
                if (hit && Erase(x, y))
                {
                    erased++;
                }
            }
        }

        return erased;
    }

    private bool Erase(int x, int y)
    {
        var i = ((y * Image.Width) + x) * Image.BytesPerPixel;
        if (_pixels[i + 3] == 0)
        {
            return false;
        }

        _pixels.AsSpan(i, Image.BytesPerPixel).Clear();
        _changedTop = Math.Min(_changedTop, y);
        _changedBottom = Math.Max(_changedBottom, y);
        return true;
    }
}
