namespace LudumDare60.Samples.SoundBoard;

/// <summary>
/// The sample's sound files, below <c>wwwroot/assets/</c> of the browser project. <c>generate-sounds.py</c> writes them.
/// </summary>
public static class SoundPaths
{
    public const string Click = "sfx/click.wav";
    public const string Clink = "sfx/clink.wav";
    public const string Squeak = "sfx/squeak.wav";
    public const string Ratchet = "sfx/ratchet.wav";
    public const string Drop = "sfx/drop.wav";
    public const string Spring = "sfx/spring.wav";
    public const string DryFire = "sfx/dry-fire.wav";
    public const string Rain = "ambience/rain.ogg";
    public const string TrackA = "music/track-a.ogg";
    public const string TrackB = "music/track-b.ogg";

    /// <summary>
    /// The effects in key order: <see cref="Engine.Platform.Key.Digit1"/> plays the first.
    /// </summary>
    public static IReadOnlyList<string> Effects { get; } = [Click, Clink, Squeak, Ratchet, Drop, Spring, DryFire];
}
