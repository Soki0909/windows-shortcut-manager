using ShortcutManager.Core.Models;
using ShortcutManager.Core.Utilities;

namespace ShortcutManager.Windows.Inventory;

public sealed record ImportKeyChoice(string Key, bool Available, string Label);

public static class ImportKeyChoices
{
    public static IReadOnlyList<ImportKeyChoice> Build(ShortcutConfig config, IReadOnlyList<ShortcutInventoryItem> inventory) => KeyboardLayout.Keys.Select(key =>
    {
        var active=config.Shortcuts.Where(item=>item.Enabled && item.Hotkey.Key==key).Select(item=>item.Name).ToArray();
        var disabled=config.Shortcuts.Count(item=>!item.Enabled && item.Hotkey.Key==key);
        var external=inventory.Where(item=>!item.IsManaged && item.Hotkey.Equals($"CTRL+ALT+{key}",StringComparison.OrdinalIgnoreCase)).ToArray();
        var available=active.Length==0 && external.Length==0;
        var status=active.Length>0 ? "候補で使用中: "+string.Join(" / ",active) : external.Length>0 ? $"外部 {external.Length} 件で使用中: "+string.Join(" / ",external.Select(item=>item.SourcePath)) : "今回の指定範囲では空き";
        if(disabled>0) status+=$"（無効の定義 {disabled} 件は保持）";
        return new ImportKeyChoice(key,available,$"{key} — {status}");
    }).ToArray();

    public static bool CanImport(ShortcutInventoryItem item) => !item.IsManaged && item.ParseError is null && (item.FileType.Equals(".lnk",StringComparison.OrdinalIgnoreCase) || item.FileType.Equals(".url",StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(item.Target,UriKind.Absolute,out var uri) && uri.Scheme is "http" or "https");
}
