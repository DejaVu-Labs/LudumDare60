namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Reads the query string of an address, as <c>location.search</c> has it.
/// </summary>
internal static class QueryString
{
    /// <summary>
    /// Whether <paramref name="query"/> has a parameter named <paramref name="name"/>, with or without a value:
    /// <c>?debug</c>, <c>?debug=1</c> and <c>?lang=en&amp;debug</c> all have <c>debug</c>. Names are compared ignoring
    /// case and are not percent-decoded.
    /// </summary>
    /// <param name="query">The query string, with or without its leading <c>?</c>; empty or null when there is none.</param>
    /// <param name="name">The parameter's name.</param>
    public static bool Has(string? query, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var rest = (query ?? string.Empty).AsSpan();
        if (rest.StartsWith('?'))
        {
            rest = rest[1..];
        }

        while (!rest.IsEmpty)
        {
            var end = rest.IndexOf('&');
            var parameter = end < 0 ? rest : rest[..end];
            var equals = parameter.IndexOf('=');
            var key = equals < 0 ? parameter : parameter[..equals];
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            rest = end < 0 ? [] : rest[(end + 1)..];
        }

        return false;
    }
}
