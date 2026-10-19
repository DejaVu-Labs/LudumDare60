using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Game;

/// <summary>
/// The revolver's sprites, <c>art/sprites/revolver.png</c>, embedded in the assembly: the body on top, the parts that
/// move in a row below it (see <see cref="PlaceholderParts"/> for where each lies). Embedded rather than fetched, so the
/// parts' shapes (grab areas, glows, dirt) are known before the first frame, like the rest of the parts sheet. Decoded
/// once, on first use.
/// </summary>
internal static class RevolverSheet
{
    /// <summary>
    /// The manifest name of the embedded file (<c>LogicalName</c> in the project file).
    /// </summary>
    public const string ResourceName = "revolver.png";

    private static readonly Image s_image = Load();

    /// <summary>
    /// The color of sheet pixel (<paramref name="x"/>, <paramref name="y"/>), not premultiplied.
    /// </summary>
    public static Color Get(int x, int y)
    {
        var i = (y * s_image.Stride) + (x * Image.BytesPerPixel);
        var pixels = s_image.Pixels;
        return new Color(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    private static Image Load()
    {
        using var stream = typeof(RevolverSheet).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return new ImageLoader().Load(ResourceName, data);
    }
}
