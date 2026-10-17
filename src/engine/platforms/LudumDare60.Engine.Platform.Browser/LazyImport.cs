namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Starts a JS module import on first use and shares it. A failed or cancelled import is started again by the next
/// call, so one network error does not break every later call for the rest of the session.
/// </summary>
/// <param name="import">Starts the import (<c>JSHost.ImportAsync</c> in the browser).</param>
internal sealed class LazyImport(Func<Task> import)
{
    private Task? _import;

    public Task EnsureAsync()
    {
        if (_import is null || _import.IsFaulted || _import.IsCanceled)
        {
            _import = import();
        }

        return _import;
    }
}
