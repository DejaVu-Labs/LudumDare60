using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// The live textures by id: their GPU handles and sizes, and the checks <c>webgl2.js</c> applies to texture calls.
/// </summary>
internal sealed class TextureTable
{
    private Entry[] _entries = new Entry[16];

    /// <summary>
    /// Number of live textures.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Checks a <see cref="IGraphicsBackend.CreateTexture"/> call before the texture is created.
    /// </summary>
    /// <exception cref="InvalidOperationException">The id is taken, or the pixels do not match the size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The id is negative or a side is less than 1.</exception>
    public void CheckCreate(int id, int width, int height, int pixelBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (Contains(id))
        {
            throw new InvalidOperationException($"Texture {id} already exists.");
        }

        if ((long)width * height * 4 != pixelBytes)
        {
            throw new InvalidOperationException(
                $"A {width}x{height} texture needs {(long)width * height * 4} bytes, not {pixelBytes}.");
        }
    }

    /// <summary>
    /// Checks a <see cref="IGraphicsBackend.UpdateTexture"/> call.
    /// </summary>
    /// <returns>The texture.</returns>
    /// <exception cref="InvalidOperationException">The texture does not exist, the rows lie outside it, or the bytes
    /// do not match the rows.</exception>
    public Entry CheckUpdate(int id, int y, int height, int rowBytes)
    {
        var entry = Get(id);
        if (height < 1 || y < 0 || (long)y + height > entry.Height)
        {
            throw new InvalidOperationException($"Rows {y}+{height} lie outside texture {id}.");
        }

        if ((long)entry.Width * height * 4 != rowBytes)
        {
            throw new InvalidOperationException(
                $"{height} rows of texture {id} need {(long)entry.Width * height * 4} bytes, not {rowBytes}.");
        }

        return entry;
    }

    public bool Contains(int id) => (uint)id < (uint)_entries.Length && _entries[id].Handle != 0;

    /// <exception cref="InvalidOperationException">The texture does not exist.</exception>
    public Entry Get(int id)
        => Contains(id) ? _entries[id] : throw new InvalidOperationException($"Texture {id} does not exist.");

    /// <summary>
    /// Adds a texture that <see cref="CheckCreate"/> accepted.
    /// </summary>
    /// <param name="id">The id <see cref="CheckCreate"/> accepted.</param>
    /// <param name="handle">The GPU texture, not zero.</param>
    /// <param name="width">Width in texels.</param>
    /// <param name="height">Height in texels.</param>
    public void Add(int id, nint handle, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handle);
        if (id >= _entries.Length)
        {
            Array.Resize(ref _entries, Math.Max(id + 1, _entries.Length * 2));
        }

        _entries[id] = new Entry(handle, width, height);
        Count++;
    }

    /// <summary>
    /// Removes a texture.
    /// </summary>
    /// <returns>Its handle.</returns>
    /// <exception cref="InvalidOperationException">The texture does not exist.</exception>
    public nint Remove(int id)
    {
        var handle = Get(id).Handle;
        _entries[id] = default;
        Count--;
        return handle;
    }

    /// <summary>
    /// The handles of every live texture. The textures stay in the table.
    /// </summary>
    public List<nint> GetHandles()
    {
        var handles = new List<nint>(Count);
        foreach (var entry in _entries)
        {
            if (entry.Handle != 0)
            {
                handles.Add(entry.Handle);
            }
        }

        return handles;
    }

    /// <param name="Handle">The GPU texture; zero for a free id.</param>
    /// <param name="Width">Width in texels.</param>
    /// <param name="Height">Height in texels.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Entry(nint Handle, int Width, int Height);
}
