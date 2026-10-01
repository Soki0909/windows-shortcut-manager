namespace ShortcutManager.Core.Utilities;

public static class KeyboardLayout
{
    public static IReadOnlyList<string> Rows { get; } = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];

    public static IReadOnlyList<string> Keys { get; } =
        Rows.SelectMany(row => row.Select(key => key.ToString())).ToArray();
}
