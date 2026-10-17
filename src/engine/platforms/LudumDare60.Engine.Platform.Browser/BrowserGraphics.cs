using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IGraphicsBackend"/> on WebGL2 (<c>wwwroot/webgl2.js</c>). Each method is one interop call.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class BrowserGraphics : IGraphicsBackend
{
    private bool _initialized;

    /// <summary>
    /// Loads the module and creates the context on the canvas with id <paramref name="canvasId"/>.
    /// </summary>
    public async Task InitializeAsync(string canvasId)
    {
        await GraphicsInterop.ImportAsync();
        GraphicsInterop.Init(canvasId);
        _initialized = true;
    }

    public void CreateTexture(int id, int width, int height, ReadOnlySpan<byte> pixels)
    {
        EnsureInitialized();
        GraphicsInterop.CreateTexture(id, width, height, AsWritable(pixels));
    }

    public void DestroyTexture(int id)
    {
        EnsureInitialized();
        GraphicsInterop.DestroyTexture(id);
    }

    public void Submit(ReadOnlySpan<byte> commands, ReadOnlySpan<SpriteInstance> instances)
    {
        EnsureInitialized();
        GraphicsInterop.Submit(AsWritable(commands), AsWritable(MemoryMarshal.AsBytes(instances)));
    }

    /// <summary>
    /// <c>[JSImport]</c> marshals only <see cref="Span{T}"/> as a memory view. The JS side only reads these spans.
    /// </summary>
    private static Span<byte> AsWritable(ReadOnlySpan<byte> span)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("The GPU is set up when the frame loop starts (BrowserPlatform.RunAsync).");
        }
    }
}
