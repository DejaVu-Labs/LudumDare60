using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine;

/// <summary>
/// Runs an <see cref="IGame"/> on an <see cref="IPlatform"/>. The platform drives frames through <see cref="IFrameSink"/>.
/// The engine turns them into fixed simulation steps, a variable update and interpolated rendering.
/// </summary>
/// <remarks>See docs/tech/architecture.md for the frame loop.</remarks>
public sealed class GameEngine : IFrameSink
{
    /// <summary>
    /// Duration of one fixed simulation step, in seconds (60 Hz).
    /// </summary>
    public const double FixedDeltaTime = 1.0 / 60.0;

    /// <summary>
    /// Upper bound for a frame's delta time, in seconds. Longer frames (a hidden tab, a debugger pause) are clamped
    /// so the fixed-step loop cannot fall into a catch-up spiral.
    /// </summary>
    public const double MaxDeltaTime = 0.25;

    private IGame? _game;
    private AssetManager? _assets;
    private double _totalTime;
    private long _frameIndex;
    private double _accumulator;
    private long _fixedStepIndex;

    /// <summary>
    /// Runs <paramref name="game"/> on <paramref name="platform"/> until the platform stops. Frame counters and
    /// accumulated time start from zero on every run. Each run gets a new <see cref="AssetManager"/> on
    /// <see cref="IPlatform.Assets"/>: it is passed to <see cref="IGame.Initialize"/> before the first frame, and
    /// everything it holds is unloaded when the run ends.
    /// </summary>
    /// <exception cref="InvalidOperationException">The engine is already running a game.</exception>
    public async Task RunAsync(IGame game, IPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(platform);

        if (_game is not null)
        {
            throw new InvalidOperationException("The engine is already running.");
        }

        var assets = new AssetManager(platform.Assets);
        _game = game;
        _assets = assets;
        _totalTime = 0;
        _frameIndex = 0;
        _accumulator = 0;
        _fixedStepIndex = 0;
        try
        {
            game.Initialize(assets);
            await platform.RunAsync(this);
        }
        catch
        {
            EndRun(assets, runFailed: true);
            throw;
        }

        EndRun(assets, runFailed: false);
    }

    /// <summary>
    /// Applies finished asset reads (<see cref="AssetManager.Update"/>), then runs the pending fixed steps, then
    /// <see cref="IGame.Update"/>, then <see cref="IGame.PrepareRender"/>.
    /// </summary>
    /// <param name="dt">Time since the previous frame, in seconds. Clamped to [0, <see cref="MaxDeltaTime"/>];
    /// NaN counts as zero.</param>
    /// <param name="events">Raw input events of this frame. Passed to <see cref="IGame.Update"/> as is, without
    /// copying.</param>
    /// <exception cref="InvalidOperationException">No game is running.</exception>
    public void Update(double dt, ReadOnlySpan<RawInputEvent> events)
    {
        var game = GetRunningGame();
        _assets!.Update();

        var deltaTime = double.IsNaN(dt) ? 0 : Math.Clamp(dt, 0, MaxDeltaTime);

        _accumulator += deltaTime;
        while (_accumulator >= FixedDeltaTime)
        {
            _accumulator -= FixedDeltaTime;
            var fixedContext = new FrameContext(FixedDeltaTime, (_fixedStepIndex + 1) * FixedDeltaTime, _fixedStepIndex);
            game.FixedUpdate(in fixedContext);
            _fixedStepIndex++;
        }

        _totalTime += deltaTime;
        var context = new FrameContext(deltaTime, _totalTime, _frameIndex);
        game.Update(in context, events);
        _frameIndex++;

        game.PrepareRender(_accumulator / FixedDeltaTime);
    }

    /// <summary>
    /// Forwards to <see cref="IGame.Render"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No game is running.</exception>
    public void Render() => GetRunningGame().Render();

    /// <summary>
    /// Stops accepting frames and unloads the run's assets. After a failed run, a failing unload is dropped, so the
    /// caller sees the exception that ended the run, not a later one.
    /// </summary>
    private void EndRun(AssetManager assets, bool runFailed)
    {
        _game = null;
        _assets = null;
        try
        {
            assets.UnloadAll();
        }
        catch (AggregateException) when (runFailed)
        {
            // Every asset is unloaded anyway (see AssetManager.UnloadAll); the run's exception is rethrown.
        }
    }

    private IGame GetRunningGame() => _game ?? throw new InvalidOperationException("The engine is not running.");
}
