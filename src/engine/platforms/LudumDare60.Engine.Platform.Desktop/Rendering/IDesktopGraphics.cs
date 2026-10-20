using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// A desktop renderer: an <see cref="IGraphicsBackend"/> that <see cref="DesktopPlatform"/> sets up on its window.
/// </summary>
internal interface IDesktopGraphics : IGraphicsBackend
{
    /// <summary>
    /// Flags the window needs, such as <see cref="SDL.WindowFlags.OpenGL"/>.
    /// </summary>
    SDL.WindowFlags WindowFlags { get; }

    /// <summary>
    /// Whether the last <see cref="IGraphicsBackend.Submit"/> showed a frame and waited for vsync. False while the
    /// window is minimized: then the platform paces the loop itself.
    /// </summary>
    bool Presented { get; }

    /// <summary>
    /// Runs after <c>SDL_Init</c> and before the window is created (OpenGL sets its context attributes here).
    /// </summary>
    void PrepareWindow();

    /// <summary>
    /// Sets the renderer up on <paramref name="window"/>, with vsync.
    /// </summary>
    /// <exception cref="InvalidOperationException">The graphics API is not available.</exception>
    void Initialize(nint window);

    /// <summary>
    /// Releases everything. Textures still alive keep their ids: the engine destroys them after the run, and
    /// <see cref="IGraphicsBackend.DestroyTexture"/> then only forgets them.
    /// </summary>
    void Shutdown();
}
