using LudumDare60.Engine.Platform.Desktop.Rendering;
using LudumDare60.Engine.Platform.Desktop.Sounds;
using LudumDare60.Engine.Platform.Desktop.Telemetry;
using SDL3;

namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// <see cref="IPlatform"/> for Windows, Linux and macOS on SDL3: one window drawn with Vulkan through SDL_GPU or with
/// OpenGL 3.3 (see <see cref="DesktopRenderer"/>), frames
/// from a loop on the calling thread, sound mixed in managed code and played through SDL audio, keyboard, mouse, touch
/// and gamepad input, real asset files, and error reports and analytics when the build is configured for them.
/// </summary>
/// <param name="title">Window title.</param>
/// <param name="width">Window width in logical pixels when not fullscreen.</param>
/// <param name="height">Window height in logical pixels when not fullscreen.</param>
/// <param name="renderer">The graphics API to draw with.</param>
/// <param name="assetRoot">Folder of the asset files. <see langword="null"/> for <c>assets/</c> next to the
/// executable.</param>
public sealed class DesktopPlatform(
    string title,
    int width,
    int height,
    DesktopRenderer renderer = DesktopRenderer.Vulkan,
    string? assetRoot = null) : IPlatform
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

    private readonly IDesktopGraphics _graphics = renderer switch
    {
        DesktopRenderer.OpenGL => new GlGraphics(),
        _ => new GpuGraphics(),
    };

    private readonly SdlAudio _audio = new();

    private readonly DesktopTelemetry _telemetry = DesktopTelemetry.Start(AppContext.BaseDirectory, renderer);

    /// <inheritdoc />
    /// <remarks>Files under the asset root, read on the thread pool.</remarks>
    public IAssetSource Assets => _assets;

    /// <inheritdoc />
    /// <remarks>Vulkan through SDL_GPU (<see cref="GpuGraphics"/>) or OpenGL 3.3 (<see cref="GlGraphics"/>), as the
    /// constructor's renderer says, with vsync. <see cref="RunAsync"/> sets it up on the window before the first
    /// frame.</remarks>
    public IGraphicsBackend Graphics => _graphics;

    /// <inheritdoc />
    /// <remarks>WAV and Ogg Vorbis, mixed in managed code (see <see cref="Mixer"/>) and played on the default device.
    /// <see cref="RunAsync"/> opens the device before the first frame; without one the game runs silent. Unlike the
    /// browser, nothing has to unlock audio first.</remarks>
    public IAudioBackend Audio => _audio;

    /// <inheritdoc />
    /// <remarks>GameAnalytics through its REST API when <c>analytics.json</c> next to the executable has the game's keys
    /// (see <see cref="GameAnalyticsClient"/>); otherwise every event is dropped. Events wait until
    /// <see cref="RunAsync"/> starts sending; the session ends when it returns.</remarks>
    public IAnalytics Analytics => _telemetry.Analytics;

    /// <inheritdoc />
    /// <remarks>Runs the frame loop on the calling thread, which must be the process's main thread (macOS needs that
    /// for windows), and returns once the player closes the window: the returned task is always complete. Exceptions,
    /// including a failure to create the window or the renderer, are thrown from this call. Presenting waits for
    /// vsync; while the window shows nothing (minimized), the loop waits between frames to match the display's
    /// refresh rate instead.</remarks>
    /// <exception cref="InvalidOperationException">SDL could not start, or could not create the window or the
    /// renderer.</exception>
    public Task RunAsync(IFrameSink frameSink)
    {
        ArgumentNullException.ThrowIfNull(frameSink);

        foreach (var (name, value) in SdlHints.Get(Environment.GetEnvironmentVariable))
        {
            SDL.SetHint(name, value);
        }

        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio | SDL.InitFlags.Gamepad))
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");
        }

        try
        {
            // Fullscreen on the desktop, without a mode change: the game has no window frame to depend on, and Crostini
            // shows no frame for SDL's windows (see SdlHints). F11 or Alt+Enter switches to a window of the given size.
            _graphics.PrepareWindow();
            var flags = SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity | SDL.WindowFlags.Fullscreen | _graphics.WindowFlags;
            var window = SDL.CreateWindow(title, width, height, flags);
            if (window == 0)
            {
                throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");
            }

            try
            {
                _graphics.Initialize(window);
                DesktopTelemetry.OnGraphicsReady(_graphics.Info);
                if (_graphics.Info is { } info)
                {
                    WriteLog(info.Describe());
                }

                if (_audio.Initialize() is { } audioError)
                {
                    // Not an error report: a machine without a sound device is not a bug.
                    WriteError($"No sound: opening the audio device failed: {audioError}");
                }

                _telemetry.BeginRun();
                Run(window, frameSink);
            }
            finally
            {
                _telemetry.EndRun();
                _audio.Shutdown();
                _graphics.Shutdown();
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
    /// <remarks>Writes the error to the standard error stream, and sends it to Sentry when <c>reporting.json</c> next to
    /// the executable has a DSN (see <see cref="SentryReporting"/>). Exceptions that no code catches are sent too.</remarks>
    public void ReportError(Exception exception, bool handled)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteError(handled ? $"Handled error: {exception}" : $"Unhandled error: {exception}");
        SentryReporting.Capture(exception, handled);
    }

    private static void WriteError(string message) => Console.Error.WriteLine(message);

    private static void WriteLog(string message) => Console.WriteLine(message);

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
                    case SDL.EventType.KeyDown:
                        switch (WindowShortcuts.Get(in e.Key))
                        {
                            case WindowShortcuts.Action.Quit:
                                return;
                            case WindowShortcuts.Action.ToggleFullscreen:
                                var fullscreen = SDL.GetWindowFlags(window).HasFlag(SDL.WindowFlags.Fullscreen);
                                SDL.SetWindowFullscreen(window, !fullscreen);
                                break;
                            default:
                                break;
                        }

                        input.Handle(in e);
                        break;
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
            for (var i = 0; i < count; i++)
            {
                if (events[i].Kind == RawInputEventKind.PointerDown)
                {
                    _telemetry.OnPointerDown((PointerType)events[i].Code);
                }
            }

            frameSink.Update(clock.Tick(frameStartNs), events.AsSpan(0, count));
            frameSink.Render();

            if (!_graphics.Presented)
            {
                // Nothing waited for vsync this frame.
                var periodNs = FrameClock.GetFramePeriodNs(GetRefreshRate(window));
                var waitNs = FrameClock.GetWaitNs(frameStartNs, SDL.GetTicksNS(), periodNs);
                if (waitNs > 0)
                {
                    SDL.DelayPrecise(waitNs);
                }
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
