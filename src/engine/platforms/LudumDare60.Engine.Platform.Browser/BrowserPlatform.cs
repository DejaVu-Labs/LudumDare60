using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IPlatform"/> for the browser (WebAssembly). Frames come from <c>requestAnimationFrame</c>; each frame
/// costs one JS-to-.NET call and one .NET-to-JS call that drains input.
/// </summary>
/// <param name="canvasId">Id of the page's <c>&lt;canvas&gt;</c>. The game draws to it with WebGL2, and pointer, wheel and
/// resize input come from it.</param>
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

    private readonly BrowserGraphics _graphics = new();

    /// <inheritdoc />
    /// <remarks>WebGL2 on the canvas. <see cref="RunAsync"/> creates the context before the first frame.</remarks>
    public IGraphicsBackend Graphics => _graphics;

    private readonly BrowserAudio _audio = new();

    /// <inheritdoc />
    /// <remarks>Web Audio. <see cref="RunAsync"/> creates the context before the first frame. Browsers keep it
    /// suspended until the player's first click or key press, and the platform suspends it while the page is
    /// hidden.</remarks>
    public IAudioBackend Audio => _audio;

    private static bool s_reportsUnhandledExceptions;

    /// <inheritdoc />
    /// <remarks>The returned task completes only if a frame throws; it then faults with that exception. It also faults
    /// before the first frame if the browser has no WebGL2. Input listeners are attached before the first frame and
    /// removed when the loop ends. From the start of the run on, exceptions that no code catches
    /// (<see cref="AppDomain.UnhandledException"/>, <see cref="TaskScheduler.UnobservedTaskException"/>) are reported
    /// with <see cref="ReportError"/>.</remarks>
    public async Task RunAsync(IFrameSink frameSink)
    {
        ArgumentNullException.ThrowIfNull(frameSink);

        await ReportingInterop.ImportAsync();
        ReportUnhandledExceptions();
        await _graphics.InitializeAsync(canvasId);
        await _audio.InitializeAsync();
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

    /// <inheritdoc />
    /// <remarks>Sends the error to <c>reporting.js</c>, which forwards it to Sentry when the page has a reporting
    /// config. Errors reported before <see cref="RunAsync"/> imported the module are dropped.</remarks>
    public void ReportError(Exception exception, bool handled)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Report(exception, handled);
    }

    private static void Report(Exception exception, bool handled)
    {
        if (!ReportingInterop.IsImported)
        {
            return;
        }

        var report = ErrorReport.From(exception);
        try
        {
            ReportingInterop.Capture(report.Type, report.Message, report.StackTrace, report.Details, handled);
        }
        catch (JSException)
        {
            // Reporting must never take the game down.
        }
    }

    private static void ReportUnhandledExceptions()
    {
        if (s_reportsUnhandledExceptions)
        {
            return;
        }

        s_reportsUnhandledExceptions = true;
        AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Report(exception, handled: false);
            }
        };
        TaskScheduler.UnobservedTaskException += static (_, e) => Report(e.Exception, handled: false);
    }
}
