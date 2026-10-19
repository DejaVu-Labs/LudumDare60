namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// SDL scancodes to <see cref="Key"/>. Both are USB HID usage IDs, so a scancode is its key when <see cref="Key"/> has
/// a member for it.
/// </summary>
internal static class KeyMap
{
    private static readonly bool[] s_known = CreateTable();

    /// <returns>The key, or <see cref="Key.None"/> for scancodes outside <see cref="Key"/>.</returns>
    public static Key FromScancode(int scancode)
        => (uint)scancode < (uint)s_known.Length && s_known[scancode] ? (Key)scancode : Key.None;

    private static bool[] CreateTable()
    {
        var known = new bool[256];
        foreach (var key in Enum.GetValues<Key>())
        {
            known[(int)key] = key != Key.None;
        }

        return known;
    }
}
