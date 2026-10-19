using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// A transfer buffer for uploads to the GPU. It grows to the largest upload and is cycled on every write, so a write
/// never overwrites data an earlier copy in the same or a previous command buffer still has to read.
/// </summary>
internal sealed unsafe class GpuUploadBuffer(nint device)
{
    private nint _buffer;
    private uint _size;

    /// <summary>
    /// Copies <paramref name="data"/> to the start of the buffer.
    /// </summary>
    /// <returns>The transfer buffer to copy from, at offset 0.</returns>
    /// <exception cref="InvalidOperationException">SDL could not create or map the buffer.</exception>
    public nint Write(ReadOnlySpan<byte> data)
    {
        var length = (uint)data.Length;
        if (length > _size)
        {
            if (_buffer != 0)
            {
                SDL.ReleaseGPUTransferBuffer(device, _buffer);
            }

            _size = Math.Max(length, 2 * _size);
            var info = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = _size };
            _buffer = SDL.CreateGPUTransferBuffer(device, in info);
            if (_buffer == 0)
            {
                _size = 0;
                throw new InvalidOperationException($"Creating a transfer buffer failed: {SDL.GetError()}");
            }
        }

        var mapped = SDL.MapGPUTransferBuffer(device, _buffer, cycle: true);
        if (mapped == 0)
        {
            throw new InvalidOperationException($"Mapping a transfer buffer failed: {SDL.GetError()}");
        }

        data.CopyTo(new Span<byte>((void*)mapped, data.Length));
        SDL.UnmapGPUTransferBuffer(device, _buffer);
        return _buffer;
    }

    /// <summary>
    /// Releases the buffer. The next <see cref="Write"/> creates a new one.
    /// </summary>
    public void Release()
    {
        if (_buffer != 0)
        {
            SDL.ReleaseGPUTransferBuffer(device, _buffer);
            _buffer = 0;
            _size = 0;
        }
    }
}
