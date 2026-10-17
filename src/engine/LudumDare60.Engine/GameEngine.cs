using System.Diagnostics.CodeAnalysis;

namespace LudumDare60.Engine;

public sealed class GameEngine
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Stub; engine state will be added.")]
    public Task RunAsync(IGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Task.CompletedTask;
    }
}
