using System.IO.Compression;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Backup;

public sealed class RestoreService(ConfigRepository repository, ConfigValidator validator, BackupService backups, ManagedShortcutApplier applier, string statePath)
{
    public ApplyResult Restore(string backupPath, string managedDirectory, IReadOnlyCollection<string>? externalConflictHotkeys = null)
    {
        var manifest = BackupService.Inspect(backupPath);
        if (manifest.FormatVersion != 1) return new(false, "未対応の backup format です。");
        using var archive = ZipFile.OpenRead(backupPath);
        var entry = archive.GetEntry("config.json") ?? throw new InvalidDataException("config.json が含まれていません。");
        ShortcutConfig config;
        using (var stream = entry.Open()) using (var reader = new StreamReader(stream)) config = ConfigJson.Deserialize(reader.ReadToEnd());
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, "Backup内のconfigが無効です: " + string.Join(" / ", validation.Issues.Select(issue => issue.Message)));
        backups.Create("restore", repository.Path, managedDirectory, statePath);
        return applier.Apply(config, managedDirectory, externalConflictHotkeys);
    }
}
