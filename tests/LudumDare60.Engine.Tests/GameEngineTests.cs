using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;

namespace LudumDare60.Engine.Tests;

public sealed class GameEngineTests
{
    private const double Tolerance = 1e-9;
    private const double F = GameEngine.FixedDeltaTime;

    private readonly GameEngine _engine = new(640, 360);
    private readonly RecordingGame _game = new();

    private Task RunFramesAsync(params double[] deltaTimes) => _engine.RunAsync(_game, new ScriptedPlatform(deltaTimes));

    [Fact]
    public async Task RunAsync_NullGame_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.RunAsync(null!, new ScriptedPlatform()));
    }

    [Fact]
    public async Task RunAsync_NullPlatform_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.RunAsync(_game, null!));
    }

    [Fact]
    public async Task RunAsync_PassesEngineToPlatformAsFrameSink()
    {
        var platform = new ScriptedPlatform();

        await _engine.RunAsync(_game, platform);

        Assert.Same(_engine, platform.FrameSink);
    }

    [Fact]
    public async Task RunAsync_CompletesWhenPlatformCompletes()
    {
        var platform = new ControlledPlatform();
        var run = _engine.RunAsync(_game, platform);

        Assert.False(run.IsCompleted);

        platform.Stop();
        await run;
    }

    [Fact]
    public async Task RunAsync_WhileRunning_Throws()
    {
        var platform = new ControlledPlatform();
        var run = _engine.RunAsync(_game, platform);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.RunAsync(_game, new ScriptedPlatform()));

        platform.Stop();
        await run;
    }

    [Fact]
    public async Task RunAsync_PlatformFails_PropagatesExceptionAndAllowsNewRun()
    {
        var platform = new ControlledPlatform();
        var run = _engine.RunAsync(_game, platform);
        var failure = new InvalidOperationException("platform failure");

        platform.Fail(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => run));
        await RunFramesAsync(0.01);
        Assert.Single(_game.Updates);
    }

    [Fact]
    public void Update_NotRunning_Throws() => Assert.Throws<InvalidOperationException>(() => _engine.Update(F, []));

    [Fact]
    public void Render_NotRunning_Throws() => Assert.Throws<InvalidOperationException>(_engine.Render);

    [Fact]
    public async Task Update_AfterRunCompleted_Throws()
    {
        await RunFramesAsync();

        Assert.Throws<InvalidOperationException>(() => _engine.Update(F, []));
    }

    [Fact]
    public async Task Frame_CallsFixedUpdateThenUpdateThenPrepareRenderThenRender()
    {
        await RunFramesAsync(0.02);

        Assert.Equal(["Initialize", "FixedUpdate", "Update", "PrepareRender", "Render"], _game.Calls);
    }

    [Fact]
    public async Task Update_DeltaBelowFixedStep_SkipsFixedUpdate()
    {
        await RunFramesAsync(0.01);

        Assert.Equal(["Initialize", "Update", "PrepareRender", "Render"], _game.Calls);
    }

    [Fact]
    public async Task Update_SmallDeltas_AccumulateIntoFixedStep()
    {
        await RunFramesAsync(0.01, 0.01);

        Assert.Equal(
            ["Initialize", "Update", "PrepareRender", "Render", "FixedUpdate", "Update", "PrepareRender", "Render"],
            _game.Calls);
    }

    [Fact]
    public async Task Update_LargeDelta_RunsSeveralFixedSteps()
    {
        await RunFramesAsync(3.6 * F);

        Assert.Equal(3, _game.FixedUpdates.Count);
        for (var i = 0; i < _game.FixedUpdates.Count; i++)
        {
            var context = _game.FixedUpdates[i];
            Assert.Equal(F, context.DeltaTime);
            Assert.Equal((i + 1) * F, context.TotalTime, Tolerance);
            Assert.Equal(i, context.FrameIndex);
        }
    }

    [Fact]
    public async Task Update_FixedStepIndex_ContinuesAcrossFrames()
    {
        await RunFramesAsync(1.5 * F, 1.5 * F);

        Assert.Equal([0L, 1L, 2L], _game.FixedUpdates.Select(static c => c.FrameIndex));
        Assert.Equal(3 * F, _game.FixedUpdates[^1].TotalTime, Tolerance);
    }

    [Fact]
    public async Task Update_Context_TracksDeltaTotalTimeAndFrameIndex()
    {
        await RunFramesAsync(0.01, 0.02, 0.03);

        Assert.Equal(3, _game.Updates.Count);
        Assert.Equal(0.01, _game.Updates[0].DeltaTime);
        Assert.Equal(0.01, _game.Updates[0].TotalTime);
        Assert.Equal(0, _game.Updates[0].FrameIndex);
        Assert.Equal(0.02, _game.Updates[1].DeltaTime);
        Assert.Equal(0.03, _game.Updates[1].TotalTime, Tolerance);
        Assert.Equal(1, _game.Updates[1].FrameIndex);
        Assert.Equal(0.06, _game.Updates[2].TotalTime, Tolerance);
        Assert.Equal(2, _game.Updates[2].FrameIndex);
    }

    [Theory]
    [InlineData(0.6)]
    [InlineData(1.2)]
    [InlineData(2.95)]
    public async Task PrepareRender_Alpha_IsRemainderOfFixedStep(double steps)
    {
        await RunFramesAsync(steps * F);

        Assert.Equal(steps - Math.Floor(steps), Assert.Single(_game.Alphas), 1e-6);
    }

    [Fact]
    public async Task PrepareRender_Alpha_StaysInUnitRange()
    {
        await RunFramesAsync(0.001, 0.016, 0.017, 0.033, 0.1, 0.25, 1, 0);

        Assert.All(_game.Alphas, static alpha => Assert.InRange(alpha, 0, 1 - double.Epsilon));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public async Task Update_DeltaAboveMax_IsClampedToMaxDeltaTime(double dt)
    {
        await RunFramesAsync(dt);

        Assert.Equal(GameEngine.MaxDeltaTime, Assert.Single(_game.Updates).DeltaTime);
        Assert.InRange(_game.FixedUpdates.Count, (int)(GameEngine.MaxDeltaTime / F) - 1, (int)(GameEngine.MaxDeltaTime / F));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.01)]
    [InlineData(double.NegativeInfinity)]
    public async Task Update_InvalidDelta_CountsAsZero(double dt)
    {
        await RunFramesAsync(dt);

        var context = Assert.Single(_game.Updates);
        Assert.Equal(0, context.DeltaTime);
        Assert.Equal(0, context.TotalTime);
        Assert.Empty(_game.FixedUpdates);
        Assert.Equal(0, Assert.Single(_game.Alphas));
    }

    [Fact]
    public async Task RunAsync_SecondRun_ResetsTimeAndCounters()
    {
        await RunFramesAsync(2.5 * F);
        var secondGame = new RecordingGame();

        await _engine.RunAsync(secondGame, new ScriptedPlatform(0.5 * F));

        var context = Assert.Single(secondGame.Updates);
        Assert.Equal(0, context.FrameIndex);
        Assert.Equal(0.5 * F, context.TotalTime, Tolerance);
        Assert.Empty(secondGame.FixedUpdates);
        Assert.Equal(0.5, Assert.Single(secondGame.Alphas), 1e-6);
    }

    [Fact]
    public async Task Update_PassesEachFrameEventsToGameUpdate()
    {
        RawInputEvent[] first = [KeyDown(Key.W), KeyDown(Key.A)];
        RawInputEvent[] third = [new(RawInputEventKind.KeyUp, 0, (ushort)Key.W, 0, -0.004f, 0, 0, 0)];

        await _engine.RunAsync(_game, ScriptedPlatform.FromFrames(new(0.02, first), new(0.02, []), new(0.02, third)));

        Assert.Equal(3, _game.UpdateEvents.Count);
        Assert.Equal(first, _game.UpdateEvents[0]);
        Assert.Empty(_game.UpdateEvents[1]);
        Assert.Equal(third, _game.UpdateEvents[2]);
    }

    [Fact]
    public async Task Update_PassesPlatformMemoryWithoutCopying()
    {
        RawInputEvent[] events = [KeyDown(Key.Space)];
        var game = new SpanIdentityGame(events);

        await _engine.RunAsync(game, ScriptedPlatform.FromFrames(new ScriptedFrame(0.01, events)));

        Assert.True(game.SawPlatformMemory);
    }

    [Fact]
    public async Task Update_EventsAndFixedSteps_PassesEventsOnlyOnceToUpdate()
    {
        await _engine.RunAsync(_game, ScriptedPlatform.FromFrames(new ScriptedFrame(3.5 * F, [KeyDown(Key.D)])));

        Assert.Equal(3, _game.FixedUpdates.Count);
        Assert.Equal([KeyDown(Key.D)], Assert.Single(_game.UpdateEvents));
    }

    [Fact]
    public async Task RunAsync_CallsInitializeOnceBeforeThePlatformRuns()
    {
        var platform = new ControlledPlatform();
        var run = _engine.RunAsync(_game, platform);

        Assert.Equal(["Initialize"], _game.Calls);

        platform.Stop();
        await run;
    }

    [Fact]
    public async Task RunAsync_InitializeThrows_PropagatesAndAllowsNewRun()
    {
        var failure = new InvalidOperationException("initialize failure");
        _game.OnInitialize = (_, _) => throw failure;

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => RunFramesAsync(0.01)));
        Assert.Equal(["Initialize"], _game.Calls);

        _game.OnInitialize = null;
        await RunFramesAsync(0.01);
    }

    [Fact]
    public async Task Initialize_AssetManager_ReadsThroughPlatformAssets()
    {
        var source = new FakeAssetSource();
        _game.OnInitialize = static (assets, _) => assets.Request<byte[]>("config.json");

        await _engine.RunAsync(_game, new ScriptedPlatform { Assets = source });

        Assert.Equal(["config.json"], source.Reads);
    }

    [Fact]
    public async Task RunAsync_EachRun_GetsNewAssetManager()
    {
        await RunFramesAsync();
        var first = _game.Assets;

        await RunFramesAsync();

        Assert.NotNull(first);
        Assert.NotSame(first, _game.Assets);
    }

    [Fact]
    public async Task Update_AppliesFinishedAssetReadsBeforeFixedUpdate()
    {
        var source = new FakeAssetSource { Files = { ["level.txt"] = "level"u8.ToArray() } };
        var game = new AssetProbeGame("level.txt");

        await _engine.RunAsync(game, new ScriptedPlatform(1.5 * F) { Assets = source });

        Assert.Equal([AssetState.Loaded], game.FixedUpdateStates);
    }

    [Fact]
    public async Task RunAsync_End_UnloadsAssets()
    {
        var source = new FakeAssetSource { Files = { ["level.txt"] = "level"u8.ToArray() } };
        var game = new AssetProbeGame("level.txt");

        await _engine.RunAsync(game, new ScriptedPlatform(0.01) { Assets = source });

        Assert.Equal("level", Assert.Single(game.Loader.Unloaded).Text);
        Assert.Throws<InvalidOperationException>(() => game.Assets!.GetState(game.Handle));
    }

    [Fact]
    public async Task RunAsync_PlatformFails_StillUnloadsAssets()
    {
        var source = new FakeAssetSource { Files = { ["level.txt"] = "level"u8.ToArray() } };
        var game = new AssetProbeGame("level.txt");
        var platform = new ControlledPlatform { Assets = source };
        var run = _engine.RunAsync(game, platform);
        _engine.Update(0.01, []);

        platform.Fail(new InvalidOperationException("platform failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Single(game.Loader.Unloaded);
    }

    [Fact]
    public async Task RunAsync_PlatformFailsAndUnloadThrows_PropagatesPlatformException()
    {
        var source = new FakeAssetSource { Files = { ["level.txt"] = "level"u8.ToArray() } };
        var game = new AssetProbeGame("level.txt");
        game.Loader.OnUnload = static _ => throw new InvalidOperationException("unload failure");
        var platform = new ControlledPlatform { Assets = source };
        var run = _engine.RunAsync(game, platform);
        _engine.Update(0.01, []);
        var failure = new InvalidOperationException("platform failure");

        platform.Fail(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => run));
        Assert.Single(game.Loader.Unloaded);
    }

    [Fact]
    public async Task RunAsync_UnloadThrowsAfterCleanEnd_Throws()
    {
        var source = new FakeAssetSource { Files = { ["level.txt"] = "level"u8.ToArray() } };
        var game = new AssetProbeGame("level.txt");
        game.Loader.OnUnload = static _ => throw new InvalidOperationException("unload failure");

        await Assert.ThrowsAsync<AggregateException>(
            () => _engine.RunAsync(game, new ScriptedPlatform(0.01) { Assets = source }));
    }

    private static RawInputEvent KeyDown(Key key) => new(RawInputEventKind.KeyDown, 0, (ushort)key, 0, -0.008f, 0, 0, 0);

    /// <summary>
    /// Checks that <see cref="IGame.Update"/> sees the platform's own memory, not a copy.
    /// </summary>
    private sealed class SpanIdentityGame(RawInputEvent[] platformEvents) : IGame
    {
        public bool SawPlatformMemory { get; private set; }

        public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
        {
        }

        public void FixedUpdate(in FrameContext context)
        {
        }

        public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
        {
            SawPlatformMemory = events.Length == platformEvents.Length
                && Unsafe.AreSame(ref MemoryMarshal.GetReference(events), ref platformEvents[0]);
        }

        public void PrepareRender(double alpha)
        {
        }

        public void Render(SpriteBatch batch)
        {
        }
    }

    /// <summary>
    /// Requests one text asset in <see cref="Initialize"/> and records its state in every fixed step.
    /// </summary>
    private sealed class AssetProbeGame(string path) : IGame
    {
        public TextLoader Loader { get; } = new();

        public AssetManager? Assets { get; private set; }

        public AssetHandle<TextAsset> Handle { get; private set; }

        public List<AssetState> FixedUpdateStates { get; } = [];

        public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
        {
            Assets = assets;
            assets.Register(Loader);
            Handle = assets.Request<TextAsset>(path);
        }

        public void FixedUpdate(in FrameContext context) => FixedUpdateStates.Add(Assets!.GetState(Handle));

        public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
        {
        }

        public void PrepareRender(double alpha)
        {
        }

        public void Render(SpriteBatch batch)
        {
        }
    }
}
