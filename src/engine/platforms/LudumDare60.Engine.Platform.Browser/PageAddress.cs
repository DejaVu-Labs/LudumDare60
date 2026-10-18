using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// The address of the page the game runs in.
/// </summary>
[SupportedOSPlatform("browser")]
public static class PageAddress
{
    /// <summary>
    /// Whether the page's address has the query parameter <paramref name="name"/>, with or without a value: for
    /// <c>debug</c>, <c>index.html?debug</c> or <c>index.html?debug=1</c>. Names are compared ignoring case.
    /// </summary>
    public static bool HasQueryParameter(string name)
    {
        using var location = JSHost.GlobalThis.GetPropertyAsJSObject("location");
        return QueryString.Has(location?.GetPropertyAsString("search"), name);
    }
}
