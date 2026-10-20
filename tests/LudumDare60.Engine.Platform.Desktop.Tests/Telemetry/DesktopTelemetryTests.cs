using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class DesktopTelemetryTests
{
    [Theory]
    [InlineData(DesktopRenderer.Vulkan, "vulkan")]
    [InlineData(DesktopRenderer.OpenGL, "opengl")]
    public void GetRendererName_IsLowerCase(DesktopRenderer renderer, string expected)
        => Assert.Equal(expected, DesktopTelemetry.GetRendererName(renderer));
}
