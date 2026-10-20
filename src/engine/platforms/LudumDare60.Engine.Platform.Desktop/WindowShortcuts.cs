using SDL3;

namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// Keys the desktop platform handles itself, because the game starts fullscreen without a window frame: F11 or
/// Alt+Enter toggles fullscreen (keyboards like the Pixelbook's end at F10), and Alt+F4 quits. The game still receives
/// these keys.
/// </summary>
internal static class WindowShortcuts
{
    public enum Action
    {
        None,
        ToggleFullscreen,
        Quit,
    }

    /// <returns>What the key event asks the window to do.</returns>
    public static Action Get(in SDL.KeyboardEvent e)
    {
        if (!e.Down || e.Repeat)
        {
            return Action.None;
        }

        return e.Scancode switch
        {
            SDL.Scancode.F11 => Action.ToggleFullscreen,
            SDL.Scancode.Return or SDL.Scancode.KpEnter when (e.Mod & SDL.Keymod.Alt) != SDL.Keymod.None => Action.ToggleFullscreen,
            SDL.Scancode.F4 when (e.Mod & SDL.Keymod.Alt) != SDL.Keymod.None => Action.Quit,
            _ => Action.None,
        };
    }
}
