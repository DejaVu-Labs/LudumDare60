namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// <see cref="IAudioBackend"/> that plays nothing: the desktop platform has no sound output yet. Every sound reports
/// <see cref="AudioEventKind.SoundReady"/>, so the game runs as it would with sound.
/// </summary>
internal sealed class SilentAudio : IAudioBackend
{
    private readonly List<AudioEvent> _pending = [];

    public void CreateSound(int id, ReadOnlySpan<byte> data) => _pending.Add(new AudioEvent(AudioEventKind.SoundReady, id));

    public void DestroySound(int id)
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].SoundId == id)
            {
                _pending.RemoveAt(i);
            }
        }
    }

    public int Submit(ReadOnlySpan<byte> commands, Span<AudioEvent> events)
    {
        var count = Math.Min(events.Length, _pending.Count);
        for (var i = 0; i < count; i++)
        {
            events[i] = _pending[i];
        }

        _pending.RemoveRange(0, count);
        return count;
    }
}
