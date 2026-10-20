namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// The launch arguments the desktop platform understands.
/// </summary>
public static class DesktopArguments
{
    /// <summary>
    /// Picks the renderer: <c>--renderer vulkan</c> or <c>--renderer opengl</c> (also <c>--renderer=opengl</c> and
    /// <c>gl</c>), in any case. Other arguments are left alone.
    /// </summary>
    public const string RendererOption = "--renderer";

    /// <summary>
    /// What <see cref="RendererOption"/> accepts, for help and error text.
    /// </summary>
    public const string RendererValues = "vulkan, opengl";

    /// <summary>
    /// Reads the renderer from <paramref name="args"/>; the last <see cref="RendererOption"/> wins.
    /// </summary>
    /// <param name="args">The process's arguments.</param>
    /// <param name="renderer">The renderer; <see cref="DesktopRenderer.Vulkan"/> when none is given.</param>
    /// <param name="error">Why the arguments are wrong, or null.</param>
    /// <returns>Whether the arguments name a known renderer or none.</returns>
    public static bool TryGetRenderer(IReadOnlyList<string> args, out DesktopRenderer renderer, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        renderer = DesktopRenderer.Vulkan;
        error = null;
        for (var i = 0; i < args.Count; i++)
        {
            string? value;
            if (string.Equals(args[i], RendererOption, StringComparison.OrdinalIgnoreCase))
            {
                value = i + 1 < args.Count ? args[++i] : null;
            }
            else if (args[i].StartsWith(RendererOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = args[i][(RendererOption.Length + 1)..];
            }
            else
            {
                continue;
            }

            switch (value?.ToUpperInvariant())
            {
                case "VULKAN":
                    renderer = DesktopRenderer.Vulkan;
                    break;
                case "OPENGL" or "GL":
                    renderer = DesktopRenderer.OpenGL;
                    break;
                default:
                    error = value is null
                        ? $"{RendererOption} needs a value: {RendererValues}."
                        : $"Unknown renderer '{value}'. {RendererOption} takes {RendererValues}.";
                    return false;
            }
        }

        return true;
    }
}
