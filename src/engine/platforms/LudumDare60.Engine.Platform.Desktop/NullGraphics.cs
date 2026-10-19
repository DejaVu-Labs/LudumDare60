namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// <see cref="IGraphicsBackend"/> that draws nothing: the desktop platform has no renderer yet, so the window stays
/// empty while the game runs.
/// </summary>
internal sealed class NullGraphics : IGraphicsBackend
{
    public void CreateTexture(int id, int width, int height, ReadOnlySpan<byte> pixels)
    {
    }

    public void UpdateTexture(int id, int y, int height, ReadOnlySpan<byte> rows)
    {
    }

    public void DestroyTexture(int id)
    {
    }

    public void Submit(ReadOnlySpan<byte> commands, ReadOnlySpan<SpriteInstance> instances)
    {
    }
}
