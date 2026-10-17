using System.Buffers.Binary;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Testing;

/// <summary>
/// One audio command read back from a submitted stream: its opcode and its arguments, ints as they are and floats as
/// their value.
/// </summary>
internal sealed record AudioCommand(AudioCommandKind Kind, double[] Arguments)
{
    public bool Equals(AudioCommand? other)
        => other is not null && Kind == other.Kind && Arguments.AsSpan().SequenceEqual(other.Arguments);

    public override int GetHashCode() => HashCode.Combine(Kind, Arguments.Length);

    public override string ToString() => $"{Kind}({string.Join(", ", Arguments)})";
}

/// <summary>
/// Records what the engine asks of the audio device. Rejects what a real backend cannot do: creating a sound id twice
/// and destroying one that does not exist. Decode results are reported only when a test queues them.
/// </summary>
internal sealed class FakeAudioBackend : IAudioBackend
{
    private readonly Queue<AudioEvent> _pending = new();

    /// <summary>
    /// Every call in order: <c>create {id} {bytes}B</c>, <c>destroy {id}</c>, <c>submit</c>.
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// File bytes of every live sound, by id.
    /// </summary>
    public Dictionary<int, byte[]> Sounds { get; } = [];

    /// <summary>
    /// The commands of every submitted frame, copied.
    /// </summary>
    public List<byte[]> Frames { get; } = [];

    /// <summary>
    /// Runs inside <see cref="DestroySound"/>, after the call is recorded. Throw from it to simulate a failure.
    /// </summary>
    public Action<int>? OnDestroy { get; set; }

    /// <summary>
    /// Queues events for the next <see cref="Submit"/> calls.
    /// </summary>
    public void Report(params AudioEvent[] events)
    {
        foreach (var e in events)
        {
            _pending.Enqueue(e);
        }
    }

    /// <summary>
    /// Queues <see cref="AudioEventKind.SoundReady"/> for every id.
    /// </summary>
    public void ReportReady(params int[] soundIds)
    {
        foreach (var id in soundIds)
        {
            _pending.Enqueue(new AudioEvent(AudioEventKind.SoundReady, id));
        }
    }

    public void CreateSound(int id, ReadOnlySpan<byte> data)
    {
        if (!Sounds.TryAdd(id, data.ToArray()))
        {
            throw new InvalidOperationException($"Sound {id} already exists.");
        }

        Calls.Add($"create {id} {data.Length}B");
    }

    public void DestroySound(int id)
    {
        Calls.Add($"destroy {id}");
        if (!Sounds.Remove(id))
        {
            throw new InvalidOperationException($"Sound {id} does not exist.");
        }

        OnDestroy?.Invoke(id);
    }

    public int Submit(ReadOnlySpan<byte> commands, Span<AudioEvent> events)
    {
        Calls.Add("submit");
        Frames.Add(commands.ToArray());
        var count = 0;
        while (count < events.Length && _pending.TryDequeue(out var e))
        {
            events[count++] = e;
        }

        return count;
    }

    /// <summary>
    /// The commands of the last submitted frame, decoded with the arguments documented on <see cref="AudioCommandKind"/>.
    /// </summary>
    public List<AudioCommand> LastCommands() => Decode(Frames[^1]);

    /// <summary>
    /// Every command of every submitted frame, decoded, in order.
    /// </summary>
    public List<AudioCommand> AllCommands()
    {
        var all = new List<AudioCommand>();
        foreach (var frame in Frames)
        {
            all.AddRange(Decode(frame));
        }

        return all;
    }

    /// <summary>
    /// Decodes a command stream. Ids and buses come back as integers, volumes, pitches, pans and fades as floats.
    /// </summary>
    public static List<AudioCommand> Decode(byte[] stream)
    {
        var commands = new List<AudioCommand>();
        var offset = 0;
        while (offset < stream.Length)
        {
            var kind = (AudioCommandKind)BinaryPrimitives.ReadInt32LittleEndian(stream.AsSpan(offset));

            // 'i' is an int32 argument, 'f' a float32 one.
            var layout = kind switch
            {
                AudioCommandKind.Play => "iifff",
                AudioCommandKind.PlayLoop => "iiiffff",
                AudioCommandKind.SetVoice => "ifff",
                AudioCommandKind.StopVoice => "if",
                AudioCommandKind.SetBusVolume => "if",
                _ => throw new InvalidOperationException($"Unknown audio command {kind} at byte {offset}."),
            };
            var arguments = new double[layout.Length];
            for (var i = 0; i < layout.Length; i++)
            {
                var argument = stream.AsSpan(offset + ((i + 1) * sizeof(int)));
                arguments[i] = layout[i] == 'i'
                    ? BinaryPrimitives.ReadInt32LittleEndian(argument)
                    : BinaryPrimitives.ReadSingleLittleEndian(argument);
            }

            commands.Add(new AudioCommand(kind, arguments));
            offset += (layout.Length + 1) * sizeof(int);
        }

        return commands;
    }
}
