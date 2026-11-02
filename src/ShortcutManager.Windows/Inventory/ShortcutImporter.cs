using ShortcutManager.Core.Models;

namespace ShortcutManager.Windows.Inventory;

public sealed class ShortcutImporter
{
    public ShortcutDefinition CreateProposal(ShortcutInventoryItem item, string key)
    {
        if (item.IsManaged || item.ParseError is not null) throw new InvalidOperationException("管理対象または読取りに失敗した項目は移行できません。");
        ShortcutTarget target;
        // WScript's target/arguments are insufficient to prove equivalence: .lnk
        // may carry elevation, advertised-app, AppUserModelID or other shell data.
        // Preserve the source link and let Windows interpret its entire contract.
        if (item.FileType.Equals(".lnk",StringComparison.OrdinalIgnoreCase)) target = new ApplicationTarget { LaunchMode = ApplicationLaunchMode.Shortcut, Value = item.SourcePath };
        else if (item.FileType.Equals(".url",StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(item.Target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") target = new WebsiteTarget { Url = item.Target };
        else throw new InvalidOperationException("対応しない形式です。.lnkまたはhttp/httpsの.urlを選んでください。");
        return new() { Name = item.DisplayName, Enabled = true, Hotkey = new() { Key = key }, Target = target };
    }
}

