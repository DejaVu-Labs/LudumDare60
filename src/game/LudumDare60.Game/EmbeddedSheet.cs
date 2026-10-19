using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Game;

/// <summary>
/// A sprite sheet from <c>art/sprites/</c> embedded in the assembly (see <see cref="PlaceholderParts"/> for where each
/// piece lies). Embedded rather than fetched, so the pieces' shapes (grab areas, glows, dirt) are known before the first
/// frame, like the rest of the parts sheet. Each is decoded once, on first use.
/// </summary>
internal sealed class EmbeddedSheet
{
    /// <summary>
    /// <c>revolver.png</c>: the revolver's body on top, the parts that move in a row below it.
    /// </summary>
    public static readonly EmbeddedSheet Revolver = new("revolver.png");

    /// <summary>
    /// <c>tools.png</c>: the oil can on the left, the brush on the right.
    /// </summary>
    public static readonly EmbeddedSheet Tools = new("tools.png");

    private readonly Image _image;

    private EmbeddedSheet(string resourceName)
    {
        ResourceName = resourceName;
        _image = Load(resourceName);
    }

    /// <summary>
    /// The manifest name of the embedded file (<c>LogicalName</c> in the project file).
    /// </summary>
    public string ResourceName { get; }

    public int Width => _image.Width;

    public int Height => _image.Height;

    /// <summary>
    /// The color of sheet pixel (<paramref name="x"/>, <paramref name="y"/>), not premultiplied.
    /// </summary>
    public Color Get(int x, int y)
    {
        var i = (y * _image.Stride) + (x * Image.BytesPerPixel);
        var pixels = _image.Pixels;
        return new Color(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    private static Image Load(string resourceName)
    {
        using var stream = typeof(EmbeddedSheet).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' is missing.");
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return new ImageLoader().Load(resourceName, data);
    }
}
