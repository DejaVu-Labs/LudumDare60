using LudumDare60.Engine.Platform;

namespace LudumDare60.Game.Tests.Analytics;

/// <summary>
/// <see cref="IAnalytics"/> that keeps every event, in the order they were sent.
/// </summary>
internal sealed class RecordingAnalytics : IAnalytics
{
    public List<SentEvent> Events { get; } = [];

    /// <summary>
    /// The design events, as id and value.
    /// </summary>
    public IEnumerable<(string Id, double? Value)> Designs
        => Events.Where(e => e.Status is null).Select(e => (e.Id, e.Value));

    /// <summary>
    /// The progression events.
    /// </summary>
    public IEnumerable<SentEvent> Progressions => Events.Where(e => e.Status is not null);

    /// <summary>
    /// The value of the one design event <paramref name="id"/>; fails when it was sent never or more than once.
    /// </summary>
    public double? ValueOf(string id) => Assert.Single(Designs, d => string.Equals(d.Id, id, StringComparison.Ordinal)).Value;

    public void Progression(ProgressionStatus status, string progression01, string? progression02, string? progression03, int? score)
        => Events.Add(new SentEvent(status, string.Join(':', new[] { progression01, progression02, progression03 }.Where(p => p is not null)), score));

    public void Design(string eventId, double? value) => Events.Add(new SentEvent(null, eventId, value));

    /// <param name="Status">The progression status, or null for a design event.</param>
    /// <param name="Id">The design event id, or the progression's levels joined by <c>:</c>.</param>
    /// <param name="Value">The design value or the progression score.</param>
    internal sealed record SentEvent(ProgressionStatus? Status, string Id, double? Value);
}
