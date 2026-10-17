namespace LudumDare60.Engine.Assets;

/// <summary>
/// A decoded image in main memory: 8-bit RGBA pixels, row by row from the top-left corner, with straight (not
/// premultiplied) alpha. This is the layout <c>rgba8unorm</c> textures expect, so the pixels upload as they are.
/// </summary>
/// <remarks>
/// A loaded image is shared by every handle to its path, so its pixels are read-only.
/// </remarks>
public sealed class Image
{
    /// <summary>
    /// Bytes per pixel: red, green, blue and alpha.
    /// </summary>
    public const int BytesPerPixel = 4;

    private readonly byte[] _pixels;

    /// <param name="width">Width in pixels, at least 1.</param>
    /// <param name="height">Height in pixels, at least 1.</param>
    /// <param name="pixels">Exactly <c>width * height * 4</c> bytes in the layout described on <see cref="Image"/>. The
    /// image keeps the array without copying it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than
    /// 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> has the wrong length.</exception>
    public Image(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);

        var expected = (long)width * height * BytesPerPixel;
        if (pixels.LongLength != expected)
        {
            throw new ArgumentException(
                $"A {width}x{height} image needs {expected} bytes of RGBA pixels, not {pixels.LongLength}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// Bytes per row: <c>Width * 4</c>. Rows are tightly packed.
    /// </summary>
    public int Stride => Width * BytesPerPixel;

    /// <summary>
    /// All pixels, <see cref="Height"/> rows of <see cref="Stride"/> bytes.
    /// </summary>
    public ReadOnlySpan<byte> Pixels => _pixels;
}
