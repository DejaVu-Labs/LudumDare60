namespace LudumDare60.Engine.Platform;

/// <summary>
/// <see cref="IAnalytics"/> that drops every event: for platforms without analytics, and for tests.
/// </summary>
public sealed class NullAnalytics : IAnalytics
{
    private NullAnalytics()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static NullAnalytics Instance { get; } = new();

    /// <inheritdoc />
    public void Progression(ProgressionStatus status, string progression01, string? progression02, string? progression03, int? score)
    {
    }

    /// <inheritdoc />
    public void Design(string eventId, double? value)
    {
    }
}
