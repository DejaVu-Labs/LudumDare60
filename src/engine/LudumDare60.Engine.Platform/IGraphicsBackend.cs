namespace LudumDare60.Engine.Platform;

/// <summary>
/// A platform's GPU. The engine creates textures through it and submits one command stream per frame. The engine never
/// calls it before the platform's first frame, so a backend can set up its device in <see cref="IPlatform.RunAsync"/>.
/// </summary>
public interface IGraphicsBackend
{
    /// <summary>
    /// Creates texture <paramref name="id"/> and uploads its pixels.
    /// </summary>
    /// <param name="id">Chosen by the engine: zero or more, unused until <see cref="DestroyTexture"/> frees it.</param>
    /// <param name="width">Width in texels, at least 1.</param>
    /// <param name="height">Height in texels, at least 1.</param>
    /// <param name="pixels"><c>width * height * 4</c> bytes: 8-bit RGBA, straight alpha, rows from the top, tightly
    /// packed. Read during the call only.</param>
    void CreateTexture(int id, int width, int height, ReadOnlySpan<byte> pixels);

    /// <summary>
    /// Replaces whole rows of texture <paramref name="id"/>'s pixels, for textures that change while the game runs.
    /// </summary>
    /// <param name="id">A live texture.</param>
    /// <param name="y">The first row to replace, from the top.</param>
    /// <param name="height">How many rows, at least 1; <c>y + height</c> is at most the texture's height.</param>
    /// <param name="rows"><c>textureWidth * height * 4</c> bytes in the layout of <see cref="CreateTexture"/>. Read
    /// during the call only.</param>
    void UpdateTexture(int id, int y, int height, ReadOnlySpan<byte> rows);

    /// <summary>
    /// Deletes texture <paramref name="id"/>. The id may be created again afterwards.
    /// </summary>
    void DestroyTexture(int id);

    /// <summary>
    /// Runs one frame of render commands.
    /// </summary>
    /// <param name="commands">The frame's commands, see <see cref="RenderCommandKind"/>. Read during the call only.</param>
    /// <param name="instances">The sprite instances the commands refer to by index. Read during the call only.</param>
    void Submit(ReadOnlySpan<byte> commands, ReadOnlySpan<SpriteInstance> instances);
}
