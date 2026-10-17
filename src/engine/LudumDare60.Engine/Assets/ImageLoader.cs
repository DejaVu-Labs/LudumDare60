using StbImageSharp;

namespace LudumDare60.Engine.Assets;

/// <summary>
/// Decodes image files into an RGBA <see cref="Image"/> with StbImageSharp, a managed port of <c>stb_image</c>. Not
/// registered by default: call <c>assets.Register(new ImageLoader())</c>, so a game without images does not ship the
/// decoder.
/// </summary>
/// <remarks>
/// Use PNG. The decoder also reads JPEG, BMP, TGA, PSD, HDR (reduced to 8 bits) and the first frame of a GIF. Every
/// file becomes 8-bit RGBA whatever its channels: gray is copied to red, green and blue, and a missing alpha channel
/// is opaque. Pixels are not premultiplied, and rows are not flipped.
/// </remarks>
public sealed class ImageLoader : IAssetLoader<Image>
{
    /// <exception cref="InvalidOperationException">The file is not an image the decoder reads, or it is corrupt.</exception>
    public Image Load(string path, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            throw new InvalidOperationException("The file is empty.");
        }

        var result = ImageResult.FromMemory(data, ColorComponents.RedGreenBlueAlpha);
        return new Image(result.Width, result.Height, result.Data);
    }

    public void Unload(Image asset)
    {
    }
}
