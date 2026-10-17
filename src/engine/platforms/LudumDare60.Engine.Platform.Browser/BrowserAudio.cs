using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IAudioBackend"/> on Web Audio (<c>wwwroot/webaudio.js</c>). Each method is one interop call. Without Web
/// Audio it stays silent: sounds are created and fail to decode, and commands are dropped.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class BrowserAudio : IAudioBackend
{
    private bool _initialized;

    /// <summary>
    /// Loads the module and creates the audio context.
    /// </summary>
    public async Task InitializeAsync()
    {
        await AudioInterop.ImportAsync();
        AudioInterop.Init();
        _initialized = true;
    }

    public void CreateSound(int id, ReadOnlySpan<byte> data)
    {
        EnsureInitialized();
        AudioInterop.CreateSound(id, AsWritable(data));
    }

    public void DestroySound(int id)
    {
        EnsureInitialized();
        AudioInterop.DestroySound(id);
    }

    public int Submit(ReadOnlySpan<byte> commands, Span<AudioEvent> events)
    {
        EnsureInitialized();
        return AudioInterop.Submit(AsWritable(commands), MemoryMarshal.AsBytes(events));
    }

    /// <summary>
    /// <c>[JSImport]</c> marshals only <see cref="Span{T}"/> as a memory view. The JS side only reads these spans.
    /// </summary>
    private static Span<byte> AsWritable(ReadOnlySpan<byte> span)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Audio is set up when the frame loop starts (BrowserPlatform.RunAsync).");
        }
    }
}
