using System.IO.Compression;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Backup;

public sealed class RestoreService(ConfigValidator validator, ManagedShortcutApplier applier)
{
    public ApplyResult Restore(string backupPath, string managedDirectory, IReadOnlyCollection<string>? externalConflictHotkeys = null)
    {
        var config = ReadConfig(backupPath);
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, "バックアップの復元を中止しました。元の状態は変更していません: " + string.Join(" / ", validation.Issues.Select(issue => issue.Message + (issue.Path is null ? "" : $" ({issue.Path})"))));
        ManagedShortcutApplier.EnsureOwnershipSafe(managedDirectory);
        // Apply creates the current-state backup after its conflict and ownership checks.
        return applier.Apply(config, managedDirectory, externalConflictHotkeys, backupReason: "restore");
    }

    public string Preview(string backupPath)
    {
        var config = ReadConfig(backupPath);
        var validation = validator.Validate(config);
        var issues = validation.Issues.Select(issue => $"・{issue.Message}" + (issue.Path is null ? "" : $" ({issue.Path})"));
        return $"復元対象: 有効 {config.Shortcuts.Count(item => item.Enabled)} 件 / 全 {config.Shortcuts.Count} 件\n\n" + BackupScope.Description
            + (validation.IsValid ? "\n\n外部ファイルの参照確認で不足はありません。URIアプリの登録状態、Chromeの拡張・グループの存在は復元後に確認してください。" : "\n\n復元できない項目があります。外部ファイルを元の場所へ戻すか、元の設定を修正してバックアップを作り直してください。\n" + string.Join("\n", issues));
    }

    private static ShortcutConfig ReadConfig(string backupPath)
    {
        var verified = BackupArchive.Read(backupPath);
        // The archive already bounded this byte array. Preserve StreamReader's original
        // UTF-8/UTF-16/UTF-32 BOM support without an unbounded ReadToEnd on ZIP input.
        using var input = new MemoryStream(verified.Config, writable: false);
        using var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true);
        var text = new char[verified.Config.Length];
        var length = reader.ReadBlock(text, 0, text.Length);
        return ConfigJson.Deserialize(new string(text, 0, length));
    }
}
