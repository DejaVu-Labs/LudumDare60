using System.Buffers.Binary;
using System.Text.Json;
using LudumDare60.Engine.Platform.Desktop.Rendering;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class RenderCommandReaderTests
{
    private readonly RecordingTarget _target = new();

    private static byte[] Commands(params int[] words)
    {
        var bytes = new byte[words.Length * 4];
        for (var i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        }

        return bytes;
    }

    private static readonly int[] s_setSurface = [1, 1400, 800, 640, 360, 60, 40, 1280, 720];

    [Fact]
    public void Run_SharedFixture_RunsEveryCommandInOrder()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "render-frame.json")));
        var root = fixture.RootElement;
        var commands = Convert.FromHexString(root.GetProperty("commandsHex").GetString()!);
        var instanceCount = Convert.FromHexString(root.GetProperty("instancesHex").GetString()!).Length / SpriteInstance.Size;
        var clear = root.GetProperty("clear").EnumerateArray().Select(static c => (byte)c.GetInt32()).ToArray();
        var expected = new List<string>
        {
            "SetSurface 1400x800 640x360 PixelRect { X = 60, Y = 40, Width = 1280, Height = 720 }",
            $"Clear {new Color(clear[0], clear[1], clear[2], clear[3])}",
        };
        foreach (var draw in root.GetProperty("draws").EnumerateArray())
        {
            expected.Add($"DrawSprites {draw.GetProperty("texture").GetInt32()} {draw.GetProperty("first").GetInt32()} {draw.GetProperty("count").GetInt32()}");
        }

        expected.Add("Present");

        RenderCommandReader.Run(commands, instanceCount, _target);

        Assert.Equal(expected, _target.Calls);
    }

    [Theory]
    [InlineData(2, 0x01020304)]
    [InlineData(3, 0)]
    [InlineData(4)]
    public void Run_DrawingBeforeSetSurface_Throws(params int[] command)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RenderCommandReader.Run(Commands([.. command, 0, 0, 1]), 1, _target));

        Assert.Equal("SetSurface must be the first render command.", exception.Message);
        Assert.Empty(_target.Calls);
    }

    [Fact]
    public void Run_UnknownOpcode_ThrowsAfterTheCommandsBeforeIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RenderCommandReader.Run(Commands([.. s_setSurface, 9]), 0, _target));

        Assert.Equal("Unknown render command 9 at byte 36.", exception.Message);
        Assert.Single(_target.Calls);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Run_DrawSpritesOutsideTheInstances_Throws(int first, int count)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => RenderCommandReader.Run(Commands([.. s_setSurface, 3, 0, first, count]), 3, _target));

        Assert.StartsWith("DrawSprites reads instances", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DrawSpritesUpToTheLastInstance_Draws()
    {
        RenderCommandReader.Run(Commands([.. s_setSurface, 3, 7, 1, 2]), 3, _target);

        Assert.Equal("DrawSprites 7 1 2", _target.Calls[^1]);
    }

    [Theory]
    [InlineData(37)]
    [InlineData(40)]
    [InlineData(44)]
    public void Run_CutOffCommand_Throws(int length)
    {
        var commands = Commands([.. s_setSurface, 3, 0, 0, 1]).AsSpan(0, length).ToArray();

        var exception = Assert.Throws<InvalidOperationException>(() => RenderCommandReader.Run(commands, 1, _target));

        Assert.Equal("The render command at byte 36 is cut off.", exception.Message);
    }

    [Fact]
    public void Run_CutOffClear_Throws()
    {
        var commands = Commands([.. s_setSurface, 2, 0]).AsSpan(0, 42).ToArray();

        Assert.Throws<InvalidOperationException>(() => RenderCommandReader.Run(commands, 0, _target));
    }

    [Fact]
    public void Run_Empty_DoesNothing()
    {
        RenderCommandReader.Run([], 0, _target);

        Assert.Empty(_target.Calls);
    }

    private sealed class RecordingTarget : IRenderCommandTarget
    {
        public List<string> Calls { get; } = [];

        public void SetSurface(int surfaceWidth, int surfaceHeight, int virtualWidth, int virtualHeight, PixelRect viewport)
            => Calls.Add($"SetSurface {surfaceWidth}x{surfaceHeight} {virtualWidth}x{virtualHeight} {viewport}");

        public void Clear(Color color) => Calls.Add($"Clear {color}");

        public void DrawSprites(int textureId, int firstInstance, int instanceCount)
            => Calls.Add($"DrawSprites {textureId} {firstInstance} {instanceCount}");

        public void Present() => Calls.Add("Present");
    }
}
