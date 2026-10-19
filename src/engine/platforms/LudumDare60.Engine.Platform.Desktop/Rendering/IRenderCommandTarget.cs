namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// Runs the commands that <see cref="RenderCommandReader"/> reads, one call per command (see
/// <see cref="RenderCommandKind"/> for what each does).
/// </summary>
internal interface IRenderCommandTarget
{
    void SetSurface(int surfaceWidth, int surfaceHeight, int virtualWidth, int virtualHeight, PixelRect viewport);

    void Clear(Color color);

    void DrawSprites(int textureId, int firstInstance, int instanceCount);

    void Present();
}
