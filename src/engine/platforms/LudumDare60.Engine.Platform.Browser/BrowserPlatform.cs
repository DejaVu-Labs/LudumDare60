using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IPlatform"/> for the browser (WebAssembly). Frames come from <c>requestAnimationFrame</c>, one
/// JS-to-.NET call per frame.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserPlatform : IPlatform
{
    /// <inheritdoc />
    /// <remarks>The returned task completes only if a frame throws; it then faults with that exception.</remarks>
    public async Task RunAsync(IFrameSink frameSink)
    {
        ArgumentNullException.ThrowIfNull(frameSink);

        await FrameLoopInterop.ImportAsync();
        await FrameLoopInterop.RunAsync(new FrameDispatcher(frameSink).OnFrame);
    }
}
