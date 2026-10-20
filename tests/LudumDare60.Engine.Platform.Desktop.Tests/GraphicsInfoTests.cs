using LudumDare60.Engine.Platform.Desktop.Rendering;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class GraphicsInfoTests
{
    [Theory]
    [InlineData("Vulkan", "Intel(R) HD Graphics 615", "venus", "Mesa 25.0.7", "Renderer: Vulkan, Intel(R) HD Graphics 615 (venus), Mesa 25.0.7")]
    [InlineData("OpenGL", "llvmpipe", null, "4.5", "Renderer: OpenGL, llvmpipe, 4.5")]
    [InlineData("OpenGL", null, "Mesa", null, "Renderer: OpenGL, Mesa")]
    [InlineData("Vulkan", null, null, null, "Renderer: Vulkan")]
    public void Describe_LeavesMissingFieldsOut(string api, string? device, string? vendor, string? version, string expected)
        => Assert.Equal(expected, new GraphicsInfo(api, device, vendor, version).Describe());
}
