using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Bindings to <c>wwwroot/webaudio.js</c>, which ships as <c>_content/LudumDare60.Engine.Platform.Browser/webaudio.js</c>.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class AudioInterop
{
    public const string ModuleName = "LudumDare60.Engine.Platform.Browser/webaudio";

    /// <summary>
    /// Resolved relative to <c>_framework/dotnet.runtime.js</c>, like <see cref="FrameLoopInterop.ModuleUrl"/>.
    /// </summary>
    public const string ModuleUrl = "../_content/LudumDare60.Engine.Platform.Browser/webaudio.js";

    /// <summary>
    /// Loads the module. Bindings in this class work only after the returned task completes.
    /// </summary>
    public static async Task ImportAsync()
    {
        using var module = await JSHost.ImportAsync(ModuleName, ModuleUrl);
    }

    /// <summary>
    /// Creates the <c>AudioContext</c> and its buses, and listens for the first user gesture (which unlocks audio) and
    /// for the page being hidden (which suspends it). Calling it again keeps the existing context. Without Web Audio the
    /// module stays silent instead of failing.
    /// </summary>
    [JSImport("init", ModuleName)]
    public static partial void Init();

    /// <inheritdoc cref="IAudioBackend.CreateSound"/>
    [JSImport("createSound", ModuleName)]
    public static partial void CreateSound(int id, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    /// <inheritdoc cref="IAudioBackend.DestroySound"/>
    [JSImport("destroySound", ModuleName)]
    public static partial void DestroySound(int id);

    /// <summary>
    /// Runs one frame of audio commands and writes the queued events into <paramref name="events"/> as
    /// <see cref="AudioEvent"/> bytes.
    /// </summary>
    /// <returns>Number of events written.</returns>
    /// <exception cref="JSException">The stream is malformed.</exception>
    [JSImport("submit", ModuleName)]
    public static partial int Submit(
        [JSMarshalAs<JSType.MemoryView>] Span<byte> commands,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> events);
}
