namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// What a renderer found once it was set up, for error reports. Fields the driver does not tell are null.
/// </summary>
/// <param name="Api"><c>Vulkan</c> or <c>OpenGL</c>.</param>
/// <param name="Device">The GPU, for example <c>Intel(R) HD Graphics 615</c>.</param>
/// <param name="Vendor">The GPU or driver vendor.</param>
/// <param name="Version">The driver or API version.</param>
internal sealed record GraphicsInfo(string Api, string? Device, string? Vendor, string? Version)
{
    /// <summary>
    /// One line for the log, for example <c>Renderer: OpenGL, Mesa Intel(R) HD Graphics 615 (Intel), 4.6 Mesa 25.0.7</c>.
    /// Missing fields are left out.
    /// </summary>
    public string Describe()
    {
        var text = $"Renderer: {Api}";
        if (Device is not null)
        {
            text += Vendor is not null ? $", {Device} ({Vendor})" : $", {Device}";
        }
        else if (Vendor is not null)
        {
            text += $", {Vendor}";
        }

        return Version is not null ? $"{text}, {Version}" : text;
    }
}
