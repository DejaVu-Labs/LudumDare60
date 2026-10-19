namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class SilentAudioTests
{
    private static AudioEvent[] Submit(SilentAudio audio, int capacity = 8)
    {
        var events = new AudioEvent[capacity];
        return events[..audio.Submit([], events)];
    }

    [Fact]
    public void Submit_ReportsEveryCreatedSoundReadyInOrder()
    {
        var audio = new SilentAudio();
        audio.CreateSound(3, [1]);
        audio.CreateSound(1, [2]);

        Assert.Equal([new AudioEvent(AudioEventKind.SoundReady, 3), new AudioEvent(AudioEventKind.SoundReady, 1)], Submit(audio));
        Assert.Empty(Submit(audio));
    }

    [Fact]
    public void Submit_EventsThatDoNotFit_ArriveWithTheNextCall()
    {
        var audio = new SilentAudio();
        audio.CreateSound(1, []);
        audio.CreateSound(2, []);

        Assert.Equal([new AudioEvent(AudioEventKind.SoundReady, 1)], Submit(audio, capacity: 1));
        Assert.Equal([new AudioEvent(AudioEventKind.SoundReady, 2)], Submit(audio));
    }

    [Fact]
    public void DestroySound_BeforeSubmit_ReportsNothingForIt()
    {
        var audio = new SilentAudio();
        audio.CreateSound(1, []);
        audio.CreateSound(2, []);
        audio.DestroySound(1);

        Assert.Equal([new AudioEvent(AudioEventKind.SoundReady, 2)], Submit(audio));
    }
}
