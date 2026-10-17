using System.Text;
using LudumDare60.Engine.Assets;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// Parses files as UTF-8 text and records every load and unload. <see cref="OnLoad"/> can replace the result or
/// throw.
/// </summary>
internal sealed class TextLoader : IAssetLoader<TextAsset>
{
    public List<TextAsset> Loaded { get; } = [];

    public List<TextAsset> Unloaded { get; } = [];

    /// <summary>
    /// Runs inside <see cref="Load"/> with the parsed asset; its return value is what the loader returns.
    /// </summary>
    public Func<TextAsset, TextAsset?>? OnLoad { get; set; }

    public TextAsset Load(string path, byte[] data)
    {
        var asset = new TextAsset(path, Encoding.UTF8.GetString(data));
        var result = OnLoad is null ? asset : OnLoad(asset);
        if (result is not null)
        {
            Loaded.Add(result);
        }

        return result!;
    }

    public void Unload(TextAsset asset) => Unloaded.Add(asset);
}
