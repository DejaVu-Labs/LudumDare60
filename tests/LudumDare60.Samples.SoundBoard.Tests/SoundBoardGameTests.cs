using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Samples.SoundBoard.Tests;

public sealed class SoundBoardGameTests
{
    private static readonly string[] s_allPaths =
        [.. SoundPaths.Effects, SoundPaths.Rain, SoundPaths.TrackA, SoundPaths.TrackB];

    private readonly ManualPlatform _platform = new() { Assets = new InMemoryAssets() };
    private readonly SoundBoardGame _game = new();

    public SoundBoardGameTests()
        => _ = new GameEngine(SoundBoardGame.VirtualWidth, SoundBoardGame.VirtualHeight).RunAsync(_game, _platform);

    private FakeAudioBackend Audio => _platform.AudioBackend;

    private List<AudioCommand> Frame(params RawInputEvent[] events)
    {
        _platform.Frame(1.0 / 60, events);
        return Audio.LastCommands();
    }

    /// <summary>
    /// Runs frames until every file is read and handed to the audio device, then reports every sound decoded.
    /// </summary>
    private void LoadAll()
    {
        Frame();
        Frame();
        Assert.Equal(s_allPaths.Length, _game.CreatedCount);
        Audio.ReportReady([.. Audio.Sounds.Keys]);
        Frame();
    }

    private static RawInputEvent KeyDown(Key key) => new(RawInputEventKind.KeyDown, 0, (ushort)key, 0, 0, 0, 0, 0);

    private static RawInputEvent KeyUp(Key key) => new(RawInputEventKind.KeyUp, 0, (ushort)key, 0, 0, 0, 0, 0);

    private static RawInputEvent PointerAt(float x, float y)
        => new(RawInputEventKind.PointerMove, 0, (ushort)PointerType.Mouse, 1, 0, x, y, 0);

    private List<AudioCommand> Press(Key key, params RawInputEvent[] before)
    {
        var commands = Frame([.. before, KeyDown(key)]);
        Frame(KeyUp(key));
        return commands;
    }

    [Fact]
    public void Update_FilesLoaded_CreatesEverySoundAndReleasesTheFiles()
    {
        Frame();
        Frame();

        Assert.Equal(s_allPaths.Length, _game.CreatedCount);
        Assert.Equal(s_allPaths.Length, Audio.Sounds.Count);
        Assert.Equal(Encoding(SoundPaths.Rain), Audio.Sounds[SoundPaths.Effects.Count]);
    }

    [Fact]
    public void DigitKey_SoundNotDecodedYet_PlaysNothing()
    {
        Frame();
        Frame();

        Assert.Empty(Press(Key.Digit1));
    }

    [Fact]
    public void DigitKey_PlaysEffectPannedByPointerWithVariedPitch()
    {
        LoadAll();

        var commands = Press(Key.Digit3, PointerAt(SoundBoardGame.VirtualWidth, 0));

        var play = Assert.Single(commands);
        Assert.Equal(AudioCommandKind.Play, play.Kind);
        Assert.Equal(2, play.Arguments[0]);
        Assert.Equal((int)AudioBus.Sfx, play.Arguments[1]);
        Assert.Equal(1, play.Arguments[2]);
        Assert.InRange(play.Arguments[3], 1 - SoundBoardGame.PitchVariation, 1 + SoundBoardGame.PitchVariation);
        Assert.Equal(1, play.Arguments[4]);
    }

    [Fact]
    public void DigitKey_Repeated_VariesPitch()
    {
        LoadAll();

        var first = Press(Key.Digit1)[0].Arguments[3];
        var second = Press(Key.Digit1)[0].Arguments[3];

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RainKey_StartsLoopThatFollowsThePointerAndStopsOnSecondPress()
    {
        LoadAll();

        var start = Press(Key.L, PointerAt(0, 0));

        Assert.True(_game.IsRainPlaying);
        Assert.Equal(
            new AudioCommand(AudioCommandKind.PlayLoop, [0, SoundPaths.Effects.Count, (int)AudioBus.Ambience, 1, 1, -1, SoundBoardGame.RainFade]),
            Assert.Single(start));

        var follow = Frame(PointerAt(SoundBoardGame.VirtualWidth / 2, SoundBoardGame.VirtualHeight));

        Assert.Equal(new AudioCommand(AudioCommandKind.SetVoice, [0, 0.2f, 1, 0]), Assert.Single(follow));

        var stop = Press(Key.L);

        Assert.False(_game.IsRainPlaying);
        Assert.Equal(new AudioCommand(AudioCommandKind.StopVoice, [0, SoundBoardGame.RainFade]), Assert.Single(stop));
    }

    [Fact]
    public void MusicKeys_CrossfadeBetweenTracksAndStopMusic()
    {
        LoadAll();
        var trackA = SoundPaths.Effects.Count + 1;
        var trackB = SoundPaths.Effects.Count + 2;
        var music = (int)AudioBus.Music;
        var fade = SoundBoardGame.MusicFade;

        Assert.Equal([new AudioCommand(AudioCommandKind.PlayLoop, [0, trackA, music, 1, 1, 0, fade])], Press(Key.Q));
        Assert.Empty(Press(Key.Q));
        Assert.Equal(
            [
                new AudioCommand(AudioCommandKind.StopVoice, [0, fade]),
                new AudioCommand(AudioCommandKind.PlayLoop, [0, trackB, music, 1, 1, 0, fade]),
            ],
            Press(Key.W));
        Assert.Equal([new AudioCommand(AudioCommandKind.StopVoice, [0, fade])], Press(Key.S));
    }

    [Fact]
    public void MuteKey_TogglesMasterVolume()
    {
        LoadAll();
        var master = (int)AudioBus.Master;

        Assert.Equal([new AudioCommand(AudioCommandKind.SetBusVolume, [master, 0])], Press(Key.M));
        Assert.True(_game.IsMuted);
        Assert.Equal([new AudioCommand(AudioCommandKind.SetBusVolume, [master, 1])], Press(Key.M));
        Assert.False(_game.IsMuted);
    }

    [Fact]
    public void Render_DrawsPadsBarTracksAndPointer()
    {
        LoadAll();

        var frame = _platform.Backend.Frames[^1];

        Assert.Equal(7 + 1 + 2 + 1, frame.Instances.Length);
    }

    private static byte[] Encoding(string path) => System.Text.Encoding.UTF8.GetBytes(path);

    /// <summary>
    /// Every sound path holds its own name as bytes; reads complete at once.
    /// </summary>
    private sealed class InMemoryAssets : IAssetSource
    {
        public Task<byte[]> ReadAsync(string path) => Task.FromResult(Encoding(path));
    }
}
