using ShortcutManager.Core.Models;
using ShortcutManager.Core.Utilities;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Apply;

public sealed record AuditDifference(string File,string Field,string Actual,string Expected);
public sealed record ShortcutAuditResult(IReadOnlyList<string> Differences)
{
    public bool Matches => Differences.Count == 0;
    public IReadOnlyList<AuditDifference> Details {get;init;}=[];
}

public sealed class ManagedShortcutAuditor(ConfigValidator validator, LaunchSpecResolver resolver, IShellLinkService links)
{
    public ShortcutAuditResult Audit(ShortcutConfig config, string directory)
    {
        var validation = validator.Validate(config);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        ManagedShortcutApplier.EnsureOwnershipSafe(directory, config.Shortcuts.Where(item => item.Enabled).Select(ManagedFilename.For).ToArray(), links);
        var differences = new List<string>();
        var details = new List<AuditDifference>();
        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in config.Shortcuts.Where(item => item.Enabled))
        {
            var name = ManagedFilename.For(definition);
            expectedNames.Add(name);
            var expected = resolver.Resolve(config, definition);
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) { differences.Add($"{name}: ファイルがありません。");details.Add(new(path,"ファイル","なし","設定から生成")); continue; }
            try
            {
                var actual = links.Read(path);
                var fields=ShellLinkComparison.Differences(actual,expected);
                if (fields.Count>0)
                    differences.Add($"{name}: 保存済み設定と一致しません。");
                details.AddRange(fields.Select(field=>new AuditDifference(path,field.Field,field.Actual,field.Expected)));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                differences.Add($"{name}: 読み取れません ({error.Message})。");
                details.Add(new(path,"読取り",error.Message,"設定に一致するリンク"));
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
                if (!expectedNames.Contains(name)) {differences.Add($"{name}: 設定にない管理ショートカットです。");details.Add(new(path,"ファイル","設定にない管理リンク","なし"));}
            }
            if (Directory.EnumerateDirectories(directory).Any())
                throw new InvalidOperationException("管理フォルダにサブフォルダがあります。自動修復を中止しました。");
        }
        return new(differences) {Details=details};
    }

}
