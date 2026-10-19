using System.Runtime.InteropServices.JavaScript;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IAnalytics"/> on <c>wwwroot/analytics.js</c>: one interop call per event. Optional values travel as a flag
/// and a number. An event that fails in JS is dropped.
/// </summary>
/// <param name="progression">Sends a progression event: status, the three levels, whether there is a score, the
/// score (<see cref="AnalyticsInterop.AddProgression"/> in the browser).</param>
/// <param name="design">Sends a design event: id, whether there is a value, the value
/// (<see cref="AnalyticsInterop.AddDesign"/> in the browser).</param>
internal sealed class BrowserAnalytics(
    Action<int, string, string?, string?, bool, int> progression,
    Action<string, bool, double> design) : IAnalytics
{
    public void Progression(ProgressionStatus status, string progression01, string? progression02, string? progression03, int? score)
    {
        try
        {
            progression((int)status, progression01, progression02, progression03, score.HasValue, score.GetValueOrDefault());
        }
        catch (JSException)
        {
            // Analytics must never take the game down.
        }
    }

    public void Design(string eventId, double? value)
    {
        try
        {
            design(eventId, value.HasValue, value.GetValueOrDefault());
        }
        catch (JSException)
        {
            // Analytics must never take the game down.
        }
    }
}
