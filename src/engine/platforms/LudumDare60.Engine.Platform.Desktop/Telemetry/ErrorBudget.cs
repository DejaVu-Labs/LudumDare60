namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// Lets each error through once, and at most a fixed number in all, like <c>reporting.js</c> does per page: an error
/// that repeats every frame must not flood the project. Thread-safe: Sentry calls it from any thread.
/// </summary>
/// <param name="capacity">Errors let through at most.</param>
internal sealed class ErrorBudget(int capacity)
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <returns>Whether an error with <paramref name="key"/> may be sent.</returns>
    public bool TryTake(string key)
    {
        lock (_lock)
        {
            return _seen.Count < capacity && _seen.Add(key);
        }
    }
}
