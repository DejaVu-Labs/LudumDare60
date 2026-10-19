using LudumDare60.Engine.Platform;

namespace LudumDare60.Game.Analytics;

/// <summary>
/// The product analytics events of docs/gamedesign/analytics.md for what the player does outside the repair: leaving
/// the title screen, the music on the phone and the elephant statuette. <see cref="ShopAnalytics"/> covers the orders.
/// </summary>
/// <remarks>Every id is built in the constructor, so the calls allocate nothing.</remarks>
public sealed class RoomAnalytics
{
    /// <summary>
    /// Sent once when the player presses on the title screen, with the seconds the title screen was up.
    /// </summary>
    public const string StartedEvent = "Game:Started";

    /// <summary>
    /// Sent when a click on the phone turns the music off after its last track, with the seconds since the player
    /// started.
    /// </summary>
    public const string PhoneOffEvent = "Phone:Off";

    /// <summary>
    /// Sent each time the elephant statuette trumpets, with the seconds since the player started.
    /// </summary>
    public const string ElephantEvent = "Elephant:Trumpet";

    private const string PhoneTrackPrefix = "Phone:Track:";

    private readonly IAnalytics _analytics;
    private readonly string[] _trackIds;
    private bool _started;

    /// <param name="analytics">Where the events go.</param>
    /// <param name="trackPaths">The phone's tracks, as asset paths (<see cref="AssetPaths.MusicTracks"/>): each event
    /// id names its track by the file name without the extension.</param>
    public RoomAnalytics(IAnalytics analytics, IReadOnlyList<string> trackPaths)
    {
        ArgumentNullException.ThrowIfNull(analytics);
        ArgumentNullException.ThrowIfNull(trackPaths);
        _analytics = analytics;
        _trackIds = new string[trackPaths.Count];
        for (var i = 0; i < _trackIds.Length; i++)
        {
            _trackIds[i] = PhoneTrackEventId(trackPaths[i]);
        }
    }

    /// <summary>
    /// The id of the event sent when a click on the phone starts the track at <paramref name="trackPath"/>.
    /// </summary>
    public static string PhoneTrackEventId(string trackPath)
        => string.Concat(PhoneTrackPrefix, Path.GetFileNameWithoutExtension(trackPath));

    /// <summary>
    /// Sends <see cref="StartedEvent"/>, once: the player left the title screen.
    /// </summary>
    /// <param name="titleSeconds">How long the title screen was up, from the shop opening to the press.</param>
    public void Started(float titleSeconds)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _analytics.Design(StartedEvent, Seconds(titleSeconds));
    }

    /// <summary>
    /// Sends the event of a click on the phone: the track it started, or <see cref="PhoneOffEvent"/>.
    /// </summary>
    /// <param name="track">The track now playing (<see cref="PhonePlayer.Track"/>), or -1 for the music off.</param>
    /// <param name="playSeconds">Seconds since the player started.</param>
    public void Phone(int track, float playSeconds)
        => _analytics.Design(track >= 0 && track < _trackIds.Length ? _trackIds[track] : PhoneOffEvent, Seconds(playSeconds));

    /// <summary>
    /// Sends <see cref="ElephantEvent"/>: the elephant statuette trumpeted.
    /// </summary>
    /// <param name="playSeconds">Seconds since the player started.</param>
    public void Trumpet(float playSeconds) => _analytics.Design(ElephantEvent, Seconds(playSeconds));

    private static double Seconds(float seconds) => Math.Round(seconds, 2);
}
