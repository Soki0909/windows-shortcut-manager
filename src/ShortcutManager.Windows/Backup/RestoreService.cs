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
        var verified = BackupArchive.Read(backupPath);
        var config = ConfigJson.Deserialize(System.Text.Encoding.UTF8.GetString(verified.Config));
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, "Backup内のconfigが無効です: " + string.Join(" / ", validation.Issues.Select(issue => issue.Message)));
        ManagedShortcutApplier.EnsureOwnershipSafe(managedDirectory);
        // Apply creates the current-state backup after its conflict and ownership checks.
        return applier.Apply(config, managedDirectory, externalConflictHotkeys);
    }
}
