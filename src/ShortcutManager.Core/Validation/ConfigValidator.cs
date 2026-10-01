using System.Text.RegularExpressions;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Validation;

public sealed partial class ConfigValidator
{
    [GeneratedRegex("^[a-p]{32}$")]
    private static partial Regex ExtensionIdPattern();

    public ValidationResult Validate(ShortcutConfig config, bool checkFileSystem = true)
    {
        var issues = new List<ValidationIssue>();
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
        if (name.Length is < 1 or > 80) issues.Add(new("invalid-name", "Name は1～80文字です。", shortcut.Id));
        if (shortcut.Hotkey.Modifiers.Count != 2 || !shortcut.Hotkey.Modifiers.Any(value => value.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) || !shortcut.Hotkey.Modifiers.Any(value => value.Equals("Alt", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new("invalid-modifiers", "修飾キーは Ctrl+Alt 固定です。", shortcut.Id));
        if (shortcut.Hotkey.Key.Length != 1 || shortcut.Hotkey.Key[0] is < 'A' or > 'Z')
            issues.Add(new("invalid-key", "Key は A-Z の大文字1文字です。", shortcut.Id));

        if (!shortcut.Enabled) return;

        switch (shortcut.Target)
        {
            case WebsiteTarget website when !Uri.TryCreate(website.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps):
                issues.Add(new("invalid-website", "Website は http/https の絶対URLが必要です。", shortcut.Id));
                break;
            case ApplicationTarget app:
                if (string.IsNullOrWhiteSpace(app.Value)) issues.Add(new("missing-application-target", "Application target が空です。", shortcut.Id));
                else if (app.LaunchMode == ApplicationLaunchMode.Uri && (!Uri.TryCreate(app.Value, UriKind.Absolute, out var appUri) || string.IsNullOrWhiteSpace(appUri.Scheme))) issues.Add(new("invalid-application-uri", "有効なURI schemeが必要です。", shortcut.Id));
                else if (checkFileSystem && app.LaunchMode != ApplicationLaunchMode.Uri && !File.Exists(app.Value)) issues.Add(new("missing-application-target", "Application target が存在しません。", shortcut.Id, app.Value));
                else if (app.LaunchMode == ApplicationLaunchMode.Shortcut && !app.Value.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) issues.Add(new("invalid-shortcut-target", "shortcut launchMode には .lnk が必要です。", shortcut.Id));
                break;
            case BatTarget bat:
                if (!bat.Path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) && !bat.Path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) issues.Add(new("invalid-bat-extension", "BAT target は .bat または .cmd です。", shortcut.Id));
                else if (checkFileSystem && !File.Exists(bat.Path)) issues.Add(new("missing-bat", "BAT/CMD ファイルが存在しません。", shortcut.Id, bat.Path));
                break;
            case ChromeTabGroupTarget chrome:
                var groupName = chrome.GroupName.Trim();
                if (groupName.Length is < 1 or > 200) issues.Add(new("invalid-group-name", "groupName は1～200文字です。", shortcut.Id));
                if (!ExtensionIdPattern().IsMatch(config.Settings.Chrome.ExtensionId)) issues.Add(new("invalid-extension-id", "Chrome Extension ID は a-p の32文字です。", shortcut.Id));
                break;
        }
    }
}
