using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Backup;

public sealed class RestoreService(ConfigValidator validator, ManagedShortcutApplier applier)
{
    public ApplyResult Restore(string backupPath, string managedDirectory, IReadOnlyCollection<string>? externalConflictHotkeys = null, RestoreMode mode = RestoreMode.Entire, string? expectedArchiveSha256 = null, string? expectedConfigJson = null)
    {
        var preview=Prepare(backupPath,null,externalConflictHotkeys,mode,managedDirectory);
        if(expectedArchiveSha256 is not null && preview.ArchiveSha256!=expectedArchiveSha256)return new(false,"確認後にバックアップが変わりました。復元せず、プレビューを開き直してください。");
        var config = preview.Config;
        if(expectedConfigJson is not null && ConfigJson.Serialize(config)!=expectedConfigJson)return new(false,"確認後に起動先や競合の状態が変わりました。復元内容を再確認してください。");
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, "バックアップの復元を中止しました。元の状態は変更していません: " + string.Join(" / ", validation.Issues.Select(issue => issue.Message + (issue.Path is null ? "" : $" ({issue.Path})"))));
        ManagedShortcutApplier.EnsureOwnershipSafe(managedDirectory);
        // Apply creates the current-state backup after its conflict and ownership checks.
        return applier.Apply(config, managedDirectory, externalConflictHotkeys, backupReason: "restore");
    }

    public RestorePreview Prepare(string backupPath,ShortcutConfig? current,IReadOnlyCollection<string>? externalConflictHotkeys=null,RestoreMode mode=RestoreMode.Entire,string? managedDirectory=null)
    {
        using var guard=new FileStream(backupPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(guard.Length>BackupArchive.MaxTotalBytes)throw new InvalidDataException("Backup ZIP exceeds size limit.");
        var hash=Convert.ToHexString(SHA256.HashData(guard));
        var archive=BackupArchive.Read(backupPath);
        var config=ParseConfig(archive.Config);
        var originalManagedDirectory=config.Settings.ManagedShortcutDirectory;
        if(managedDirectory is not null) config=config with {Settings=config.Settings with {ManagedShortcutDirectory=Path.GetFullPath(managedDirectory)}};
        var disabled=new List<Guid>();var disabledReasons=new List<string>();var external=externalConflictHotkeys ?? [];
        if(mode==RestoreMode.DisableUnavailable)
        {
            var issues=validator.Validate(config).Issues;
            var unavailable=issues.Where(issue=>issue.ShortcutId is not null && issue.Code is not "invalid-name" and not "invalid-key" and not "invalid-modifiers" and not "duplicate-id" and not "duplicate-hotkey" and not "invalid-structure").Select(issue=>issue.ShortcutId!.Value).ToHashSet();
            config=config with {Shortcuts=config.Shortcuts.Select(item=> {
                if(!item.Enabled || !unavailable.Contains(item.Id) && !external.Contains(item.Hotkey.Canonical,StringComparer.OrdinalIgnoreCase))return item;
                disabled.Add(item.Id);
                disabledReasons.Add(item.Name+": "+string.Join(" / ",issues.Where(issue=>issue.ShortcutId==item.Id).Select(issue=>issue.Message+(issue.Path is null?"":" / "+issue.Path)))+(external.Contains(item.Hotkey.Canonical,StringComparer.OrdinalIgnoreCase)?" / 外部キー競合: "+item.Hotkey.Canonical:string.Empty));
                return item with {Enabled=false};
            }).ToList()};
        }
        var conflicts=config.Shortcuts.Where(item=>item.Enabled && external.Contains(item.Hotkey.Canonical,StringComparer.OrdinalIgnoreCase)).Select(item=>item.Hotkey.Canonical).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(hash,archive.Manifest,config,RestoreDifference.Compare(current,config),validator.Validate(config).Issues,disabled,conflicts,current is null || JsonSerializer.Serialize(current.Settings,ConfigJson.Options)!=JsonSerializer.Serialize(config.Settings,ConfigJson.Options),originalManagedDirectory,managedDirectory,disabledReasons);
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
        return ParseConfig(verified.Config);
    }

    internal static ShortcutConfig ParseConfig(byte[] bytes)
    {
        // The archive already bounded this byte array. Preserve StreamReader's original
        // UTF-8/UTF-16/UTF-32 BOM support without an unbounded ReadToEnd on ZIP input.
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true);
        var text = new char[bytes.Length];
        var length = reader.ReadBlock(text, 0, text.Length);
        return ConfigJson.Deserialize(new string(text, 0, length));
    }
}
