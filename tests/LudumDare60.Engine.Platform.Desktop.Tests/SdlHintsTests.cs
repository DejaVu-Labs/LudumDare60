using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class SdlHintsTests
{
    [Fact]
    public void Get_PlainDesktop_OnlyTurnsOffMouseCopiesOfTouches()
        => Assert.Equal([new(SDL.Hints.TouchMouseEvents, "0")], SdlHints.Get(static _ => null));

    [Fact]
    public void Get_UnderSommelier_PicksWaylandWithoutLibdecor()
    {
        var hints = SdlHints.Get(static name => string.Equals(name, SdlHints.SommelierVariable, StringComparison.Ordinal) ? "0.20" : null);

        Assert.Contains(new(SDL.Hints.VideoDriver, "wayland"), hints);
        Assert.Contains(new(SDL.Hints.VideoWaylandAllowLibdecor, "0"), hints);
    }

    [Fact]
    public void Get_SommelierVariableEmpty_CountsAsUnset()
        => Assert.Single(SdlHints.Get(static _ => ""));
}
