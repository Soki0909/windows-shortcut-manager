using ShortcutManager.Core.Models;

namespace ShortcutManager.Windows.ShellLinks;

/// <summary>The persisted launch contract, shared by staging, commit and startup audit.</summary>
public static class ShellLinkComparison
{
    public static bool Matches(ShellLinkInfo actual, LaunchSpec expected) =>
        SamePath(actual.TargetPath, expected.TargetPath)
        && actual.Arguments == expected.Arguments
        && SamePath(actual.WorkingDirectory, expected.WorkingDirectory)
        && NormalizeHotkey(actual.Hotkey) == NormalizeHotkey(expected.Hotkey)
        && string.Equals(NormalizeIcon(actual.IconLocation), NormalizeIcon(expected.IconLocation), StringComparison.OrdinalIgnoreCase)
        && actual.Description == expected.Description
        && actual.WindowStyle == expected.WindowStyle;

    private static bool SamePath(string left, string right) => string.Equals(
        left.Length == 0 ? left : Path.GetFullPath(left), right.Length == 0 ? right : Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeIcon(string value) => value.EndsWith(",0", StringComparison.Ordinal) ? value[..^2] : value;

    private static string NormalizeHotkey(string value)
    {
        var parts = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        return string.Join('+', new[] { "CTRL", "ALT", "SHIFT" }.Where(parts.Contains)
            .Concat(parts.Where(part => part is not "CTRL" and not "ALT" and not "SHIFT").Order(StringComparer.Ordinal)));
    }
}
