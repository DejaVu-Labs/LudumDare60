using System.Numerics;

namespace LudumDare60.Game.Tests;

public sealed class PhonePlayerTests
{
    [Fact]
    public void Constructor_MusicIsOff()
    {
        var player = new PhonePlayer(3);

        Assert.Equal(-1, player.Track);
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void Next_EachClick_PlaysTheNextTrackThenTurnsTheMusicOffAndStartsOver()
    {
        var player = new PhonePlayer(3);

        int[] tracks = [player.Next(), player.Next(), player.Next(), player.Next(), player.Next()];

        Assert.Equal([0, 1, 2, -1, 0], tracks);
    }

    [Fact]
    public void Constructor_NoTracks_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PhonePlayer(0));

    [Fact]
    public void IsOnPhone_OpaqueAndTransparentPixels_TakeOnlyTheOpaqueOnes()
    {
        // A 2x2 sprite with its top-left pixel opaque.
        bool[] opaque = [true, false, false, false];

        Assert.True(PhonePlayer.IsOnPhone(ShopDecor.PhoneAt + new Vector2(0.5f), opaque, 2));
        Assert.False(PhonePlayer.IsOnPhone(ShopDecor.PhoneAt + new Vector2(1.5f, 0.5f), opaque, 2));
        Assert.False(PhonePlayer.IsOnPhone(ShopDecor.PhoneAt - new Vector2(0.5f), opaque, 2));
        Assert.False(PhonePlayer.IsOnPhone(ShopDecor.PhoneAt + new Vector2(5), opaque, 2));
        Assert.False(PhonePlayer.IsOnPhone(ShopDecor.PhoneAt, [], 0));
    }
}
