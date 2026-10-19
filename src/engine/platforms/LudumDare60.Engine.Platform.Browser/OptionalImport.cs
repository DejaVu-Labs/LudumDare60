using System.Runtime.InteropServices.JavaScript;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Imports a JS module the game runs without: error reporting (<c>reporting.js</c>) and product analytics
/// (<c>analytics.js</c>). A content blocker may strip a file named like a tracker, and a connection may drop; either way
/// the game starts, and the module's bindings stay off (their <c>IsImported</c> stays false).
/// </summary>
internal static class OptionalImport
{
    /// <summary>
    /// Runs <paramref name="import"/> and returns whether it worked; a failed import returns false instead of throwing.
    /// </summary>
    /// <param name="import">Starts the import (<c>JSHost.ImportAsync</c> in the browser).</param>
    public static async Task<bool> TryAsync(Func<Task> import)
    {
        ArgumentNullException.ThrowIfNull(import);
        try
        {
            await import();
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}
