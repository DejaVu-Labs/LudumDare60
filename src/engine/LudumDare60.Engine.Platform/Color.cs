using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform;

/// <summary>
/// An 8-bit RGBA color with straight (not premultiplied) alpha. In memory the bytes are red, green, blue, alpha, the
/// layout of an <c>rgba8unorm</c> texel.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Color(byte R, byte G, byte B, byte A = byte.MaxValue)
{
    /// <summary>
    /// Opaque white: a tint that leaves a sprite unchanged.
    /// </summary>
    public static Color White => new(255, 255, 255);

    /// <summary>
    /// Opaque black.
    /// </summary>
    public static Color Black => new(0, 0, 0);

    /// <summary>
    /// Fully transparent black.
    /// </summary>
    public static Color Transparent => new(0, 0, 0, 0);

    /// <summary>
    /// An opaque color from a <c>0xRRGGBB</c> value, the way palettes write colors (<c>#245c48</c> is
    /// <c>FromRgb(0x245c48)</c>).
    /// </summary>
    public static Color FromRgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    /// <summary>
    /// The same color with alpha <paramref name="alpha"/>.
    /// </summary>
    public Color WithAlpha(byte alpha) => this with { A = alpha };
}
