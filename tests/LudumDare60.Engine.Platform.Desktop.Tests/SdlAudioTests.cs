using LudumDare60.Engine.Platform.Desktop.Sounds;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

/// <summary>
/// <see cref="SdlAudio"/> without a device: decoding, events and commands work the same, only nothing is heard.
/// </summary>
public sealed class SdlAudioTests
{
    private readonly SdlAudio _audio = new();

    private AudioEvent[] Submit(ReadOnlySpan<byte> commands = default, int capacity = 8)
    {
        var events = new AudioEvent[capacity];
        return events[.._audio.Submit(commands, events)];
    }

    [Fact]
    public void CreateSound_Wav_ReportsReady()
    {
        _audio.CreateSound(3, Wav.Pcm16(1, 22050, 1, 2, 3));
        _audio.WaitForDecodes();

        Assert.Equal([new AudioEvent(AudioEventKind.SoundReady, 3)], Submit());
        Assert.Empty(Submit());
        Assert.False(_audio.HasDevice);
    }

    [Fact]
    public void CreateSound_Broken_ReportsFailed()
    {
        _audio.CreateSound(1, [1, 2, 3]);
        _audio.WaitForDecodes();

        Assert.Equal([new AudioEvent(AudioEventKind.SoundFailed, 1)], Submit());
    }

    [Fact]
    public void CreateSound_CopiesTheData()
    {
        var file = Wav.Pcm16(1, 22050, 1);
        _audio.CreateSound(0, file);
        Array.Clear(file);
        _audio.WaitForDecodes();

        Assert.Equal(AudioEventKind.SoundReady, Submit()[0].Kind);
    }

    [Fact]
    public void CreateSound_TakenId_Throws()
    {
        _audio.CreateSound(0, Wav.Pcm16(1, 22050, 1));

        Assert.Throws<InvalidOperationException>(() => _audio.CreateSound(0, Wav.Pcm16(1, 22050, 1)));
    }

    [Fact]
    public void DestroySound_WhileDecoding_ReportsNothing()
    {
        _audio.CreateSound(0, Wav.Pcm16(1, 22050, 1));
        _audio.CreateSound(1, Wav.Pcm16(1, 22050, 1));
        _audio.WaitForDecodes();
        _audio.DestroySound(0);

        Assert.Equal([new AudioEvent(AudioEventKind.SoundReady, 1)], Submit());
        Assert.False(_audio.Mixer.HasSound(0));
    }

    [Fact]
    public void DestroySound_ReadyButNotReported_ReportsNothing()
    {
        _audio.CreateSound(0, Wav.Pcm16(1, 22050, 1));
        _audio.CreateSound(1, Wav.Pcm16(1, 22050, 1));
        _audio.WaitForDecodes();
        Submit(capacity: 1);

        _audio.DestroySound(1);

        Assert.Empty(Submit());
    }

    [Fact]
    public void Submit_EventsThatDoNotFit_ArriveWithTheNextCall()
    {
        _audio.CreateSound(0, Wav.Pcm16(1, 22050, 1));
        _audio.CreateSound(1, Wav.Pcm16(1, 22050, 1));
        _audio.WaitForDecodes();

        Assert.Single(Submit(capacity: 1));
        Assert.Single(Submit());
    }

    [Fact]
    public void Submit_RunsCommandsOnTheMixer()
    {
        _audio.CreateSound(0, Wav.Pcm16(1, 22050, 1, 2, 3));
        _audio.WaitForDecodes();
        Submit();

        // PlayLoop voice 4, sound 0, Ambience, volume 1, pitch 1, pan 0, no fade.
        int[] words = [2, 4, 0, (int)AudioBus.Ambience, BitConverter.SingleToInt32Bits(1), BitConverter.SingleToInt32Bits(1), 0, 0];
        Submit(System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan()));

        Assert.Equal(1, _audio.Mixer.ActiveVoices);
    }

    [Fact]
    public void Submit_BadCommand_Throws()
        => Assert.Throws<InvalidOperationException>(() => _audio.Submit([9, 0, 0, 0], []));
}
