using System.Buffers.Binary;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// Reads a frame's render command stream (see <see cref="RenderCommandKind"/>) and checks it, the way
/// <c>webgl2.js</c> does: every command complete, <see cref="RenderCommandKind.SetSurface"/> before anything that
/// draws, and every <see cref="RenderCommandKind.DrawSprites"/> inside the frame's instances.
/// </summary>
internal static class RenderCommandReader
{
    /// <summary>
    /// Runs <paramref name="commands"/> on <paramref name="target"/>.
    /// </summary>
    /// <param name="commands">The frame's commands.</param>
    /// <param name="instanceCount">Number of sprite instances the frame submitted.</param>
    /// <param name="target">Runs each command.</param>
    /// <exception cref="InvalidOperationException">The stream is malformed. Commands before the bad one have run.</exception>
    public static void Run(ReadOnlySpan<byte> commands, int instanceCount, IRenderCommandTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var hasSurface = false;
        var offset = 0;
        while (offset < commands.Length)
        {
            var opcode = (RenderCommandKind)ReadInt32(commands, offset, offset);
            switch (opcode)
            {
                case RenderCommandKind.SetSurface:
                    target.SetSurface(
                        ReadInt32(commands, offset + 4, offset),
                        ReadInt32(commands, offset + 8, offset),
                        ReadInt32(commands, offset + 12, offset),
                        ReadInt32(commands, offset + 16, offset),
                        new PixelRect(
                            ReadInt32(commands, offset + 20, offset),
                            ReadInt32(commands, offset + 24, offset),
                            ReadInt32(commands, offset + 28, offset),
                            ReadInt32(commands, offset + 32, offset)));
                    hasSurface = true;
                    offset += 36;
                    break;
                case RenderCommandKind.Clear:
                    RequireSurface(hasSurface);
                    var rgba = commands.Slice(offset + 4, Math.Min(4, Math.Max(0, commands.Length - offset - 4)));
                    if (rgba.Length < 4)
                    {
                        throw Truncated(offset);
                    }

                    target.Clear(new Color(rgba[0], rgba[1], rgba[2], rgba[3]));
                    offset += 8;
                    break;
                case RenderCommandKind.DrawSprites:
                    RequireSurface(hasSurface);
                    var textureId = ReadInt32(commands, offset + 4, offset);
                    var first = ReadInt32(commands, offset + 8, offset);
                    var count = ReadInt32(commands, offset + 12, offset);
                    if (first < 0 || count < 1 || (long)first + count > instanceCount)
                    {
                        throw new InvalidOperationException(
                            $"DrawSprites reads instances {first}..{(long)first + count} of {instanceCount}.");
                    }

                    target.DrawSprites(textureId, first, count);
                    offset += 16;
                    break;
                case RenderCommandKind.Present:
                    RequireSurface(hasSurface);
                    target.Present();
                    offset += 4;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown render command {(int)opcode} at byte {offset}.");
            }
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> commands, int position, int commandOffset)
        => position + 4 <= commands.Length
            ? BinaryPrimitives.ReadInt32LittleEndian(commands[position..])
            : throw Truncated(commandOffset);

    private static void RequireSurface(bool hasSurface)
    {
        if (!hasSurface)
        {
            throw new InvalidOperationException("SetSurface must be the first render command.");
        }
    }

    private static InvalidOperationException Truncated(int offset)
        => new($"The render command at byte {offset} is cut off.");
}
