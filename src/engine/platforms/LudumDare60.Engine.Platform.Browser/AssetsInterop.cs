using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/assets.js</c>, which ships as <c>_content/LudumDare60.Engine.Platform.Browser/assets.js</c>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class AssetsInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/assets";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, like <see cref="FrameLoopInterop.ModuleUrl"/>.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/assets.js";

    private static readonly LazyImport s_import = new(ImportAsync);

    /// <summary>
    /// Loads the module on first use, and again after a failed import. A game requests assets before the frame loop
    /// starts, so the import cannot wait for <see cref="BrowserPlatform.RunAsync"/>.
    /// </summary>
    public static Task EnsureImportedAsync() => s_import.EnsureAsync();

    /// <summary>
    /// Fetches <paramref name="url"/> and keeps the bytes in JS under <paramref name="id"/> until <see cref="Take"/>.
    /// </summary>
    /// <param name="id">Unique among reads not taken yet.</param>
    /// <param name="url">Resolved against the page URL.</param>
    /// <param name="noCache">Revalidate with the server instead of trusting the browser cache.</param>
    /// <returns>The byte length, or minus the HTTP status for an error status.</returns>
    /// <exception cref="JSException">A network error.</exception>
    [JSImport("read", ModuleName)]
    [return: JSMarshalAs<JSType.Promise<JSType.Number>>]
    public static partial Task<int> ReadAsync(int id, string url, bool noCache);

    /// <summary>
    /// Copies the bytes of read <paramref name="id"/> into <paramref name="destination"/>, which must have their exact
    /// length, and frees them in JS.
    /// </summary>
    [JSImport("take", ModuleName)]
    public static partial void Take(int id, [JSMarshalAs<JSType.MemoryView>] Span<byte> destination);

    private static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
    }
}
