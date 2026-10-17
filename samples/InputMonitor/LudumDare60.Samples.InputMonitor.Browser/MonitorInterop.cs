using System.Runtime.InteropServices.JavaScript;

namespace LudumDare60.Samples.InputMonitor.Browser;

/// <summary>
/// Bindings to <c>wwwroot/monitor.js</c>.
/// </summary>
internal static partial class MonitorInterop
{
    public const string ModuleName = "LudumDare60.Samples.InputMonitor.Browser/monitor";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>; the import map redirects it to the fingerprinted file.
    /// </summary>
    public const string ModuleUrl = "../monitor.js";

    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
    }

    /// <summary>
    /// Replaces the text of the page's monitor element.
    /// </summary>
    /// <param name="utf8">UTF-8 text. JS only reads it.</param>
    [JSImport("show", ModuleName)]
    public static partial void Show([JSMarshalAs<JSType.MemoryView>] Span<byte> utf8);
}
