namespace LudumDare60.Engine.Platform;

/// <summary>
/// Product analytics: what players do, sent to wherever the platform collects it (the browser platform sends it to
/// GameAnalytics when the page is configured to). Events are rare moments of play, not per-frame data: each call may
/// cost an interop call. Calls must not throw; a platform that cannot send drops the event.
/// </summary>
/// <remarks>Ids follow the GameAnalytics rules, which the browser platform checks and otherwise drops the event: each
/// part is 1 to 64 of the characters <c>A-Z a-z 0-9 space - _ . ( ) ! ?</c>. See docs/gamedesign/analytics.md for the
/// events the game sends.</remarks>
public interface IAnalytics
{
    /// <summary>
    /// A step in the player's progression: an attempt starts, then completes or fails.
    /// </summary>
    /// <param name="status">Whether the attempt starts, completes or fails.</param>
    /// <param name="progression01">The top level, for example <c>Order</c>.</param>
    /// <param name="progression02">The second level, for example the order's id; null for none.</param>
    /// <param name="progression03">The third level; null for none. Needs <paramref name="progression02"/>.</param>
    /// <param name="score">A score for the attempt, or null for none.</param>
    void Progression(ProgressionStatus status, string progression01, string? progression02, string? progression03, int? score);

    /// <summary>
    /// Something the player did or reached, with an optional number that analytics sums and averages.
    /// </summary>
    /// <param name="eventId">One to five parts joined by <c>:</c>, most general first, for example
    /// <c>Order:hoodie:HalfCock</c>.</param>
    /// <param name="value">A number to aggregate, such as seconds or a count, or null for none.</param>
    void Design(string eventId, double? value);
}
