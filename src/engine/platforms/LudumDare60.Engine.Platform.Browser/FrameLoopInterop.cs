using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/frame-loop.js</c>, which ships as
/// <c>_content/LudumDare60.Engine.Platform.Browser/frame-loop.js</c>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class FrameLoopInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/frame-loop";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, which performs the import. The host page's import
    /// map redirects this URL to the fingerprinted file.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/frame-loop.js";

    /// <summary>
    /// Loads the module. Bindings in this class work only after the returned task completes.
    /// </summary>
    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
    }

    /// <summary>
    /// Calls <paramref name="onFrame"/> from <c>requestAnimationFrame</c> once per frame, with the seconds since the
    /// previous frame (0 on the first frame).
    /// </summary>
    /// <returns>A task that faults with the exception thrown by <paramref name="onFrame"/>.</returns>
    [JSImport("run", ModuleName)]
    public static partial Task RunAsync([JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> onFrame);
}
