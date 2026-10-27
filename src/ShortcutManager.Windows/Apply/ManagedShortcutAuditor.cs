using ShortcutManager.Core.Models;
using ShortcutManager.Core.Utilities;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Apply;

public sealed record ShortcutAuditResult(IReadOnlyList<string> Differences)
{
    public bool Matches => Differences.Count == 0;
}

public sealed class ManagedShortcutAuditor(ConfigValidator validator, LaunchSpecResolver resolver, IShellLinkService links)
{
    public ShortcutAuditResult Audit(ShortcutConfig config, string directory)
    {
        var validation = validator.Validate(config);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        ManagedShortcutApplier.EnsureOwnershipSafe(directory);
        var differences = new List<string>();
        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in config.Shortcuts.Where(item => item.Enabled))
        {
            var name = ManagedFilename.For(definition);
            expectedNames.Add(name);
            var expected = resolver.Resolve(config, definition);
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) { differences.Add($"{name}: ファイルがありません。"); continue; }
            try
            {
                var actual = links.Read(path);
                if (!SamePath(actual.TargetPath, expected.TargetPath)
                    || actual.Arguments != expected.Arguments
                    || !SamePath(actual.WorkingDirectory, expected.WorkingDirectory)
                    || !actual.Hotkey.Equals(expected.Hotkey, StringComparison.OrdinalIgnoreCase)
                    || !NormalizeIcon(actual.IconLocation).Equals(NormalizeIcon(expected.IconLocation), StringComparison.OrdinalIgnoreCase)
                    || actual.Description != expected.Description
                    || actual.WindowStyle != expected.WindowStyle)
                    differences.Add($"{name}: 保存済み設定と一致しません。");
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                differences.Add($"{name}: 読み取れません ({error.Message})。");
            }
        }
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(path);
                if (name.Equals(ManagedShortcutApplier.OwnershipMarker, StringComparison.OrdinalIgnoreCase)) continue;
                if (!name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"管理フォルダ内に想定外のファイルがあります。自動修復を中止しました: {name}");
                if (!expectedNames.Contains(name)) differences.Add($"{name}: 設定にない管理ショートカットです。");
            }
            if (Directory.EnumerateDirectories(directory).Any())
                throw new InvalidOperationException("管理フォルダにサブフォルダがあります。自動修復を中止しました。");
        }
        return new(differences);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(left.Length == 0 ? left : Path.GetFullPath(left), right.Length == 0 ? right : Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeIcon(string value) => value.EndsWith(",0", StringComparison.Ordinal) ? value[..^2] : value;
}
