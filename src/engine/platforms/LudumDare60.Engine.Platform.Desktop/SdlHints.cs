using SDL3;

namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// The SDL hints the desktop platform sets before <c>SDL_Init</c>. Hints are defaults: an environment variable of the
/// same name still wins.
/// </summary>
internal static class SdlHints
{
    /// <summary>
    /// Set by sommelier, the Wayland proxy of ChromeOS's Linux container (Crostini).
    /// </summary>
    public const string SommelierVariable = "SOMMELIER_VERSION";

    /// <summary>
    /// The hints for this environment, as SDL hint name and value.
    /// </summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable; null when it is not set.</param>
    public static List<KeyValuePair<string, string>> Get(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        // Touches arrive as finger events; SDL's mouse copies of them would report each touch twice.
        var hints = new List<KeyValuePair<string, string>> { new(SDL.Hints.TouchMouseEvents, "0") };
        if (!string.IsNullOrEmpty(getEnvironmentVariable(SommelierVariable)))
        {
            // On XWayland, where SDL goes when the compositor lacks fifo-v1, the Venus Vulkan driver gets no swapchain.
            hints.Add(new(SDL.Hints.VideoDriver, "wayland"));

            // sommelier draws the client-side title bar of libdecor above the window and shifts the picture down by
            // its height, while pointer positions stay relative to the top of the picture: every hit lands too high.
            // Without libdecor the window has no frame there at all, which the game lives with by starting fullscreen
            // (F11 or Alt+Enter for a window, Alt+F4 to quit: see WindowShortcuts).
            hints.Add(new(SDL.Hints.VideoWaylandAllowLibdecor, "0"));
        }

        return hints;
    }
}
