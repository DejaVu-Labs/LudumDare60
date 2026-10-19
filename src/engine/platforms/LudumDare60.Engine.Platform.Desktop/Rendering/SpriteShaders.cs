namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// The compiled sprite shaders (SPIR-V), embedded in the assembly from <c>Shaders/*.spv</c>.
/// </summary>
internal static class SpriteShaders
{
    public const string Vertex = "sprite.vert.spv";

    public const string Fragment = "sprite.frag.spv";

    /// <summary>
    /// Reads the embedded shader <paramref name="name"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No shader of that name is embedded.</exception>
    public static byte[] Load(string name)
    {
        using var stream = typeof(SpriteShaders).Assembly.GetManifestResourceStream("Shaders/" + name)
            ?? throw new InvalidOperationException($"The shader '{name}' is not embedded in the assembly.");
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return data;
    }
}
