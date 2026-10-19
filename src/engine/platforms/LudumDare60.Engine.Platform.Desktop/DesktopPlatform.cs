using SDL3;

namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// <see cref="IPlatform"/> for Windows, Linux and macOS on SDL3: one window, frames from a loop on the calling thread.
/// It does not draw yet (<see cref="Graphics"/> discards every frame) and plays no sound (<see cref="Audio"/>), but it
/// runs the game with keyboard, mouse, touch and gamepad input and real asset files.
/// </summary>
/// <param name="title">Window title.</param>
/// <param name="width">Initial window width in logical pixels.</param>
/// <param name="height">Initial window height in logical pixels.</param>
/// <param name="assetRoot">Folder of the asset files. <see langword="null"/> for <c>assets/</c> next to the
/// executable.</param>
public sealed class DesktopPlatform(string title, int width, int height, string? assetRoot = null) : IPlatform
{
    /// <summary>
    /// Asset folder next to the executable, used unless the game says otherwise.
    /// </summary>
    public const string DefaultAssetFolder = "assets";

    /// <summary>
    /// Events passed to one frame. Events beyond it stay queued and arrive with the next frame.
    /// </summary>
    public const int InputCapacity = 512;

    private readonly FileAssetSource _assets =
        new(assetRoot ?? Path.Combine(AppContext.BaseDirectory, DefaultAssetFolder));

    private readonly Dictionary<uint, nint> _gamepads = [];

    /// <inheritdoc />
    /// <remarks>Files under the asset root, read on the thread pool.</remarks>
    public IAssetSource Assets => _assets;

    /// <inheritdoc />
    /// <remarks>Draws nothing yet: the desktop renderer does not exist.</remarks>
    public IGraphicsBackend Graphics { get; } = new NullGraphics();

    /// <inheritdoc />
    /// <remarks>Plays nothing yet: every sound reports ready and stays silent.</remarks>
    public IAudioBackend Audio { get; } = new SilentAudio();

    /// <inheritdoc />
    /// <remarks>Runs the frame loop on the calling thread, which must be the process's main thread (macOS needs that
    /// for windows), and returns once the player closes the window: the returned task is always complete. Exceptions,
    /// including a failure to create the window, are thrown from this call. Without a renderer there is no vsync, so
    /// the loop waits between frames to match the display's refresh rate.</remarks>
    /// <exception cref="InvalidOperationException">SDL could not start or create the window.</exception>
    public Task RunAsync(IFrameSink frameSink)
    {
        ArgumentNullException.ThrowIfNull(frameSink);

        // Touches arrive as finger events; SDL's mouse copies of them would report each touch twice.
        SDL.SetHint(SDL.Hints.TouchMouseEvents, "0");
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad))
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");
        }

        try
        {
            var window = SDL.CreateWindow(title, width, height, SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity);
            if (window == 0)
            {
                throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");
            }

            try
            {
                Run(window, frameSink);
            }
            finally
            {
                foreach (var gamepad in _gamepads.Values)
                {
                    SDL.CloseGamepad(gamepad);
                }

                _gamepads.Clear();
                SDL.DestroyWindow(window);
            }
        }
        finally
        {
            SDL.Quit();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Writes the error to the standard error stream.</remarks>
    public void ReportError(Exception exception, bool handled)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Console.Error.WriteLine(handled ? $"Handled error: {exception}" : $"Unhandled error: {exception}");
    }

    private void Run(nint window, IFrameSink frameSink)
    {
        var input = new SdlInput();
        var events = new RawInputEvent[InputCapacity];
        var clock = new FrameClock();
        var flags = SDL.GetWindowFlags(window);
        SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight);
        input.Start(
            SDL.GetTicksNS(),
            pixelWidth,
            pixelHeight,
            SDL.GetWindowPixelDensity(window),
            hasKeyboardFocus: flags.HasFlag(SDL.WindowFlags.InputFocus),
            visible: !flags.HasFlag(SDL.WindowFlags.Hidden) && !flags.HasFlag(SDL.WindowFlags.Minimized));

        while (true)
        {
            while (SDL.PollEvent(out var e))
            {
                switch ((SDL.EventType)e.Type)
                {
                    case SDL.EventType.Quit:
                        return;
                    case SDL.EventType.WindowPixelSizeChanged:
                    case SDL.EventType.WindowDisplayScaleChanged:
                        SDL.GetWindowSizeInPixels(window, out pixelWidth, out pixelHeight);
                        input.SetSize(e.Window.Timestamp, pixelWidth, pixelHeight, SDL.GetWindowPixelDensity(window));
                        break;
                    case SDL.EventType.GamepadAdded:
                        OpenGamepad(e.GDevice.Which);
                        input.Handle(in e);
                        break;
                    case SDL.EventType.GamepadRemoved:
                        input.Handle(in e);
                        CloseGamepad(e.GDevice.Which);
                        break;
                    default:
                        input.Handle(in e);
                        break;
                }
            }

            var frameStartNs = SDL.GetTicksNS();
            var count = input.Drain(events, frameStartNs);
            frameSink.Update(clock.Tick(frameStartNs), events.AsSpan(0, count));
            frameSink.Render();

            var periodNs = FrameClock.GetFramePeriodNs(GetRefreshRate(window));
            var waitNs = FrameClock.GetWaitNs(frameStartNs, SDL.GetTicksNS(), periodNs);
            if (waitNs > 0)
            {
                SDL.DelayPrecise(waitNs);
            }
        }
    }

    private static float GetRefreshRate(nint window)
    {
        var mode = SDL.GetCurrentDisplayMode(SDL.GetDisplayForWindow(window));
        return mode is { } displayMode ? displayMode.RefreshRate : 0;
    }

    private void OpenGamepad(uint instanceId)
    {
        if (!_gamepads.ContainsKey(instanceId))
        {
            var gamepad = SDL.OpenGamepad(instanceId);
            if (gamepad != 0)
            {
                _gamepads.Add(instanceId, gamepad);
            }
        }
    }

    private void CloseGamepad(uint instanceId)
    {
        if (_gamepads.Remove(instanceId, out var gamepad))
        {
            SDL.CloseGamepad(gamepad);
        }
    }
}
