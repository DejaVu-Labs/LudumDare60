namespace LudumDare60.Engine.Platform;

/// <summary>
/// Limits every audio backend applies.
/// </summary>
public static class AudioLimits
{
    /// <summary>
    /// Voices that may play at once on the <see cref="AudioBus.Sfx"/> and <see cref="AudioBus.Ambience"/> buses, one-shots
    /// and loops together. A one-shot beyond it is dropped; loops always start. Music does not count.
    /// </summary>
    public const int MaxVoices = 32;
}
