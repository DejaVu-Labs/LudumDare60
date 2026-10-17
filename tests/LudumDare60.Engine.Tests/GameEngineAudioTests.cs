using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Sounds;

namespace LudumDare60.Engine.Tests;

public sealed class GameEngineAudioTests
{
    private readonly GameEngine _engine = new(640, 360);
    private readonly RecordingGame _game = new();

    [Fact]
    public async Task Initialize_GetsAudioOnPlatformBackend()
    {
        var platform = new ScriptedPlatform(0.01);
        var created = false;
        _game.OnUpdateAudio = audio =>
        {
            if (!created)
            {
                audio.CreateSound([1]);
                created = true;
            }
        };

        await _engine.RunAsync(_game, platform);

        Assert.NotNull(_game.Audio);
        Assert.Equal(["create 0 1B", "submit", "destroy 0"], platform.AudioBackend.Calls);
    }

    [Fact]
    public async Task Initialize_CreateSound_Throws()
    {
        _game.OnInitializeAudio = static audio => audio.CreateSound([1]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RunAsync(_game, new ScriptedPlatform(0.01)));
    }

    [Fact]
    public async Task Initialize_SetBusVolume_IsSubmittedWithFirstFrameAndResetAtRunEnd()
    {
        var platform = new ScriptedPlatform(0.01, 0.01);
        _game.OnInitializeAudio = static audio => audio.SetBusVolume(AudioBus.Master, 0.25f);

        await _engine.RunAsync(_game, platform);

        var frames = platform.AudioBackend.Frames;
        Assert.Equal(3, frames.Count);
        Assert.Equal(
            [new Testing.AudioCommand(AudioCommandKind.SetBusVolume, [(int)AudioBus.Master, 0.25])],
            Testing.FakeAudioBackend.Decode(frames[0]));
        Assert.Empty(frames[1]);
        Assert.Equal(
            [new Testing.AudioCommand(AudioCommandKind.SetBusVolume, [(int)AudioBus.Master, 1])],
            Testing.FakeAudioBackend.Decode(frames[2]));
    }

    [Fact]
    public async Task Frame_SubmitsAudioOncePerFrame()
    {
        var platform = new ScriptedPlatform(0.01, 0.01, 0.01);

        await _engine.RunAsync(_game, platform);

        Assert.Equal(3, platform.AudioBackend.Frames.Count);
    }

    [Fact]
    public async Task Update_DecodeResult_IsVisibleInNextFrame()
    {
        var platform = new ScriptedPlatform(0.01, 0.01);
        var states = new List<SoundState>();
        Sound sound = default;
        _game.OnUpdateAudio = audio =>
        {
            if (!audio.IsAlive(sound))
            {
                sound = audio.CreateSound([1]);
                platform.AudioBackend.ReportReady(sound.Id);
            }

            states.Add(audio.GetState(sound));
        };

        await _engine.RunAsync(_game, platform);

        Assert.Equal([SoundState.Decoding, SoundState.Ready], states);
    }

    [Fact]
    public async Task PrepareRender_RecordingCommands_Throws()
    {
        _game.OnPrepareRenderAudio = static audio => audio.SetBusVolume(AudioBus.Sfx, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RunAsync(_game, new ScriptedPlatform(0.01)));
    }

    [Fact]
    public async Task Render_RecordingCommands_Throws()
    {
        _game.OnRenderAudio = static audio => audio.SetBusVolume(AudioBus.Sfx, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RunAsync(_game, new ScriptedPlatform(0.01)));
    }

    [Fact]
    public async Task FixedUpdate_RecordingCommands_IsAllowed()
    {
        var platform = new ScriptedPlatform(GameEngine.FixedDeltaTime);
        _game.OnFixedUpdate = _ => _game.Audio!.SetBusVolume(AudioBus.Sfx, 0.5f);

        await _engine.RunAsync(_game, platform);

        Assert.Equal(
            [new Testing.AudioCommand(AudioCommandKind.SetBusVolume, [(int)AudioBus.Sfx, 0.5])],
            Testing.FakeAudioBackend.Decode(platform.AudioBackend.Frames[0]));
    }

    [Fact]
    public async Task RunAsync_EachRun_GetsNewAudio()
    {
        await _engine.RunAsync(_game, new ScriptedPlatform(0.01));
        var first = _game.Audio;

        await _engine.RunAsync(_game, new ScriptedPlatform(0.01));

        Assert.NotNull(first);
        Assert.NotSame(first, _game.Audio);
    }

    [Fact]
    public async Task RunAsync_FrameThrows_StillDestroysSounds()
    {
        var platform = new ScriptedPlatform(0.01);
        _game.OnUpdateAudio = static audio => audio.CreateSound([1]);
        _game.OnRender = static _ => throw new InvalidOperationException("render failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RunAsync(_game, platform));

        Assert.Empty(platform.AudioBackend.Sounds);
    }
}
