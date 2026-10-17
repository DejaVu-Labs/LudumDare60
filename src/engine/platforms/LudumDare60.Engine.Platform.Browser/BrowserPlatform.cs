using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IPlatform"/> for the browser (WebAssembly). Frames come from <c>requestAnimationFrame</c>; each frame
/// costs one JS-to-.NET call and one .NET-to-JS call that drains input.
/// </summary>
/// <param name="canvasId">Id of the page's <c>&lt;canvas&gt;</c>. Pointer, wheel and resize input come from it.</param>
[SupportedOSPlatform("browser")]
public sealed class BrowserPlatform(string canvasId = BrowserPlatform.DefaultCanvasId) : IPlatform
{
    /// <summary>
    /// Canvas id the host page uses unless it says otherwise.
    /// </summary>
    public const string DefaultCanvasId = "screen";

    /// <inheritdoc />
    /// <remarks>The returned task completes only if a frame throws; it then faults with that exception. Input
    /// listeners are attached before the first frame and removed when the loop ends.</remarks>
    public async Task RunAsync(IFrameSink frameSink)
    {
        ArgumentNullException.ThrowIfNull(frameSink);

        await FrameLoopInterop.ImportAsync();
        await InputInterop.ImportAsync();

        InputInterop.Attach(canvasId);
        try
        {
            await FrameLoopInterop.RunAsync(new FrameDispatcher(frameSink, InputInterop.Drain).OnFrame);
        }
        finally
        {
            InputInterop.Detach();
        }
    }
}
