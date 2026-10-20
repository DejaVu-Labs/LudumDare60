using System.Runtime.InteropServices;
using System.Text;
using LudumDare60.Engine.Platform.Desktop.Rendering;
using Silk.NET.Core.Contexts;
using Silk.NET.Shaderc;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

/// <summary>
/// The SPIR-V the desktop renderer ships is committed next to its GLSL source. These tests compile the source with
/// shaderc and compare. Run them with <c>LD60_UPDATE_SHADERS=1</c> to write the compiled shaders instead.
/// </summary>
public sealed unsafe class SpriteShaderTests
{
    private const string UpdateVariable = "LD60_UPDATE_SHADERS";

    private static readonly string s_shaderFolder = Path.Combine(
        FindRepositoryRoot(), "src", "engine", "platforms", "LudumDare60.Engine.Platform.Desktop", "Shaders");

    // One shaderc for the whole run, never unloaded: unloading it leaves its exit handlers pointing at freed code, and
    // the test process crashes on the way out.
    private static readonly Lazy<Shaderc> s_shaderc = new(static () => new Shaderc(new DefaultNativeContext(ShadercLibraryPath())));

    public static TheoryData<string, int> Shaders => new()
    {
        { "sprite.vert", (int)ShaderKind.VertexShader },
        { "sprite.frag", (int)ShaderKind.FragmentShader },
    };

    [Theory]
    [MemberData(nameof(Shaders))]
    public void CommittedSpirV_MatchesGlslSource(string name, int kind)
    {
        var compiled = Compile(name, (ShaderKind)kind);
        var spirVPath = Path.Combine(s_shaderFolder, name + ".spv");
        if (string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllBytes(spirVPath, compiled);
        }

        Assert.True(
            File.Exists(spirVPath) && File.ReadAllBytes(spirVPath).AsSpan().SequenceEqual(compiled),
            $"{name}.spv is not the compiled {name}. Run the tests with {UpdateVariable}=1 to update it.");
    }

    [Theory]
    [MemberData(nameof(Shaders))]
    public void EmbeddedSpirV_IsTheCommittedFile(string name, int kind)
    {
        _ = kind;

        var embedded = SpriteShaders.Load(name + ".spv");

        Assert.Equal(File.ReadAllBytes(Path.Combine(s_shaderFolder, name + ".spv")), embedded);
    }

    [Theory]
    [InlineData(GlShaders.SpriteVertex, (int)ShaderKind.VertexShader)]
    [InlineData(GlShaders.SpriteFragment, (int)ShaderKind.FragmentShader)]
    public void OpenGlShaders_CompileAsGlsl330(string source, int kind)
    {
        var shaderc = s_shaderc.Value;
        var compiler = shaderc.CompilerInitialize();
        var options = shaderc.CompileOptionsInitialize();
        try
        {
            shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Opengl, 450);
            shaderc.CompileOptionsSetAutoMapLocations(options, true);
            shaderc.CompileOptionsSetAutoBindUniforms(options, true);
            var length = (nuint)Encoding.UTF8.GetByteCount(source);
            var result = shaderc.CompileIntoSpv(compiler, source, length, (ShaderKind)kind, "sprite", "main", options);
            try
            {
                Assert.True(
                    shaderc.ResultGetCompilationStatus(result) == CompilationStatus.Success,
                    shaderc.ResultGetErrorMessageS(result));
            }
            finally
            {
                shaderc.ResultRelease(result);
            }
        }
        finally
        {
            shaderc.CompileOptionsRelease(options);
            shaderc.CompilerRelease(compiler);
        }
    }

    [Fact]
    public void Load_MissingShader_Throws()
        => Assert.Throws<InvalidOperationException>(() => SpriteShaders.Load("missing.spv"));

    private static byte[] Compile(string name, ShaderKind kind)
    {
        var source = File.ReadAllText(Path.Combine(s_shaderFolder, name));
        var shaderc = s_shaderc.Value;
        var compiler = shaderc.CompilerInitialize();
        var options = shaderc.CompileOptionsInitialize();
        try
        {
            shaderc.CompileOptionsSetOptimizationLevel(options, OptimizationLevel.Performance);
            var length = (nuint)Encoding.UTF8.GetByteCount(source);
            var result = shaderc.CompileIntoSpv(compiler, source, length, kind, name, "main", options);
            try
            {
                if (shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
                {
                    Assert.Fail($"Compiling {name} failed: {shaderc.ResultGetErrorMessageS(result)}");
                }

                return new ReadOnlySpan<byte>(shaderc.ResultGetBytes(result), (int)shaderc.ResultGetLength(result)).ToArray();
            }
            finally
            {
                shaderc.ResultRelease(result);
            }
        }
        finally
        {
            shaderc.CompileOptionsRelease(options);
            shaderc.CompilerRelease(compiler);
        }
    }

    /// <summary>
    /// The shaderc library Silk.NET.Shaderc.Native puts in the test output. <c>Shaderc.GetApi</c> looks for it under the
    /// runtime identifier it works out itself, which on some distributions (Ubuntu, so the CI runner) is not the
    /// portable <c>linux-x64</c> the package's folder is named after, and then finds nothing; the process's own
    /// identifier is the portable one.
    /// </summary>
    private static string ShadercLibraryPath()
    {
        var name = OperatingSystem.IsWindows() ? "shaderc_shared.dll"
            : OperatingSystem.IsMacOS() ? "libshaderc_shared.dylib"
            : "libshaderc_shared.so";
        return Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", name);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LudumDare60.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("LudumDare60.slnx not found above the test output.");
    }
}
