using ShortcutManager.Core.Models;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.Windows.Apply;

public sealed record StartupReconcileResult(bool Repaired, string Message, int Differences, string? BackupPath = null);

public sealed class StartupShortcutReconciler(ManagedShortcutAuditor auditor, ManagedShortcutApplier applier)
{
    public async Task<StartupReconcileResult> ReconcileAsync(ShortcutConfig config, string directory, Func<Task<IReadOnlyList<ShortcutInventoryItem>>> scan)
    {
        var audit = await Task.Run(() => auditor.Audit(config, directory));
        var inventory = await scan();
        var external = inventory.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
        var conflicts = config.Shortcuts.Where(item => item.Enabled && external.Contains(item.Hotkey.Canonical, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (conflicts.Length > 0)
            return new(false, "起動時監査: 管理外のHotKey競合があります。自動修復を中止しました: " + string.Join(" / ", conflicts.Select(item => item.Hotkey.Canonical)), audit.Differences.Count);
        if (audit.Matches)
            return new(false, "起動時監査: 保存済み設定とショートカットは一致しています。", 0);
        var result = await Task.Run(() => applier.Apply(config, directory, external));
        return new(result.Succeeded,
            result.Succeeded
                ? $"起動時監査: {audit.Differences.Count} 件の不一致をバックアップ後に修復しました。Explorerの再起動でHotKeyを有効化できます。"
                : $"起動時監査: 自動修復できませんでした。{result.Message}",
            audit.Differences.Count, result.BackupPath);
    }
}
