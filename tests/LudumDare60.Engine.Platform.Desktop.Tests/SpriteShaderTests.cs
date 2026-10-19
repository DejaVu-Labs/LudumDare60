using System.Text;
using LudumDare60.Engine.Platform.Desktop.Rendering;
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

    [Fact]
    public void Load_MissingShader_Throws()
        => Assert.Throws<InvalidOperationException>(() => SpriteShaders.Load("missing.spv"));

    private static byte[] Compile(string name, ShaderKind kind)
    {
        var source = File.ReadAllText(Path.Combine(s_shaderFolder, name));
        var shaderc = Shaderc.GetApi();
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
