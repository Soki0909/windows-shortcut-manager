namespace ShortcutManager.Windows.Inventory;

public static class InventorySearch
{
    public static bool Matches(ShortcutInventoryItem item,string text,bool withKey,bool importableOnly) =>
        (!withKey || !string.IsNullOrWhiteSpace(item.Hotkey)) && (!importableOnly || ImportKeyChoices.CanImport(item)) &&
        (string.IsNullOrWhiteSpace(text) || new[] {item.DisplayName,item.SourcePath,item.Target,item.Hotkey,item.FileType,item.ParseError ?? string.Empty}.Any(value=>value.Contains(text.Trim(),StringComparison.OrdinalIgnoreCase)));
}
