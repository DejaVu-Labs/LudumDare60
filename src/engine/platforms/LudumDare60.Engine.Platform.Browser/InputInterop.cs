using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/input.js</c>, which ships as <c>_content/LudumDare60.Engine.Platform.Browser/input.js</c>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class InputInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/input";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, like <see cref="FrameLoopInterop.ModuleUrl"/>.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/input.js";

    /// <summary>
    /// Loads the module. Bindings in this class work only after the returned task completes.
    /// </summary>
    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
    }

    /// <summary>
    /// Starts listening to the window and to the canvas with id <paramref name="canvasId"/>.
    /// </summary>
    /// <exception cref="JSException">The page has no such canvas, or input is already attached.</exception>
    [JSImport("attach", ModuleName)]
    public static partial void Attach(string canvasId);

    /// <summary>
    /// Stops listening. Does nothing when input is not attached.
    /// </summary>
    [JSImport("detach", ModuleName)]
    public static partial void Detach();

    /// <summary>
    /// Moves the queued events into <paramref name="destination"/> as <see cref="RawInputEvent"/> bytes, oldest
    /// first. Events that do not fit stay queued for the next call.
    /// </summary>
    /// <param name="destination">Room for <c>destination.Length / RawInputEvent.Size</c> events.</param>
    /// <param name="frameTimeMs">Timestamp of the current animation frame; event times are relative to it.</param>
    /// <returns>Number of events written.</returns>
    [JSImport("drain", ModuleName)]
    public static partial int Drain([JSMarshalAs<JSType.MemoryView>] Span<byte> destination, double frameTimeMs);
}
