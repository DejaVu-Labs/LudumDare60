using System.Text.RegularExpressions;
using LudumDare60.Game.Analytics;

namespace LudumDare60.Game.Tests.Analytics;

public sealed partial class RoomAnalyticsTests
{
    private readonly RecordingAnalytics _recorded = new();
    private readonly RoomAnalytics _analytics;

    public RoomAnalyticsTests() => _analytics = new RoomAnalytics(_recorded, AssetPaths.MusicTracks);

    [GeneratedRegex(@"^[A-Za-z0-9\s\-_.()!?]{1,64}(:[A-Za-z0-9\s\-_.()!?]{1,64}){0,4}$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GameAnalyticsDesignId();

    [Fact]
    public void Started_TwiceAfterTheTitle_SendsTheTitleSecondsOnce()
    {
        _analytics.Started(4.567f);
        _analytics.Started(9);

        Assert.Equal([(RoomAnalytics.StartedEvent, 4.57)], _recorded.Designs);
    }

    [Fact]
    public void Phone_EveryClick_NamesTheTrackItStartsOrTheMusicOff()
    {
        for (var track = 0; track < AssetPaths.MusicTracks.Count; track++)
        {
            _analytics.Phone(track, track + 10);
        }

        _analytics.Phone(-1, 20.004f);

        var expected = AssetPaths.MusicTracks.Select((path, i) => (RoomAnalytics.PhoneTrackEventId(path), (double?)(i + 10)))
            .Append((RoomAnalytics.PhoneOffEvent, 20.0));
        Assert.Equal(expected, _recorded.Designs);
        Assert.Equal("Phone:Track:blues", RoomAnalytics.PhoneTrackEventId(AssetPaths.MusicTracks[0]));
    }

    [Fact]
    public void Trumpet_EachTime_SendsTheSecondsPlayed()
    {
        _analytics.Trumpet(1.5f);
        _analytics.Trumpet(3);

        Assert.Equal([(RoomAnalytics.ElephantEvent, 1.5), (RoomAnalytics.ElephantEvent, 3.0)], _recorded.Designs);
    }

    [Fact]
    public void EveryEventId_FollowsTheGameAnalyticsRules()
    {
        var ids = AssetPaths.MusicTracks.Select(RoomAnalytics.PhoneTrackEventId)
            .Append(RoomAnalytics.StartedEvent)
            .Append(RoomAnalytics.PhoneOffEvent)
            .Append(RoomAnalytics.ElephantEvent);

        Assert.All(ids, id => Assert.Matches(GameAnalyticsDesignId(), id));
    }
}
