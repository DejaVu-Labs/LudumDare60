using System.Text.Json;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Sounds;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Engine.Tests;

public sealed class AudioTests
{
    private static readonly string s_fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "audio-frame.json");
    private static readonly byte[] s_file = [1, 2, 3];

    private readonly FakeAudioBackend _backend = new();
    private readonly Audio _audio;

    public AudioTests()
    {
        _audio = new Audio(_backend);
        _audio.BeginFrame();
    }

    /// <summary>
    /// Ends the frame the way the engine does and starts the next one.
    /// </summary>
    private List<AudioCommand> NextFrame()
    {
        _audio.BeginRender();
        _audio.EndFrame();
        _audio.BeginFrame();
        return _backend.LastCommands();
    }

    private Sound ReadySound()
    {
        var sound = _audio.CreateSound(s_file);
        _backend.ReportReady(sound.Id);
        NextFrame();
        return sound;
    }

    private static AudioCommand Command(AudioCommandKind kind, params double[] arguments) => new(kind, arguments);

    [Fact]
    public void CreateSound_BeforeFirstFrame_Throws()
    {
        var audio = new Audio(_backend);

        Assert.Throws<InvalidOperationException>(() => audio.CreateSound(s_file));
        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void CreateSound_Empty_Throws()
        => Assert.Throws<ArgumentException>(() => _audio.CreateSound([]));

    [Fact]
    public void CreateSound_PassesBytesAndStartsDecoding()
    {
        var sound = _audio.CreateSound(s_file);

        Assert.Equal(["create 0 3B"], _backend.Calls);
        Assert.Equal(s_file, _backend.Sounds[0]);
        Assert.Equal(SoundState.Decoding, _audio.GetState(sound));
        Assert.True(_audio.IsAlive(sound));
        Assert.Equal(1, _audio.SoundCount);
    }

    [Fact]
    public void CreateSound_Several_GetDistinctIds()
    {
        var first = _audio.CreateSound(s_file);
        var second = _audio.CreateSound(s_file);

        Assert.NotEqual(first, second);
        Assert.Equal(["create 0 3B", "create 1 3B"], _backend.Calls);
    }

    [Theory]
    [InlineData(AudioEventKind.SoundReady, SoundState.Ready)]
    [InlineData(AudioEventKind.SoundFailed, SoundState.Failed)]
    public void EndFrame_DecodeEvent_SetsState(AudioEventKind kind, SoundState expected)
    {
        var sound = _audio.CreateSound(s_file);
        _backend.Report(new AudioEvent(kind, sound.Id));

        NextFrame();

        Assert.Equal(expected, _audio.GetState(sound));
    }

    [Fact]
    public void EndFrame_UnknownEventKind_Throws()
    {
        var sound = _audio.CreateSound(s_file);
        _backend.Report(new AudioEvent((AudioEventKind)9, sound.Id));

        Assert.Throws<InvalidOperationException>(_audio.EndFrame);
    }

    [Fact]
    public void EndFrame_EventForDestroyedSound_IsIgnored()
    {
        var sound = _audio.CreateSound(s_file);
        _audio.DestroySound(sound);
        _backend.ReportReady(sound.Id);

        NextFrame();

        Assert.Equal(["create 0 3B", "submit", "destroy 0"], _backend.Calls);
    }

    [Fact]
    public void EndFrame_SubmitsEachFrameOnceAndStartsEmpty()
    {
        _audio.SetBusVolume(AudioBus.Sfx, 0.5f);

        Assert.Single(NextFrame());
        Assert.Empty(NextFrame());
        Assert.Equal(2, _backend.Frames.Count);
    }

    [Fact]
    public void Play_Defaults_PlaysOnSfxAtFullVolume()
    {
        var sound = ReadySound();

        _audio.Play(sound);

        Assert.Equal([Command(AudioCommandKind.Play, sound.Id, (int)AudioBus.Sfx, 1, 1, 0)], NextFrame());
    }

    [Fact]
    public void Play_Options_AreWritten()
    {
        var sound = ReadySound();

        _audio.Play(sound, AudioBus.Ambience, new SoundOptions { Volume = 0.5f, Pitch = 2, Pan = -1 });

        Assert.Equal([Command(AudioCommandKind.Play, sound.Id, (int)AudioBus.Ambience, 0.5, 2, -1)], NextFrame());
    }

    [Theory]
    [InlineData(AudioEventKind.None)]
    [InlineData(AudioEventKind.SoundFailed)]
    public void Play_SoundNotReady_IsSkipped(AudioEventKind report)
    {
        var sound = _audio.CreateSound(s_file);
        if (report != AudioEventKind.None)
        {
            _backend.Report(new AudioEvent(report, sound.Id));
        }

        NextFrame();
        _audio.Play(sound);

        Assert.Empty(NextFrame());
    }

    [Theory]
    [InlineData(AudioBus.Master)]
    [InlineData((AudioBus)4)]
    [InlineData((AudioBus)(-1))]
    public void Play_BusMasterOrUnknown_Throws(AudioBus bus)
    {
        var sound = ReadySound();

        Assert.Throws<ArgumentOutOfRangeException>(() => _audio.Play(sound, bus, SoundOptions.Default));
    }

    [Theory]
    [InlineData(-0.1f, 1, 0)]
    [InlineData(float.NaN, 1, 0)]
    [InlineData(float.PositiveInfinity, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, Audio.MaxPitch + 1, 0)]
    [InlineData(1, float.NaN, 0)]
    [InlineData(1, 1, 1.5f)]
    [InlineData(1, 1, -1.5f)]
    [InlineData(1, 1, float.NaN)]
    public void Play_OptionOutOfRange_Throws(float volume, float pitch, float pan)
    {
        var sound = ReadySound();
        var options = new SoundOptions { Volume = volume, Pitch = pitch, Pan = pan };

        Assert.Throws<ArgumentOutOfRangeException>(() => _audio.Play(sound, AudioBus.Sfx, options));
        Assert.Throws<ArgumentOutOfRangeException>(() => _audio.PlayLoop(sound, AudioBus.Sfx, options));
    }

    [Fact]
    public void Play_DefaultOptions_Throws()
    {
        var sound = ReadySound();

        Assert.Throws<ArgumentOutOfRangeException>(() => _audio.Play(sound, AudioBus.Sfx, default));
    }

    [Fact]
    public void Play_StaleSound_Throws()
    {
        var sound = ReadySound();
        _audio.DestroySound(sound);

        Assert.Throws<InvalidOperationException>(() => _audio.Play(sound));
        Assert.Throws<InvalidOperationException>(() => _audio.Play(default));
    }

    [Fact]
    public void PlayLoop_DecodingSound_WritesCommandAndReturnsPlayingVoice()
    {
        var sound = _audio.CreateSound(s_file);

        var voice = _audio.PlayLoop(
            sound, AudioBus.Ambience, new SoundOptions { Volume = 0.5f, Pitch = 0.75f, Pan = 0.25f }, fadeInSeconds: 3);

        Assert.True(_audio.IsPlaying(voice));
        Assert.Equal(1, _audio.LoopCount);
        Assert.Equal(
            [Command(AudioCommandKind.PlayLoop, 0, sound.Id, (int)AudioBus.Ambience, 0.5, 0.75, 0.25, 3)],
            NextFrame());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void PlayLoop_FadeOutOfRange_Throws(float fade)
    {
        var sound = ReadySound();

        Assert.Throws<ArgumentOutOfRangeException>(() => _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default, fade));
    }

    [Fact]
    public void PlayLoop_Several_GetDistinctVoices()
    {
        var sound = ReadySound();

        var first = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);
        var second = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);

        Assert.NotEqual(first, second);
        Assert.Equal(2, _audio.LoopCount);
    }

    [Fact]
    public void SetVoice_WritesCommand()
    {
        var sound = ReadySound();
        var voice = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);
        NextFrame();

        _audio.SetVoice(voice, new SoundOptions { Volume = 0.25f, Pitch = 1.5f, Pan = 1 });

        Assert.Equal([Command(AudioCommandKind.SetVoice, 0, 0.25, 1.5, 1)], NextFrame());
    }

    [Fact]
    public void Stop_WritesCommandAndMakesVoiceStale()
    {
        var sound = ReadySound();
        var voice = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);
        NextFrame();

        _audio.Stop(voice, fadeOutSeconds: 0.5f);

        Assert.False(_audio.IsPlaying(voice));
        Assert.Equal(0, _audio.LoopCount);
        Assert.Throws<InvalidOperationException>(() => _audio.SetVoice(voice, SoundOptions.Default));
        Assert.Throws<InvalidOperationException>(() => _audio.Stop(voice));
        Assert.Equal([Command(AudioCommandKind.StopVoice, 0, 0.5)], NextFrame());
    }

    [Fact]
    public void Stop_ThenPlayLoop_ReusesIdWithNewGeneration()
    {
        var sound = ReadySound();
        var old = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);
        _audio.Stop(old);

        var current = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);

        Assert.NotEqual(old, current);
        Assert.False(_audio.IsPlaying(old));
        Assert.True(_audio.IsPlaying(current));
        Assert.Equal(
            [
                Command(AudioCommandKind.PlayLoop, 0, sound.Id, (int)AudioBus.Sfx, 1, 1, 0, 0),
                Command(AudioCommandKind.StopVoice, 0, 0),
                Command(AudioCommandKind.PlayLoop, 0, sound.Id, (int)AudioBus.Sfx, 1, 1, 0, 0),
            ],
            NextFrame());
    }

    [Fact]
    public void Voice_Default_IsNotPlaying()
    {
        Assert.False(_audio.IsPlaying(default));
        Assert.Throws<InvalidOperationException>(() => _audio.Stop(default));
    }

    [Fact]
    public void DestroySound_StopsItsLoopsAndDeletesAfterSubmit()
    {
        var sound = ReadySound();
        var other = ReadySound();
        var loop = _audio.PlayLoop(sound, AudioBus.Ambience, SoundOptions.Default);
        var otherLoop = _audio.PlayLoop(other, AudioBus.Ambience, SoundOptions.Default);
        _backend.Calls.Clear();

        _audio.DestroySound(sound);

        Assert.False(_audio.IsAlive(sound));
        Assert.False(_audio.IsPlaying(loop));
        Assert.True(_audio.IsPlaying(otherLoop));
        Assert.Equal(1, _audio.LoopCount);
        Assert.Equal(1, _audio.SoundCount);
        Assert.Empty(_backend.Calls);

        var commands = NextFrame();

        Assert.Equal(Command(AudioCommandKind.StopVoice, loop.Id, 0), commands[^1]);
        Assert.Equal(["submit", $"destroy {sound.Id}"], _backend.Calls);
    }

    [Fact]
    public void DestroySound_IdIsReusedOnlyAfterTheFrame()
    {
        var sound = _audio.CreateSound(s_file);
        _audio.DestroySound(sound);

        var sameFrame = _audio.CreateSound(s_file);
        NextFrame();
        var nextFrame = _audio.CreateSound(s_file);

        Assert.Equal(1, sameFrame.Id);
        Assert.Equal(0, nextFrame.Id);
        Assert.NotEqual(sound, nextFrame);
        Assert.False(_audio.IsAlive(sound));
        Assert.Equal(SoundState.Decoding, _audio.GetState(nextFrame));
    }

    [Fact]
    public void DestroySound_StaleHandle_Throws()
    {
        var sound = _audio.CreateSound(s_file);
        _audio.DestroySound(sound);

        Assert.Throws<InvalidOperationException>(() => _audio.DestroySound(sound));
        Assert.Throws<InvalidOperationException>(() => _audio.GetState(sound));
        Assert.Throws<InvalidOperationException>(() => _audio.GetState(default));
    }

    [Fact]
    public void PlayMusic_StartsLoopOnMusicBusWithFadeIn()
    {
        var track = _audio.CreateSound(s_file);

        _audio.PlayMusic(track, crossfadeSeconds: 2, volume: 0.5f);

        Assert.Equal(track, _audio.Music);
        Assert.Equal(0, _audio.LoopCount);
        Assert.Equal(
            [Command(AudioCommandKind.PlayLoop, 0, track.Id, (int)AudioBus.Music, 0.5, 1, 0, 2)],
            NextFrame());
    }

    [Fact]
    public void PlayMusic_SameTrack_DoesNothing()
    {
        var track = ReadySound();
        _audio.PlayMusic(track);
        NextFrame();

        _audio.PlayMusic(track);

        Assert.Empty(NextFrame());
    }

    [Fact]
    public void PlayMusic_OtherTrack_CrossfadesOldOutAndNewIn()
    {
        var first = ReadySound();
        var second = ReadySound();
        _audio.PlayMusic(first);
        NextFrame();

        _audio.PlayMusic(second, crossfadeSeconds: 3);

        Assert.Equal(second, _audio.Music);
        Assert.Equal(
            [
                Command(AudioCommandKind.StopVoice, 0, 3),
                Command(AudioCommandKind.PlayLoop, 0, second.Id, (int)AudioBus.Music, 1, 1, 0, 3),
            ],
            NextFrame());
    }

    [Fact]
    public void StopMusic_FadesOutAndClearsTrack()
    {
        var track = ReadySound();
        _audio.PlayMusic(track);
        NextFrame();

        _audio.StopMusic(fadeSeconds: 4);

        Assert.Null(_audio.Music);
        Assert.Equal([Command(AudioCommandKind.StopVoice, 0, 4)], NextFrame());
    }

    [Fact]
    public void StopMusic_NoMusic_DoesNothing()
    {
        _audio.StopMusic();

        Assert.Empty(NextFrame());
    }

    [Fact]
    public void PlayMusic_AfterStopMusic_SameTrackStartsAgain()
    {
        var track = ReadySound();
        _audio.PlayMusic(track);
        _audio.StopMusic();

        _audio.PlayMusic(track);

        Assert.Equal(track, _audio.Music);
        Assert.Equal(AudioCommandKind.PlayLoop, NextFrame()[^1].Kind);
    }

    [Fact]
    public void DestroySound_CurrentTrack_StopsMusicButNotLoops()
    {
        var track = ReadySound();
        var loop = _audio.PlayLoop(track, AudioBus.Ambience, SoundOptions.Default);
        _audio.PlayMusic(track);

        _audio.DestroySound(track);

        Assert.Null(_audio.Music);
        Assert.False(_audio.IsPlaying(loop));
        Assert.Equal(0, _audio.LoopCount);
    }

    [Fact]
    public void SetBusVolume_WritesCommandAndRemembersVolume()
    {
        Assert.Equal(1, _audio.GetBusVolume(AudioBus.Master));

        _audio.SetBusVolume(AudioBus.Master, 0);

        Assert.Equal(0, _audio.GetBusVolume(AudioBus.Master));
        Assert.Equal([Command(AudioCommandKind.SetBusVolume, (int)AudioBus.Master, 0)], NextFrame());
    }

    [Fact]
    public void SetBusVolume_BeforeFirstFrame_IsSubmittedWithFirstFrame()
    {
        var audio = new Audio(_backend);

        audio.SetBusVolume(AudioBus.Music, 0.5f);
        audio.BeginFrame();
        audio.EndFrame();

        Assert.Equal([Command(AudioCommandKind.SetBusVolume, (int)AudioBus.Music, 0.5)], _backend.LastCommands());
    }

    [Theory]
    [InlineData((AudioBus)4, 1)]
    [InlineData(AudioBus.Sfx, -1)]
    [InlineData(AudioBus.Sfx, float.NaN)]
    public void SetBusVolume_OutOfRange_Throws(AudioBus bus, float volume)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _audio.SetBusVolume(bus, volume));

    [Fact]
    public void Rendering_RecordingCommands_Throws()
    {
        var sound = ReadySound();
        var voice = _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default);

        _audio.BeginRender();

        Assert.Throws<InvalidOperationException>(() => _audio.Play(sound));
        Assert.Throws<InvalidOperationException>(() => _audio.PlayLoop(sound, AudioBus.Sfx, SoundOptions.Default));
        Assert.Throws<InvalidOperationException>(() => _audio.SetVoice(voice, SoundOptions.Default));
        Assert.Throws<InvalidOperationException>(() => _audio.Stop(voice));
        Assert.Throws<InvalidOperationException>(() => _audio.PlayMusic(sound));
        Assert.Throws<InvalidOperationException>(() => _audio.StopMusic());
        Assert.Throws<InvalidOperationException>(() => _audio.SetBusVolume(AudioBus.Sfx, 1));
        Assert.Throws<InvalidOperationException>(() => _audio.CreateSound(s_file));
        Assert.Throws<InvalidOperationException>(() => _audio.DestroySound(sound));

        _audio.EndFrame();
        _audio.BeginFrame();
        _audio.Play(sound);
    }

    [Fact]
    public void Commands_BeyondCapacity_Throws()
    {
        var exception = Record.Exception(() =>
        {
            for (var i = 0; i <= Audio.CommandCapacity; i++)
            {
                _audio.SetBusVolume(AudioBus.Sfx, 1);
            }
        });

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void DestroyAll_DestroysEverySoundAndResetsChangedBuses()
    {
        var first = ReadySound();
        var second = ReadySound();
        var loop = _audio.PlayLoop(first, AudioBus.Sfx, SoundOptions.Default);
        _audio.SetBusVolume(AudioBus.Master, 0);
        _audio.DestroySound(second);
        _backend.Calls.Clear();

        _audio.DestroyAll();

        Assert.Equal(["destroy 1", "destroy 0", "submit"], _backend.Calls);
        Assert.Equal([Command(AudioCommandKind.SetBusVolume, (int)AudioBus.Master, 1)], _backend.LastCommands());
        Assert.False(_audio.IsAlive(first));
        Assert.False(_audio.IsPlaying(loop));
        Assert.Equal(0, _audio.SoundCount);
        Assert.Equal(0, _audio.LoopCount);
        Assert.Equal(1, _audio.GetBusVolume(AudioBus.Master));
        Assert.Throws<InvalidOperationException>(() => _audio.CreateSound(s_file));
    }

    [Fact]
    public void DestroyAll_BusesUnchanged_SubmitsNothing()
    {
        _audio.CreateSound(s_file);
        _backend.Calls.Clear();

        _audio.DestroyAll();

        Assert.Equal(["destroy 0"], _backend.Calls);
    }

    [Fact]
    public void DestroyAll_BackendThrows_DestroysTheRestAndThrowsAggregate()
    {
        _audio.CreateSound(s_file);
        _audio.CreateSound(s_file);
        _backend.OnDestroy = static id => throw new InvalidOperationException($"destroy {id} failed");

        var exception = Assert.Throws<AggregateException>(_audio.DestroyAll);

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Empty(_backend.Sounds);
    }

    [Fact]
    public void Frame_MatchesSharedFixture()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(s_fixturePath));
        var root = fixture.RootElement;
        var first = ReadySound();
        var second = ReadySound();
        Assert.Equal([0, 1], root.GetProperty("sounds").EnumerateArray().Select(static e => e.GetInt32()));
        Assert.Equal((0, 1), (first.Id, second.Id));

        _audio.SetBusVolume(AudioBus.Music, 0.5f);
        _audio.Play(first, AudioBus.Sfx, new SoundOptions { Volume = 0.75f, Pitch = 1.25f, Pan = -0.5f });
        var loop = _audio.PlayLoop(second, AudioBus.Ambience, new SoundOptions { Volume = 0.5f }, fadeInSeconds: 0.25f);
        _audio.SetVoice(loop, new SoundOptions { Volume = 0.25f, Pitch = 0.5f, Pan = 1 });
        _audio.PlayMusic(second, crossfadeSeconds: 2);
        _audio.Stop(loop, fadeOutSeconds: 0.5f);
        NextFrame();

        Assert.Equal(root.GetProperty("commandsHex").GetString(), Convert.ToHexStringLower(_backend.Frames[^1]));
    }
}
