namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// The GameAnalytics id rules, as <c>isValidDesignId</c> and <c>isValidProgression</c> in <c>analytics.js</c>: each part
/// is 1 to 64 of the characters <c>A-Z a-z 0-9</c>, white space and <c>- _ . ( ) ! ?</c>.
/// </summary>
internal static class AnalyticsIds
{
    public const int MaxPartLength = 64;

    /// <returns>Whether <paramref name="eventId"/> is one to five valid parts joined by <c>:</c>.</returns>
    public static bool IsValidDesignId(string? eventId)
    {
        if (eventId is null)
        {
            return false;
        }

        var parts = 0;
        foreach (var range in eventId.AsSpan().Split(':'))
        {
            if (++parts > 5 || !IsValidPart(eventId.AsSpan()[range]))
            {
                return false;
            }
        }

        return true;
    }

    /// <returns>Whether the levels make a valid progression: the first is required, the third needs the second.</returns>
    public static bool IsValidProgression(ProgressionStatus status, string? progression01, string? progression02, string? progression03)
    {
        if (status is not (ProgressionStatus.Start or ProgressionStatus.Complete or ProgressionStatus.Fail))
        {
            return false;
        }

        if (progression01 is null || !IsValidPart(progression01))
        {
            return false;
        }

        if (progression02 is null)
        {
            return progression03 is null;
        }

        return IsValidPart(progression02) && (progression03 is null || IsValidPart(progression03));
    }

    public static bool IsValidPart(ReadOnlySpan<char> part)
    {
        if (part.Length is < 1 or > MaxPartLength)
        {
            return false;
        }

        foreach (var c in part)
        {
            if (!char.IsAsciiLetterOrDigit(c) && !char.IsWhiteSpace(c) && c is not ('-' or '_' or '.' or '(' or ')' or '!' or '?'))
            {
                return false;
            }
        }

        return true;
    }
}
