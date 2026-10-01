using System.Text;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Utilities;

public static class ManagedFilename
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string For(ShortcutDefinition shortcut)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder();
        foreach (var character in shortcut.Name.Trim()) builder.Append(invalid.Contains(character) ? '_' : character);
        var name = builder.ToString().Trim().TrimEnd('.');
        if (Reserved.Contains(name)) name = $"_{name}";
        if (name.Length > 80) name = name[..80].TrimEnd();
        if (string.IsNullOrWhiteSpace(name)) name = "Shortcut";
        return $"{shortcut.Hotkey.Key.ToUpperInvariant()} - {name}.lnk";
    }
}

