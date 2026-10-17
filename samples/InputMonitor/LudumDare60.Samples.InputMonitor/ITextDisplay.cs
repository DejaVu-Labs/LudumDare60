namespace LudumDare60.Samples.InputMonitor;

/// <summary>
/// Shows a block of text. Stands in for a renderer, which the engine does not have yet.
/// </summary>
public interface ITextDisplay
{
    /// <summary>
    /// Replaces the shown text.
    /// </summary>
    /// <param name="utf8">The text as UTF-8, valid only during this call.</param>
    void Show(ReadOnlySpan<byte> utf8);
}
