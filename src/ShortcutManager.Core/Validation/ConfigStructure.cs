using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Validation;

public static class ConfigStructure
{
    public const int MaxJsonCharacters = 1024 * 1024;

    public static IReadOnlyList<ValidationIssue> Inspect(ShortcutConfig? config)
    {
        var issues = new List<ValidationIssue>();
        void Invalid(string path, string message) => issues.Add(new("invalid-structure", $"{path}: {message}", Path: path));
        void Text(string? value, string path, int limit, bool optional = false)
        {
            if (value is null && !optional) Invalid(path, "null は使用できません。");
            else if (value?.Length > limit) Invalid(path, $"最大{limit}文字です。");
        }
        if (config is null) { Invalid("$", "設定がnullです。"); return issues; }
        if (config.Settings is null) Invalid("$.settings", "設定がnullです。");
        else
        {
            Text(config.Settings.ManagedShortcutDirectory, "$.settings.managedShortcutDirectory", 32767, true);
            if (config.Settings.Chrome is null) Invalid("$.settings.chrome", "設定がnullです。");
            else
            {
                Text(config.Settings.Chrome.ExtensionId, "$.settings.chrome.extensionId", 32);
                Text(config.Settings.Chrome.ExecutablePath, "$.settings.chrome.executablePath", 32767, true);
            }
        }
        if (config.Shortcuts is null) { Invalid("$.shortcuts", "一覧がnullです。"); return issues; }
        if (config.Shortcuts.Count > 26) { Invalid("$.shortcuts", "最大26件です。"); return issues; }
        for (var index = 0; index < config.Shortcuts.Count; index++)
        {
            var item = config.Shortcuts[index];
            var path = $"$.shortcuts[{index}]";
            if (item is null) { Invalid(path, "項目がnullです。"); continue; }
            Text(item.Name, path + ".name", 80);
            if (item.Hotkey is null) Invalid(path + ".hotkey", "キー設定がnullです。");
            else
            {
                Text(item.Hotkey.Key, path + ".hotkey.key", 1);
                if (item.Hotkey.Modifiers is null) Invalid(path + ".hotkey.modifiers", "修飾キーがnullです。");
                else if (item.Hotkey.Modifiers.Count > 2 || item.Hotkey.Modifiers.Any(value => value is null || value.Length > 5)) Invalid(path + ".hotkey.modifiers", "修飾キーが不正です。");
            }
            switch (item.Target)
            {
                case WebsiteTarget website: Text(website.Url, path + ".target.url", 32767); break;
                case ApplicationTarget app:
                    if (!Enum.IsDefined(app.LaunchMode)) Invalid(path + ".target.launchMode", "未定義の起動モードです。");
                    Text(app.Value, path + ".target.value", 32767);
                    Text(app.Arguments, path + ".target.arguments", 32767);
                    Text(app.WorkingDirectory, path + ".target.workingDirectory", 32767);
                    break;
                case BatTarget bat:
                    if (!Enum.IsDefined(bat.WindowMode)) Invalid(path + ".target.windowMode", "未定義の表示モードです。");
                    Text(bat.Path, path + ".target.path", 32767);
                    Text(bat.Arguments, path + ".target.arguments", 32767);
                    Text(bat.WorkingDirectory, path + ".target.workingDirectory", 32767);
                    break;
                case ChromeTabGroupTarget chrome: Text(chrome.GroupName, path + ".target.groupName", 200); break;
                default: Invalid(path + ".target", "起動対象がnullまたは未対応です。"); break;
            }
        }
        if (issues.Count == 0 && System.Text.Encoding.UTF8.GetByteCount(Serialization.ConfigJson.Serialize(config)) > MaxJsonCharacters)
            Invalid("$", "保存時のJSONは1 MiB以下である必要があります。");
        return issues;
    }
}
