using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Rendering;

/// <summary>
/// Where a low-resolution virtual screen lands on the drawing surface, and how surface coordinates map back to it.
/// The virtual screen is scaled by the largest whole number of device pixels per virtual pixel that fits, centered,
/// with black bars around it. Only a surface too small for scale 1 gets a fractional scale, so the whole screen stays
/// visible.
/// </summary>
/// <remarks>
/// Create one with <see cref="Fit"/> from a <see cref="Platform.RawInputEventKind.Resize"/> event. The renderer sizes
/// the canvas to <see cref="SurfaceWidth"/> x <see cref="SurfaceHeight"/> device pixels and draws the virtual screen
/// into the rectangle <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, <see cref="Height"/>.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PixelViewport
{
    /// <summary>
    /// Upper bound for a surface side in device pixels. Browsers cap canvases well below it.
    /// </summary>
    public const int MaxSurfaceSize = 1 << 16;

    private PixelViewport(
        int virtualWidth,
        int virtualHeight,
        float pixelRatio,
        int surfaceWidth,
        int surfaceHeight,
        float scale,
        int x,
        int y,
        int width,
        int height)
    {
        VirtualWidth = virtualWidth;
        VirtualHeight = virtualHeight;
        PixelRatio = pixelRatio;
        SurfaceWidth = surfaceWidth;
        SurfaceHeight = surfaceHeight;
        Scale = scale;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width of the virtual screen in virtual pixels.
    /// </summary>
    public int VirtualWidth { get; }

    /// <inheritdoc cref="VirtualWidth"/>
    public int VirtualHeight { get; }

    /// <summary>
    /// Device pixels per logical (CSS) pixel of the surface.
    /// </summary>
    public float PixelRatio { get; }

    /// <summary>
    /// Width of the drawing surface in device pixels: the size the canvas must have.
    /// </summary>
    public int SurfaceWidth { get; }

    /// <inheritdoc cref="SurfaceWidth"/>
    public int SurfaceHeight { get; }

    /// <summary>
    /// Device pixels per virtual pixel. A whole number unless the surface is smaller than the virtual screen.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Left edge of the virtual screen on the surface, in device pixels from the left.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Top edge of the virtual screen on the surface, in device pixels from the top.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Width of the virtual screen on the surface, in device pixels.
    /// </summary>
    public int Width { get; }

    /// <inheritdoc cref="Width"/>
    public int Height { get; }

    /// <summary>
    /// Fits a virtual screen of <paramref name="virtualWidth"/> x <paramref name="virtualHeight"/> into a surface of
    /// <paramref name="surfaceWidth"/> x <paramref name="surfaceHeight"/> logical pixels.
    /// </summary>
    /// <param name="virtualWidth">Virtual screen width, at least 1.</param>
    /// <param name="virtualHeight">Virtual screen height, at least 1.</param>
    /// <param name="surfaceWidth">Surface width in logical pixels (<see cref="Platform.RawInputEvent.X"/> of a resize
    /// event). Negative or NaN counts as zero.</param>
    /// <param name="surfaceHeight">Surface height in logical pixels (<see cref="Platform.RawInputEvent.Y"/>).</param>
    /// <param name="pixelRatio">Device pixels per logical pixel (<see cref="Platform.RawInputEvent.Z"/>). Zero,
    /// negative or NaN counts as 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">The virtual size is not positive.</exception>
    public static PixelViewport Fit(int virtualWidth, int virtualHeight, float surfaceWidth, float surfaceHeight, float pixelRatio)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(virtualWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(virtualHeight, 1);

        var ratio = pixelRatio > 0 && float.IsFinite(pixelRatio) ? pixelRatio : 1;
        var deviceWidth = ToDevicePixels(surfaceWidth, ratio);
        var deviceHeight = ToDevicePixels(surfaceHeight, ratio);

        var wholeScale = Math.Min(deviceWidth / virtualWidth, deviceHeight / virtualHeight);
        int width;
        int height;
        float scale;
        if (wholeScale >= 1)
        {
            scale = wholeScale;
            width = virtualWidth * wholeScale;
            height = virtualHeight * wholeScale;
        }
        else
        {
            scale = Math.Min((float)deviceWidth / virtualWidth, (float)deviceHeight / virtualHeight);
            if (scale <= 0)
            {
                // An empty surface shows nothing; any scale keeps the mapping finite.
                scale = 1;
            }

            width = Math.Clamp((int)MathF.Round(virtualWidth * scale), 1, Math.Max(deviceWidth, 1));
            height = Math.Clamp((int)MathF.Round(virtualHeight * scale), 1, Math.Max(deviceHeight, 1));
        }

        return new PixelViewport(
            virtualWidth,
            virtualHeight,
            ratio,
            deviceWidth,
            deviceHeight,
            scale,
            (deviceWidth - width) / 2,
            (deviceHeight - height) / 2,
            width,
            height);
    }

    /// <summary>
    /// Converts a surface position in logical pixels (pointer events) to virtual pixels. Positions on the bars map
    /// outside [0, <see cref="VirtualWidth"/>) x [0, <see cref="VirtualHeight"/>): check with <see cref="Contains"/>.
    /// </summary>
    public Vector2 ToVirtual(float surfaceX, float surfaceY) => new(
        ((surfaceX * PixelRatio) - X) * VirtualWidth / Width,
        ((surfaceY * PixelRatio) - Y) * VirtualHeight / Height);

    /// <summary>
    /// Whether a position in virtual pixels lies on the virtual screen.
    /// </summary>
    public bool Contains(Vector2 position)
        => position.X >= 0 && position.X < VirtualWidth && position.Y >= 0 && position.Y < VirtualHeight;

    private static int ToDevicePixels(float logical, float ratio)
    {
        var device = logical * ratio;
        return device > 0 && float.IsFinite(device) ? (int)MathF.Round(MathF.Min(device, MaxSurfaceSize)) : 0;
    }
}
