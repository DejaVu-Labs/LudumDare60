using System.Diagnostics.CodeAnalysis;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IPlatform"/> for the browser (WebAssembly).
/// </summary>
/// <remarks>Stub: it does not produce frames yet, so <see cref="RunAsync"/> completes immediately.</remarks>
public sealed class BrowserPlatform : IPlatform
{
    /// <inheritdoc />
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Stub; platform state will be added.")]
    public Task RunAsync(IFrameSink frameSink)
    {
        ArgumentNullException.ThrowIfNull(frameSink);
        return Task.CompletedTask;
    }
}
