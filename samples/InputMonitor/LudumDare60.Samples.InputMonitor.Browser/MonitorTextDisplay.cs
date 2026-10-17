using System.Runtime.InteropServices;

namespace LudumDare60.Samples.InputMonitor.Browser;

/// <summary>
/// Shows text in the page's <c>&lt;pre id="monitor"&gt;</c>, one interop call per change.
/// </summary>
internal sealed class MonitorTextDisplay : ITextDisplay
{
    // [JSImport] marshals Span<byte> but not ReadOnlySpan<byte>; monitor.js only reads the bytes.
    public void Show(ReadOnlySpan<byte> utf8)
    {
        MonitorInterop.Show(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(utf8), utf8.Length));
    }
}
