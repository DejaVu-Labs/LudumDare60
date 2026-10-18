using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/reporting.js</c>, which ships as <c>_content/LudumDare60.Engine.Platform.Browser/reporting.js</c>.
/// <c>boot.js</c> imports the same module and starts reporting before .NET runs, so these calls reach the page's
/// reporter.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class ReportingInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/reporting";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, like <see cref="FrameLoopInterop.ModuleUrl"/>.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/reporting.js";

    /// <summary>
    /// Whether <see cref="ImportAsync"/> finished. <see cref="Capture"/> works only after it.
    /// </summary>
    public static bool IsImported { get; private set; }

    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
        IsImported = true;
    }

    /// <summary>
    /// Sends a .NET error to Sentry, if the page reports at all. See <see cref="ErrorReport"/> for the parameters.
    /// </summary>
    [JSImport("captureManaged", ModuleName)]
    public static partial void Capture(string type, string message, string? stackTrace, string details, bool handled);
}
