using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class KeyMapTests
{
    [Theory]
    [InlineData(SDL.Scancode.A, Key.A)]
    [InlineData(SDL.Scancode.Alpha0, Key.Digit0)]
    [InlineData(SDL.Scancode.Return, Key.Enter)]
    [InlineData(SDL.Scancode.Escape, Key.Escape)]
    [InlineData(SDL.Scancode.Grave, Key.Backquote)]
    [InlineData(SDL.Scancode.F12, Key.F12)]
    [InlineData(SDL.Scancode.Up, Key.ArrowUp)]
    [InlineData(SDL.Scancode.KpEnter, Key.NumpadEnter)]
    [InlineData(SDL.Scancode.LCtrl, Key.ControlLeft)]
    [InlineData(SDL.Scancode.RGUI, Key.MetaRight)]
    public void FromScancode_KnownKey_ReturnsTheSameHidUsage(SDL.Scancode scancode, Key expected)
        => Assert.Equal(expected, KeyMap.FromScancode((int)scancode));

    [Theory]
    [InlineData(0)]
    [InlineData(0x32)] // Non-US hash: not in Key.
    [InlineData(0x64)] // Non-US backslash.
    [InlineData(0x7F)] // Mute.
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(-1)]
    public void FromScancode_UnknownScancode_ReturnsNone(int scancode)
        => Assert.Equal(Key.None, KeyMap.FromScancode(scancode));

    [Fact]
    public void FromScancode_EveryKey_MapsToItself()
    {
        foreach (var key in Enum.GetValues<Key>())
        {
            Assert.Equal(key, KeyMap.FromScancode((int)key));
        }
    }
}
