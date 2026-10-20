namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// One GameAnalytics event, recorded on the game thread and written as JSON when it is sent (see
/// <see cref="GameAnalyticsProtocol"/>).
/// </summary>
/// <param name="Category"><c>user</c> (session start), <c>session_end</c>, <c>progression</c>, <c>design</c> or
/// <c>error</c>.</param>
/// <param name="Time">When it happened, on the local clock.</param>
/// <param name="CustomDimension01">The pointer type when the event happened, or null before the first press.</param>
internal sealed record AnalyticsEvent(string Category, DateTimeOffset Time, string? CustomDimension01)
{
    /// <summary>
    /// <c>progression</c> and <c>design</c>: the event id.
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// <c>design</c>: the number to aggregate.
    /// </summary>
    public double? Value { get; init; }

    /// <summary>
    /// <c>progression</c> Complete and Fail: how many times the progression was started.
    /// </summary>
    public int? AttemptNumber { get; init; }

    /// <summary>
    /// <c>progression</c>: the score.
    /// </summary>
    public int? Score { get; init; }

    /// <summary>
    /// <c>error</c>: the message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// <c>session_end</c>: the session's length in seconds.
    /// </summary>
    public long? Length { get; init; }
}
