namespace LudumDare60.Engine.Assets;

/// <summary>
/// Rules for asset paths. They catch, on the desktop, the paths that would fail on itch.io: its file names are
/// case-sensitive, and the game runs in a subdirectory, so absolute paths break.
/// </summary>
public static class AssetPath
{
    /// <summary>
    /// Whether <paramref name="path"/> is a valid asset path: relative to the asset root, segments separated by
    /// <c>/</c>, each segment made of lowercase ASCII letters, digits, <c>-</c>, <c>_</c> and <c>.</c>, and no
    /// segment that is empty, <c>.</c> or <c>..</c>. Example: <c>sounds/cylinder-click.ogg</c>.
    /// </summary>
    public static bool IsValid(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var segmentStart = 0;
        for (var i = 0; i <= path.Length; i++)
        {
            if (i == path.Length || path[i] == '/')
            {
                if (!IsValidSegment(path.AsSpan(segmentStart, i - segmentStart)))
                {
                    return false;
                }

                segmentStart = i + 1;
            }
            else if (!IsValidChar(path[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <exception cref="ArgumentException"><paramref name="path"/> breaks the rules of <see cref="IsValid"/>.</exception>
    public static void Validate(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!IsValid(path))
        {
            throw new ArgumentException(
                $"'{path}' is not a valid asset path. Use a relative path with '/' separators and lowercase ASCII "
                    + "letters, digits, '-', '_' and '.', without empty, '.' or '..' segments.",
                nameof(path));
        }
    }

    private static bool IsValidSegment(ReadOnlySpan<char> segment) => segment is not ([] or ['.'] or ['.', '.']);

    private static bool IsValidChar(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.';
}
