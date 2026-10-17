using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IPlatform"/> for the browser (WebAssembly). Frames come from <c>requestAnimationFrame</c>; each frame
/// costs one JS-to-.NET call and one .NET-to-JS call that drains input.
/// </summary>
/// <param name="canvasId">Id of the page's <c>&lt;canvas&gt;</c>. Pointer, wheel and resize input come from it.</param>
/// <param name="assetRoot">Folder of the asset files, relative to the page.</param>
[SupportedOSPlatform("browser")]
public sealed class BrowserPlatform(
    string canvasId = BrowserPlatform.DefaultCanvasId,
    string assetRoot = BrowserPlatform.DefaultAssetRoot) : IPlatform
{
    /// <summary>
    /// Canvas id the host page uses unless it says otherwise.
    /// </summary>
    public const string DefaultCanvasId = "screen";

    /// <summary>
    /// Asset folder the host page uses unless it says otherwise: <c>wwwroot/assets/</c> of the app.
    /// </summary>
    public const string DefaultAssetRoot = "assets/";

    /// <inheritdoc />
    /// <remarks>Release builds add the commit hash to every asset URL (<c>?v=</c>), so players never get a file from
    /// an older build out of the browser or CDN cache. Debug builds revalidate every read instead, so an edited file
    /// shows up on reload even without a commit.</remarks>
    public IAssetSource Assets { get; } = new BrowserAssetSource(
        assetRoot,
#if DEBUG
        version: null,
        noCache: true,
#else
        version: BuildInfo.SourceRevision,
        noCache: false,
#endif
        fetch: static async (id, url, noCache) =>
        {
            await AssetsInterop.EnsureImportedAsync();
            return await AssetsInterop.ReadAsync(id, url, noCache);
        },
        take: AssetsInterop.Take);

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
