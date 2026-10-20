namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class DesktopArgumentsTests
{
    [Fact]
    public void TryGetRenderer_NoOption_IsVulkan()
    {
        Assert.True(DesktopArguments.TryGetRenderer(["--debug"], out var renderer, out var error));
        Assert.Equal(DesktopRenderer.Vulkan, renderer);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(new[] { "--renderer", "opengl" }, DesktopRenderer.OpenGL)]
    [InlineData(new[] { "--renderer=OpenGL" }, DesktopRenderer.OpenGL)]
    [InlineData(new[] { "--RENDERER", "gl" }, DesktopRenderer.OpenGL)]
    [InlineData(new[] { "--renderer", "vulkan" }, DesktopRenderer.Vulkan)]
    [InlineData(new[] { "--renderer=opengl", "--debug", "--renderer", "vulkan" }, DesktopRenderer.Vulkan)]
    public void TryGetRenderer_Option_PicksTheRenderer(string[] args, DesktopRenderer expected)
    {
        Assert.True(DesktopArguments.TryGetRenderer(args, out var renderer, out _));
        Assert.Equal(expected, renderer);
    }

    [Fact]
    public void TryGetRenderer_UnknownValue_Fails()
    {
        Assert.False(DesktopArguments.TryGetRenderer(["--renderer", "metal"], out _, out var error));
        Assert.Equal("Unknown renderer 'metal'. --renderer takes vulkan, opengl.", error);
    }

    [Theory]
    [InlineData("--renderer")]
    [InlineData("--renderer=")]
    public void TryGetRenderer_MissingValue_Fails(string arg)
    {
        Assert.False(DesktopArguments.TryGetRenderer([arg], out _, out var error));
        Assert.NotNull(error);
    }
}
