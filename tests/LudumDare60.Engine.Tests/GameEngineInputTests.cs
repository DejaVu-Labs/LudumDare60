using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// Which fixed step and which frame see an input event (docs/tech/architecture.md#input-state).
/// </summary>
public sealed class GameEngineInputTests
{
    private const double F = GameEngine.FixedDeltaTime;

    private readonly GameEngine _engine = new(640, 360);
    private readonly RecordingGame _game = new();

    /// <summary>
    /// Per fixed step: (A down, A pressed, A released).
    /// </summary>
    private readonly List<(bool Down, bool Pressed, bool Released)> _steps = [];

    /// <summary>
    /// Per frame, as <see cref="IGame.Update"/> sees it.
    /// </summary>
    private readonly List<(bool Down, bool Pressed, bool Released)> _frames = [];

    public GameEngineInputTests()
    {
        _game.OnFixedUpdate = c => _steps.Add((c.Input.IsDown(Key.A), c.Input.IsPressed(Key.A), c.Input.IsReleased(Key.A)));
        _game.OnUpdateContext = c => _frames.Add((c.Input.IsDown(Key.A), c.Input.IsPressed(Key.A), c.Input.IsReleased(Key.A)));
    }

    private Task RunAsync(params ScriptedFrame[] frames) => _engine.RunAsync(_game, ScriptedPlatform.FromFrames(frames));

    private static RawInputEvent Down(double time) => InputStateTests.KeyDown(Key.A, (float)time);

    private static RawInputEvent Up(double time) => InputStateTests.KeyUp(Key.A, (float)time);

    [Fact]
    public async Task Press_ReachesTheStepItHappenedIn()
    {
        // Three steps end at -2F, -F and 0; the press falls into the second.
        await RunAsync(new ScriptedFrame(3 * F, [Down(-1.5 * F)]));

        Assert.Equal([(false, false, false), (true, true, false), (true, false, false)], _steps);
    }

    [Fact]
    public async Task Press_AtFrameStart_ReachesTheLastStep()
    {
        await RunAsync(new ScriptedFrame(F, [Down(0)]));

        Assert.Equal([(true, true, false)], _steps);
    }

    [Fact]
    public async Task Press_InFrameWithoutSteps_ReachesTheNextStepOnce()
    {
        await RunAsync(
            new ScriptedFrame(0.5 * F, [Down(-0.25 * F)]),
            new ScriptedFrame(0.75 * F, []),
            new ScriptedFrame(F, []));

        Assert.Equal([(true, true, false), (true, false, false)], _steps);
    }

    [Fact]
    public async Task PressAndRelease_InOneStep_BothEdgesNotDown()
    {
        await RunAsync(new ScriptedFrame(F, [Down(-0.8 * F), Up(-0.2 * F)]));

        Assert.Equal([(false, true, true)], _steps);
    }

    [Fact]
    public async Task PressAndRelease_InTwoSteps_OneEdgeEach()
    {
        await RunAsync(new ScriptedFrame(2 * F, [Down(-1.5 * F), Up(-0.5 * F)]));

        Assert.Equal([(true, true, false), (false, false, true)], _steps);
    }

    [Fact]
    public async Task Update_SeesEdgesOncePerFrame()
    {
        await RunAsync(
            new ScriptedFrame(3 * F, [Down(-2.5 * F)]),
            new ScriptedFrame(F, []));

        Assert.Equal([(true, true, false), (true, false, false)], _frames);
    }

    [Fact]
    public async Task Update_SeesEventsThatWaitForTheNextStep()
    {
        await RunAsync(new ScriptedFrame(0.5 * F, [Down(-0.1 * F)]));

        Assert.Empty(_steps);
        Assert.Equal([(true, true, false)], _frames);
    }

    [Fact]
    public async Task RunAsync_NewRun_StartsWithNothingDown()
    {
        await RunAsync(new ScriptedFrame(F, [Down(0)]));
        _steps.Clear();
        _frames.Clear();

        await RunAsync(new ScriptedFrame(F, []));

        Assert.Equal([(false, false, false)], _steps);
        Assert.Equal([(false, false, false)], _frames);
    }
}
