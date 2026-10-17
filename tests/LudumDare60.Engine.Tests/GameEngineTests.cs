namespace LudumDare60.Engine.Tests;

public sealed class GameEngineTests
{
    private const double Tolerance = 1e-9;
    private const double F = GameEngine.FixedDeltaTime;

    private readonly GameEngine _engine = new();
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
    public void Update_NotRunning_Throws() => Assert.Throws<InvalidOperationException>(() => _engine.Update(F));

    [Fact]
    public void Render_NotRunning_Throws() => Assert.Throws<InvalidOperationException>(_engine.Render);

    [Fact]
    public async Task Update_AfterRunCompleted_Throws()
    {
        await RunFramesAsync();

        Assert.Throws<InvalidOperationException>(() => _engine.Update(F));
    }

    [Fact]
    public async Task Frame_CallsFixedUpdateThenUpdateThenPrepareRenderThenRender()
    {
        await RunFramesAsync(0.02);

        Assert.Equal(["FixedUpdate", "Update", "PrepareRender", "Render"], _game.Calls);
    }

    [Fact]
    public async Task Update_DeltaBelowFixedStep_SkipsFixedUpdate()
    {
        await RunFramesAsync(0.01);

        Assert.Equal(["Update", "PrepareRender", "Render"], _game.Calls);
    }

    [Fact]
    public async Task Update_SmallDeltas_AccumulateIntoFixedStep()
    {
        await RunFramesAsync(0.01, 0.01);

        Assert.Equal(["Update", "PrepareRender", "Render", "FixedUpdate", "Update", "PrepareRender", "Render"], _game.Calls);
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
        Assert.Equal(new FrameContext(0.01, 0.01, 0), _game.Updates[0]);
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
}
