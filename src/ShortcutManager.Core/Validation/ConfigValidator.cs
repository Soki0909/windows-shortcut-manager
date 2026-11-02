using System.Text.RegularExpressions;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Validation;

public sealed partial class ConfigValidator
{
    [GeneratedRegex("^[a-p]{32}$")]
    private static partial Regex ExtensionIdPattern();

    public ValidationResult Validate(ShortcutConfig config, bool checkFileSystem = true)
    {
        var issues = ConfigStructure.Inspect(config).ToList();
        if (issues.Count > 0) return new(issues);
        if (config.SchemaVersion != 1) issues.Add(new("invalid-schema-version", "schemaVersion は 1 である必要があります。"));
        if (config.DispatchProtocolVersion != 1) issues.Add(new("invalid-dispatch-version", "dispatchProtocolVersion は 1 である必要があります。"));
        if (config.Shortcuts.Count > 26) issues.Add(new("too-many-shortcuts", "ショートカットは最大26件です。"));

        foreach (var duplicate in config.Shortcuts.GroupBy(item => item.Id).Where(group => group.Count() > 1))
            issues.Add(new("duplicate-id", $"ID {duplicate.Key} が重複しています。", duplicate.Key));

        foreach (var shortcut in config.Shortcuts)
            ValidateShortcut(config, shortcut, issues, checkFileSystem);

        foreach (var duplicate in config.Shortcuts.Where(item => item.Enabled).GroupBy(item => item.Hotkey.Canonical).Where(group => group.Count() > 1))
            foreach (var shortcut in duplicate)
                issues.Add(new("duplicate-hotkey", $"{duplicate.Key} が重複しています。", shortcut.Id));

        return new(issues);
    }

    private static void ValidateShortcut(ShortcutConfig config, ShortcutDefinition shortcut, List<ValidationIssue> issues, bool checkFileSystem)
    {
        var name = shortcut.Name.Trim();
        if (name.Length is < 1 or > 80) issues.Add(new("invalid-name", "名前は1～80文字です。", shortcut.Id));
        if (shortcut.Hotkey.Modifiers.Count != 2 || !shortcut.Hotkey.Modifiers.Any(value => value.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) || !shortcut.Hotkey.Modifiers.Any(value => value.Equals("Alt", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new("invalid-modifiers", "修飾キーは Ctrl+Alt 固定です。", shortcut.Id));
        if (shortcut.Hotkey.Key.Length != 1 || shortcut.Hotkey.Key[0] is < 'A' or > 'Z')
            issues.Add(new("invalid-key", "Key は A-Z の大文字1文字です。", shortcut.Id));

        if (!shortcut.Enabled) return;

        switch (shortcut.Target)
        {
            case WebsiteTarget website when !Uri.TryCreate(website.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps):
                issues.Add(new("invalid-website", "Webサイトには http/https から始まるURLが必要です。", shortcut.Id));
                break;
            case ApplicationTarget app:
                if (app.LaunchMode == ApplicationLaunchMode.Uri)
                {
                    if (!Uri.TryCreate(app.Value, UriKind.Absolute, out var appUri) || string.IsNullOrWhiteSpace(appUri.Scheme)) issues.Add(new("invalid-application-uri", "アプリURIには scheme: から始まる値が必要です。例: mailto: または shell:AppsFolder\\…", shortcut.Id));
                }
                else
                {
                    ValidateFilePath(app.Value, shortcut.Id, "application-target", issues, checkFileSystem);
                    if (app.LaunchMode == ApplicationLaunchMode.Shortcut && !app.Value.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) issues.Add(new("invalid-shortcut-target", "Windowsショートカット方式には .lnk ファイルが必要です。", shortcut.Id));
                }
                ValidateWorkingDirectory(app.WorkingDirectory, shortcut.Id, issues, checkFileSystem);
                break;
            case BatTarget bat:
                if (!bat.Path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) && !bat.Path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) issues.Add(new("invalid-bat-extension", "BAT/CMDには .bat または .cmd ファイルが必要です。", shortcut.Id));
                ValidateFilePath(bat.Path, shortcut.Id, "bat", issues, checkFileSystem);
                ValidateWorkingDirectory(bat.WorkingDirectory, shortcut.Id, issues, checkFileSystem);
                break;
            case ChromeTabGroupTarget chrome:
                var groupName = chrome.GroupName.Trim();
                if (groupName.Length is < 1 or > 200) issues.Add(new("invalid-group-name", "groupName は1～200文字です。", shortcut.Id));
                if (!ExtensionIdPattern().IsMatch(config.Settings.Chrome.ExtensionId)) issues.Add(new("invalid-extension-id", "Chrome Extension ID は a-p の32文字です。", shortcut.Id));
                break;
        }
    }

    private static void ValidateFilePath(string path, Guid id, string kind, List<ValidationIssue> issues, bool checkFileSystem)
    {
        if (string.IsNullOrWhiteSpace(path)) { issues.Add(new("missing-" + kind, "起動先のファイルを指定してください。", id)); return; }
        if (!Path.IsPathFullyQualified(path)) { issues.Add(new("relative-target-path", "起動先はドライブまたは共有名から始まる絶対パスで指定してください。参照ボタンも利用できます。", id)); return; }
        if (!checkFileSystem) return;
        if (Directory.Exists(path)) issues.Add(new("target-is-directory", "起動先にフォルダが指定されています。ファイルを選んでください。", id, path));
        else if (!File.Exists(path)) issues.Add(new("missing-" + kind, "起動先ファイルが見つからないか、読み取れません。ネットワーク先の場合は接続と権限も確認してください。", id, path));
    }

    private static void ValidateWorkingDirectory(string path, Guid id, List<ValidationIssue> issues, bool checkFileSystem)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Path.IsPathFullyQualified(path)) { issues.Add(new("relative-working-directory", "作業フォルダは絶対パスで指定するか、空欄で既定の場所を使ってください。", id, path)); return; }
        if (checkFileSystem && !Directory.Exists(path)) issues.Add(new("missing-working-directory", File.Exists(path) ? "作業フォルダにファイルが指定されています。フォルダを選んでください。" : "作業フォルダが見つからないか、読み取れません。", id, path));
    }
}
