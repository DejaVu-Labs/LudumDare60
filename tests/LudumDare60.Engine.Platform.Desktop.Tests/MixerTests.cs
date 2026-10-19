using System.Buffers.Binary;
using System.Text.Json;
using LudumDare60.Engine.Platform.Desktop.Sounds;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class MixerTests
{
    private const int Rate = 1000;
    private const int Sfx = (int)AudioBus.Sfx;
    private const int Music = (int)AudioBus.Music;

    private readonly Mixer _mixer = new(Rate);

    private static readonly float s_center = MathF.Cos(MathF.PI / 4);

    private static SoundData Constant(int frames, float value = 1, int channels = 1, int sampleRate = Rate)
        => new([.. Enumerable.Repeat(value, frames * channels)], channels, sampleRate);

    private void AddReady(int id, SoundData data)
    {
        _mixer.AddSound(id);
        _mixer.SetSoundData(id, data);
    }

    private float[] Mix(int frames)
    {
        var output = new float[frames * 2];
        _mixer.Mix(output);
        return output;
    }

    private void Run(params object[] words)
    {
        var bytes = new byte[words.Length * 4];
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] is float value)
            {
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), value);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), (int)words[i]);
            }
        }

        _mixer.Run(bytes);
    }

    private void Play(int sound, int bus = Sfx, float volume = 1, float pitch = 1, float pan = 0)
        => Run(1, sound, bus, volume, pitch, pan);

    private void PlayLoop(int voice, int sound, int bus = Sfx, float volume = 1, float pitch = 1, float pan = 0, float fadeIn = 0)
        => Run(2, voice, sound, bus, volume, pitch, pan, fadeIn);

    [Fact]
    public void Mix_NothingPlaying_IsSilent()
        => Assert.All(Mix(16), static sample => Assert.Equal(0, sample));

    [Fact]
    public void Play_MonoCentered_UsesEqualPowerPan()
    {
        AddReady(0, Constant(100));
        Play(0);

        var output = Mix(10);

        Assert.Equal(s_center, output[0], 5);
        Assert.Equal(s_center, output[1], 5);
    }

    [Theory]
    [InlineData(-1f, 1f, 0f)]
    [InlineData(1f, 0f, 1f)]
    [InlineData(-5f, 1f, 0f)]
    public void Play_MonoPannedHard_GoesToOneSide(float pan, float left, float right)
    {
        AddReady(0, Constant(100));
        Play(0, pan: pan);

        var output = Mix(1);

        Assert.Equal(left, output[0], 5);
        Assert.Equal(right, output[1], 5);
    }

    [Fact]
    public void Play_StereoCentered_PassesChannelsThrough()
    {
        AddReady(0, new SoundData([0.5f, -0.25f, 0.5f, -0.25f], 2, Rate));
        Play(0);

        var output = Mix(1);

        Assert.Equal([0.5f, -0.25f], output);
    }

    [Fact]
    public void Play_StereoPannedLeft_MovesRightChannelIntoLeft()
    {
        AddReady(0, new SoundData([0.25f, 0.5f, 0.25f, 0.5f], 2, Rate));
        Play(0, pan: -1);

        var output = Mix(1);

        Assert.Equal(0.75f, output[0], 5);
        Assert.Equal(0, output[1], 5);
    }

    [Fact]
    public void Play_VolumeAndBusGains_Multiply()
    {
        AddReady(0, Constant(5000));
        Run(5, (int)AudioBus.Master, 0.5f);
        Run(5, Sfx, 0.5f);
        Mix(200);
        Play(0, volume: 0.5f, pan: -1);

        var output = Mix(1);

        Assert.Equal(0.125f, output[0], 4);
    }

    [Fact]
    public void Play_OneShot_EndsAfterTheSoundAndFreesItsVoice()
    {
        AddReady(0, Constant(10));
        Play(0);
        Assert.Equal(1, _mixer.ActiveVoices);

        var output = Mix(20);

        Assert.Equal(s_center, output[2 * 9], 5);
        Assert.Equal(0, output[2 * 10]);
        Assert.Equal(0, _mixer.ActiveVoices);
        Assert.Equal(0, _mixer.PlayingVoices);
    }

    [Theory]
    [InlineData(2f, Rate, 5)]
    [InlineData(0.5f, Rate, 20)]
    [InlineData(1f, Rate / 2, 20)]
    [InlineData(1f, Rate * 2, 5)]
    public void Play_PitchAndSampleRate_SetHowLongTheSoundLasts(float pitch, int sampleRate, int expectedFrames)
    {
        AddReady(0, Constant(10, sampleRate: sampleRate));
        Play(0, pitch: pitch);

        var output = Mix(40);

        var audible = Enumerable.Range(0, 40).Count(frame => output[2 * frame] != 0);
        Assert.Equal(expectedFrames, audible);
    }

    [Fact]
    public void Play_HalfPitch_InterpolatesBetweenSamples()
    {
        AddReady(0, new SoundData([0, 1, 0, 0], 1, Rate));
        Play(0, pitch: 0.5f, pan: -1);

        var output = Mix(4);

        Assert.Equal([0f, 0.5f, 1f, 0.5f], [output[0], output[2], output[4], output[6]]);
    }

    [Fact]
    public void Play_SoundStillDecoding_IsDropped()
    {
        _mixer.AddSound(0);
        Play(0);

        Assert.Equal(0, _mixer.PlayingVoices);
    }

    [Fact]
    public void Play_BeyondTheVoiceLimit_IsDroppedButMusicIsNot()
    {
        AddReady(0, Constant(1000));
        for (var i = 0; i < AudioLimits.MaxVoices + 1; i++)
        {
            Play(0);
        }

        Play(0, bus: Music);

        Assert.Equal(AudioLimits.MaxVoices, _mixer.ActiveVoices);
        Assert.Equal(AudioLimits.MaxVoices + 1, _mixer.PlayingVoices);
    }

    [Fact]
    public void PlayLoop_BeyondTheVoiceLimit_StillPlays()
    {
        AddReady(0, Constant(1000));
        for (var i = 0; i < AudioLimits.MaxVoices; i++)
        {
            Play(0);
        }

        PlayLoop(0, 0);

        Assert.Equal(AudioLimits.MaxVoices + 1, _mixer.ActiveVoices);
    }

    [Fact]
    public void PlayLoop_WrapsAround()
    {
        AddReady(0, new SoundData([1, 0, 0, 0], 1, Rate));
        PlayLoop(0, 0, pan: -1);

        var output = Mix(9);

        Assert.Equal([1f, 0, 0, 0, 1, 0, 0, 0, 1], Enumerable.Range(0, 9).Select(frame => output[2 * frame]));
    }

    [Fact]
    public void PlayLoop_FadeIn_RampsLinearly()
    {
        AddReady(0, Constant(1000));
        PlayLoop(0, 0, pan: -1, fadeIn: 0.1f);

        var output = Mix(200);

        Assert.Equal(0.01f, output[0], 4);
        Assert.Equal(0.5f, output[2 * 49], 4);
        Assert.Equal(1f, output[2 * 99], 4);
        Assert.Equal(1f, output[2 * 150], 4);
    }

    [Fact]
    public void PlayLoop_SoundStillDecoding_StartsWhenReady()
    {
        _mixer.AddSound(0);
        PlayLoop(0, 0, pan: -1, volume: 0.5f);
        Assert.All(Mix(10), static sample => Assert.Equal(0, sample));
        Run(3, 0, 0.25f, 1f, -1f);

        _mixer.SetSoundData(0, Constant(100));

        Assert.Equal(0.25f, Mix(1)[0], 5);
    }

    [Fact]
    public void SetVoice_GlidesToTheNewVolume()
    {
        AddReady(0, Constant(1000));
        PlayLoop(0, 0, pan: -1);
        Mix(10);

        Run(3, 0, 0.5f, 1f, -1f);
        var output = Mix(100);

        Assert.InRange(output[0], 0.5f, 1f);
        Assert.Equal(0.5f, output[2 * 99], 4);
    }

    [Fact]
    public void StopVoice_FadesOutThenFreesTheVoice()
    {
        AddReady(0, Constant(1000));
        PlayLoop(0, 0, pan: -1);
        Mix(10);

        Run(4, 0, 0.05f);

        Assert.Equal(0, _mixer.ActiveVoices);
        PlayLoop(0, 0, pan: -1, volume: 0);
        var output = Mix(60);
        Assert.Equal(0.5f, output[2 * 24], 2);
        Assert.Equal(0, output[2 * 55]);
        Assert.Equal(1, _mixer.PlayingVoices);
    }

    [Fact]
    public void StopVoice_WithoutFade_StillFadesOverTenMilliseconds()
    {
        AddReady(0, Constant(1000));
        PlayLoop(0, 0, pan: -1);
        Run(4, 0, 0f);

        var output = Mix(20);

        Assert.True(output[2 * 5] > 0);
        Assert.Equal(0, output[2 * 15]);
    }

    [Fact]
    public void StopVoice_LoopStillWaiting_RemovesItAtOnce()
    {
        _mixer.AddSound(0);
        PlayLoop(0, 0);

        Run(4, 0, 1f);

        Assert.Equal(0, _mixer.PlayingVoices);
        Assert.Equal(0, _mixer.ActiveVoices);
    }

    [Fact]
    public void SetBusVolume_MasterZero_SilencesEverything()
    {
        AddReady(0, Constant(1000));
        PlayLoop(0, 0);
        Run(5, (int)AudioBus.Master, 0f);

        var output = Mix(100);

        Assert.Equal(0, output[2 * 99], 4);
    }

    [Fact]
    public void RemoveSound_StopsOneShotsAndFadesLoops()
    {
        AddReady(0, Constant(1000));
        Play(0);
        PlayLoop(0, 0);

        _mixer.RemoveSound(0);

        Assert.False(_mixer.HasSound(0));
        Assert.Equal(1, _mixer.PlayingVoices);
        Mix(20);
        Assert.Equal(0, _mixer.PlayingVoices);
        Assert.Throws<InvalidOperationException>(() => Run(3, 0, 1f, 1f, 0f));
    }

    [Fact]
    public void AddSound_Twice_Throws()
    {
        _mixer.AddSound(0);

        var exception = Assert.Throws<InvalidOperationException>(() => _mixer.AddSound(0));

        Assert.Equal("Sound 0 already exists.", exception.Message);
    }

    [Fact]
    public void Run_Errors_MatchWebAudio()
    {
        AddReady(0, Constant(10));
        PlayLoop(1, 0);

        Assert.Equal("Sound 7 does not exist.", Assert.Throws<InvalidOperationException>(() => Play(7)).Message);
        Assert.Equal("Voices cannot play on bus 0.", Assert.Throws<InvalidOperationException>(() => Play(0, bus: 0)).Message);
        Assert.Equal("Voices cannot play on bus 4.", Assert.Throws<InvalidOperationException>(() => PlayLoop(2, 0, bus: 4)).Message);
        Assert.Equal("Voice 1 already plays.", Assert.Throws<InvalidOperationException>(() => PlayLoop(1, 0)).Message);
        Assert.Equal("Voice 9 does not exist.", Assert.Throws<InvalidOperationException>(() => Run(4, 9, 0f)).Message);
        Assert.Equal("Unknown audio bus 4.", Assert.Throws<InvalidOperationException>(() => Run(5, 4, 1f)).Message);
        Assert.Equal("Unknown audio command 6 at byte 0.", Assert.Throws<InvalidOperationException>(() => Run(6)).Message);
        Assert.Equal("The audio command at byte 0 is cut off.", Assert.Throws<InvalidOperationException>(() => Run(1, 0, Sfx)).Message);
    }

    [Fact]
    public void Run_SharedFixture_PlaysEveryVoice()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "audio-frame.json")));
        foreach (var sound in fixture.RootElement.GetProperty("sounds").EnumerateArray())
        {
            AddReady(sound.GetInt32(), Constant(1000));
        }

        _mixer.Run(Convert.FromHexString(fixture.RootElement.GetProperty("commandsHex").GetString()!));

        // The one-shot and the music count; the stopped loop fades out but no longer counts.
        Assert.Equal(3, _mixer.PlayingVoices);
        Assert.Equal(1, _mixer.ActiveVoices);
        Assert.Contains(Mix(10), static sample => sample != 0);
    }
}
