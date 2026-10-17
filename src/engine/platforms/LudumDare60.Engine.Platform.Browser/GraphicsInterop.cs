using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/webgl2.js</c>, which ships as <c>_content/LudumDare60.Engine.Platform.Browser/webgl2.js</c>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class GraphicsInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/webgl2";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, like <see cref="FrameLoopInterop.ModuleUrl"/>.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/webgl2.js";

    /// <summary>
    /// Loads the module. Bindings in this class work only after the returned task completes.
    /// </summary>
    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
    }

    /// <summary>
    /// Creates the WebGL2 context on the canvas with id <paramref name="canvasId"/>. Calling it again with the same
    /// canvas keeps the existing context.
    /// </summary>
    /// <exception cref="JSException">The page has no such canvas, or the browser has no WebGL2. The page's status
    /// element then explains it to the player.</exception>
    [JSImport("init", ModuleName)]
    public static partial void Init(string canvasId);

    /// <inheritdoc cref="IGraphicsBackend.CreateTexture"/>
    [JSImport("createTexture", ModuleName)]
    public static partial void CreateTexture(int id, int width, int height, [JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    /// <inheritdoc cref="IGraphicsBackend.DestroyTexture"/>
    [JSImport("destroyTexture", ModuleName)]
    public static partial void DestroyTexture(int id);

    /// <summary>
    /// Runs one frame of render commands over the frame's sprite instances (as bytes).
    /// </summary>
    /// <exception cref="JSException">The GPU context was lost, or the stream is malformed.</exception>
    [JSImport("submit", ModuleName)]
    public static partial void Submit(
        [JSMarshalAs<JSType.MemoryView>] Span<byte> commands,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> instances);
}
