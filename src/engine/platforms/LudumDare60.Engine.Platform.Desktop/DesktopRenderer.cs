namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// The graphics API <see cref="DesktopPlatform"/> draws with.
/// </summary>
public enum DesktopRenderer
{
    /// <summary>
    /// Vulkan through SDL_GPU. The default.
    /// </summary>
    Vulkan,

    /// <summary>
    /// OpenGL 3.3 core, for machines whose Vulkan driver is missing or broken.
    /// </summary>
    OpenGL,
}
