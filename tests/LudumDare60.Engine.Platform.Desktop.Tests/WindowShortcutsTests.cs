using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class WindowShortcutsTests
{
    private static SDL.KeyboardEvent Key(SDL.Scancode scancode, SDL.Keymod mod = SDL.Keymod.None, bool down = true, bool repeat = false)
    {
        var e = SdlEvents.Key(scancode, down, repeat: repeat).Key;
        e.Mod = mod;
        return e;
    }

    [Fact]
    public void Get_F11_TogglesFullscreen()
        => Assert.Equal(WindowShortcuts.Action.ToggleFullscreen, WindowShortcuts.Get(Key(SDL.Scancode.F11)));

    [Theory]
    [InlineData(SDL.Scancode.Return, SDL.Keymod.LAlt)]
    [InlineData(SDL.Scancode.KpEnter, SDL.Keymod.RAlt)]
    public void Get_AltEnter_TogglesFullscreen(SDL.Scancode scancode, SDL.Keymod mod)
        => Assert.Equal(WindowShortcuts.Action.ToggleFullscreen, WindowShortcuts.Get(Key(scancode, mod)));

    [Fact]
    public void Get_EnterWithoutAlt_DoesNothing()
        => Assert.Equal(WindowShortcuts.Action.None, WindowShortcuts.Get(Key(SDL.Scancode.Return)));

    [Theory]
    [InlineData(SDL.Keymod.LAlt)]
    [InlineData(SDL.Keymod.RAlt)]
    [InlineData(SDL.Keymod.LAlt | SDL.Keymod.Num)]
    public void Get_AltF4_Quits(SDL.Keymod mod)
        => Assert.Equal(WindowShortcuts.Action.Quit, WindowShortcuts.Get(Key(SDL.Scancode.F4, mod)));

    [Fact]
    public void Get_F4WithoutAlt_DoesNothing()
        => Assert.Equal(WindowShortcuts.Action.None, WindowShortcuts.Get(Key(SDL.Scancode.F4, SDL.Keymod.LCtrl)));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Get_ReleaseOrRepeat_DoesNothing(bool down, bool repeat)
        => Assert.Equal(WindowShortcuts.Action.None, WindowShortcuts.Get(Key(SDL.Scancode.F11, down: down, repeat: repeat)));

    [Fact]
    public void Get_OtherKey_DoesNothing()
        => Assert.Equal(WindowShortcuts.Action.None, WindowShortcuts.Get(Key(SDL.Scancode.Escape, SDL.Keymod.LAlt)));
}
