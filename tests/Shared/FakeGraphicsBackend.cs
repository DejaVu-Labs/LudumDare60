using System.Buffers.Binary;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Testing;

/// <summary>
/// One render command read back from a submitted stream: its opcode and its 32-bit arguments.
/// </summary>
internal sealed record RenderCommand(RenderCommandKind Kind, int[] Arguments)
{
    public bool Equals(RenderCommand? other)
        => other is not null && Kind == other.Kind && Arguments.AsSpan().SequenceEqual(other.Arguments);

    public override int GetHashCode() => HashCode.Combine(Kind, Arguments.Length);

    public override string ToString() => $"{Kind}({string.Join(", ", Arguments)})";
}

/// <summary>
/// A frame passed to <see cref="IGraphicsBackend.Submit"/>, copied.
/// </summary>
internal sealed record SubmittedFrame(byte[] Commands, SpriteInstance[] Instances)
{
    /// <summary>
    /// The commands, decoded with the argument counts documented on <see cref="RenderCommandKind"/>.
    /// </summary>
    public List<RenderCommand> Decode()
    {
        var commands = new List<RenderCommand>();
        var offset = 0;
        while (offset < Commands.Length)
        {
            var kind = (RenderCommandKind)BinaryPrimitives.ReadInt32LittleEndian(Commands.AsSpan(offset));
            var count = kind switch
            {
                RenderCommandKind.SetSurface => 8,
                RenderCommandKind.Clear => 1,
                RenderCommandKind.DrawSprites => 3,
                RenderCommandKind.Present => 0,
                _ => throw new InvalidOperationException($"Unknown render command {kind} at byte {offset}."),
            };
            var arguments = new int[count];
            for (var i = 0; i < count; i++)
            {
                arguments[i] = BinaryPrimitives.ReadInt32LittleEndian(Commands.AsSpan(offset + ((i + 1) * sizeof(int))));
            }

            commands.Add(new RenderCommand(kind, arguments));
            offset += (count + 1) * sizeof(int);
        }

        return commands;
    }
}

/// <summary>
/// Records what the engine asks of the GPU. Rejects what a real backend cannot do: creating a texture id twice and
/// destroying one that does not exist.
/// </summary>
internal sealed class FakeGraphicsBackend : IGraphicsBackend
{
    /// <summary>
    /// Every call in order: <c>create {id} {w}x{h}</c>, <c>update {id} rows {y}+{h}</c>, <c>destroy {id}</c>, <c>submit</c>.
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// Pixels of every live texture, by id.
    /// </summary>
    public Dictionary<int, byte[]> Textures { get; } = [];

    public List<SubmittedFrame> Frames { get; } = [];

    /// <summary>
    /// Runs inside <see cref="DestroyTexture"/>, after the call is recorded. Throw from it to simulate a failure.
    /// </summary>
    public Action<int>? OnDestroy { get; set; }

    public void CreateTexture(int id, int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (!Textures.TryAdd(id, pixels.ToArray()))
        {
            throw new InvalidOperationException($"Texture {id} already exists.");
        }

        Calls.Add($"create {id} {width}x{height}");
    }

    public void UpdateTexture(int id, int y, int height, ReadOnlySpan<byte> rows)
    {
        Calls.Add($"update {id} rows {y}+{height}");
        if (!Textures.TryGetValue(id, out var pixels))
        {
            throw new InvalidOperationException($"Texture {id} does not exist.");
        }

        var stride = rows.Length / height;
        if (stride * height != rows.Length || (y + height) * stride > pixels.Length)
        {
            throw new InvalidOperationException($"Rows {y}+{height} do not fit texture {id}.");
        }

        rows.CopyTo(pixels.AsSpan(y * stride));
    }

    public void DestroyTexture(int id)
    {
        Calls.Add($"destroy {id}");
        if (!Textures.Remove(id))
        {
            throw new InvalidOperationException($"Texture {id} does not exist.");
        }

        OnDestroy?.Invoke(id);
    }

    public void Submit(ReadOnlySpan<byte> commands, ReadOnlySpan<SpriteInstance> instances)
    {
        Calls.Add("submit");
        Frames.Add(new SubmittedFrame(commands.ToArray(), instances.ToArray()));
    }
}
