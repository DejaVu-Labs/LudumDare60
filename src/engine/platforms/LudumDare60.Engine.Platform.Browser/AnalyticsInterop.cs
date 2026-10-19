using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/analytics.js</c>, which ships as <c>_content/LudumDare60.Engine.Platform.Browser/analytics.js</c>.
/// <c>boot.js</c> imports the same module and starts analytics before .NET runs, so these calls reach the page's
/// analytics.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class AnalyticsInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/analytics";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, like <see cref="FrameLoopInterop.ModuleUrl"/>.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/analytics.js";

    /// <summary>
    /// Whether <see cref="ImportAsync"/> finished. The bindings work only after it.
    /// </summary>
    public static bool IsImported { get; private set; }

    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
        IsImported = true;
    }

    /// <summary>
    /// Sends a progression event, if the page has analytics. See <see cref="IAnalytics.Progression"/>.
    /// </summary>
    [JSImport("addProgression", ModuleName)]
    public static partial void AddProgression(int status, string progression01, string? progression02, string? progression03, bool hasScore, int score);

    /// <summary>
    /// Sends a design event, if the page has analytics. See <see cref="IAnalytics.Design"/>.
    /// </summary>
    [JSImport("addDesign", ModuleName)]
    public static partial void AddDesign(string eventId, bool hasValue, double value);
}
