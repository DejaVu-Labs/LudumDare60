using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Input;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;

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
    private Graphics? _graphics;
    private Audio? _audio;
    private SpriteBatch? _batch;
    private readonly InputRouter _input = new();
    private PixelViewport _viewport;
    private double _totalTime;
    private long _frameIndex;
    private double _accumulator;
    private long _fixedStepIndex;

    /// <param name="virtualWidth">Width of the virtual screen the game draws, in virtual pixels.</param>
    /// <param name="virtualHeight">Height of the virtual screen.</param>
    /// <exception cref="ArgumentOutOfRangeException">A side is less than 1.</exception>
    public GameEngine(int virtualWidth, int virtualHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(virtualWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(virtualHeight, 1);
        VirtualWidth = virtualWidth;
        VirtualHeight = virtualHeight;
    }

    /// <summary>
    /// Width of the virtual screen in virtual pixels. The renderer scales it up to the drawing surface (see
    /// <see cref="PixelViewport"/>).
    /// </summary>
    public int VirtualWidth { get; }

    /// <inheritdoc cref="VirtualWidth"/>
    public int VirtualHeight { get; }

    /// <summary>
    /// Runs <paramref name="game"/> on <paramref name="platform"/> until the platform stops. Frame counters and
    /// accumulated time start from zero on every run. Each run gets a new <see cref="AssetManager"/> on
    /// <see cref="IPlatform.Assets"/>, a new <see cref="Graphics"/> on <see cref="IPlatform.Graphics"/> and a new
    /// <see cref="Audio"/> on <see cref="IPlatform.Audio"/>: all three are passed to <see cref="IGame.Initialize"/>
    /// before the first frame, and everything they hold is unloaded or destroyed when the run ends.
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
        var graphics = new Graphics(platform.Graphics);
        var audio = new Audio(platform.Audio);
        _game = game;
        _assets = assets;
        _graphics = graphics;
        _audio = audio;
        _batch = new SpriteBatch(graphics);
        _viewport = PixelViewport.Fit(VirtualWidth, VirtualHeight, VirtualWidth, VirtualHeight, 1);
        _input.Reset(in _viewport);
        _totalTime = 0;
        _frameIndex = 0;
        _accumulator = 0;
        _fixedStepIndex = 0;
        try
        {
            game.Initialize(assets, graphics, audio);
            await platform.RunAsync(this);
        }
        catch
        {
            EndRun(assets, graphics, audio, runFailed: true);
            throw;
        }

        EndRun(assets, graphics, audio, runFailed: false);
    }

    /// <summary>
    /// Applies finished asset reads (<see cref="AssetManager.Update"/>) and this frame's resize events, then runs the
    /// pending fixed steps, then <see cref="IGame.Update"/>, then <see cref="IGame.PrepareRender"/>. Each fixed step
    /// sees the input events that happened before its end (see <see cref="FrameContext.Input"/>).
    /// </summary>
    /// <param name="dt">Time since the previous frame, in seconds. Clamped to [0, <see cref="MaxDeltaTime"/>];
    /// NaN counts as zero.</param>
    /// <param name="events">Raw input events of this frame. Passed to <see cref="IGame.Update"/> as is, without
    /// copying. The last <see cref="RawInputEventKind.Resize"/> among them sets <see cref="FrameContext.Viewport"/>.</param>
    /// <exception cref="InvalidOperationException">No game is running.</exception>
    public void Update(double dt, ReadOnlySpan<RawInputEvent> events)
    {
        var game = GetRunningGame();
        _graphics!.BeginFrame();
        _audio!.BeginFrame();
        _assets!.Update();
        ApplyResize(events);

        var deltaTime = double.IsNaN(dt) ? 0 : Math.Clamp(dt, 0, MaxDeltaTime);
        _input.BeginFrame(events, deltaTime, in _viewport);

        _accumulator += deltaTime;
        while (_accumulator >= FixedDeltaTime)
        {
            _accumulator -= FixedDeltaTime;

            // The accumulator is how far the frame start lies past this step's end.
            _input.BeginFixedStep(-_accumulator, in _viewport);
            var fixedContext = new FrameContext(
                FixedDeltaTime, (_fixedStepIndex + 1) * FixedDeltaTime, _fixedStepIndex, _viewport, _input.Fixed);
            game.FixedUpdate(in fixedContext);
            _fixedStepIndex++;
        }

        _totalTime += deltaTime;
        var context = new FrameContext(deltaTime, _totalTime, _frameIndex, _viewport, _input.Frame);
        game.Update(in context, events);
        _frameIndex++;

        _audio.BeginRender();
        game.PrepareRender(_accumulator / FixedDeltaTime);
    }

    /// <summary>
    /// Lets <see cref="IGame.Render"/> record the frame's sprites, then submits them to the GPU in one call and deletes
    /// the textures destroyed during the frame. Then submits the frame's audio commands in one call, applies the
    /// platform's decode results and deletes the sounds destroyed during the frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">No game is running.</exception>
    public void Render()
    {
        var game = GetRunningGame();
        var batch = _batch!;
        batch.Begin(in _viewport);
        game.Render(batch);
        batch.End();
        _graphics!.EndFrame();
        _audio!.EndFrame();
    }

    /// <summary>
    /// Stops accepting frames, unloads the run's assets and destroys its textures and sounds. After a failed run, a
    /// failing unload or destroy is dropped, so the caller sees the exception that ended the run, not a later one.
    /// </summary>
    private void EndRun(AssetManager assets, Graphics graphics, Audio audio, bool runFailed)
    {
        _game = null;
        _assets = null;
        _graphics = null;
        _audio = null;
        _batch = null;
        try
        {
            assets.UnloadAll();
        }
        catch (AggregateException) when (runFailed)
        {
            // Every asset is unloaded anyway (see AssetManager.UnloadAll); the run's exception is rethrown.
        }
        finally
        {
            try
            {
                DestroyTextures(graphics, runFailed);
            }
            finally
            {
                DestroySounds(audio, runFailed);
            }
        }
    }

    private static void DestroyTextures(Graphics graphics, bool runFailed)
    {
        try
        {
            graphics.DestroyAll();
        }
        catch (AggregateException) when (runFailed)
        {
            // Every texture is destroyed anyway (see Graphics.DestroyAll); the run's exception is rethrown.
        }
    }

    private static void DestroySounds(Audio audio, bool runFailed)
    {
        try
        {
            audio.DestroyAll();
        }
        catch (AggregateException) when (runFailed)
        {
            // Every sound is destroyed anyway (see Audio.DestroyAll); the run's exception is rethrown.
        }
    }

    private void ApplyResize(ReadOnlySpan<RawInputEvent> events)
    {
        for (var i = events.Length - 1; i >= 0; i--)
        {
            ref readonly var e = ref events[i];
            if (e.Kind == RawInputEventKind.Resize)
            {
                _viewport = PixelViewport.Fit(VirtualWidth, VirtualHeight, e.X, e.Y, e.Z);
                return;
            }
        }
    }

    private IGame GetRunningGame() => _game ?? throw new InvalidOperationException("The engine is not running.");
}
