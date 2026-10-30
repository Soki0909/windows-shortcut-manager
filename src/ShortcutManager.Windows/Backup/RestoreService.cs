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
        // The archive already bounded this byte array. Preserve StreamReader's original
        // UTF-8/UTF-16/UTF-32 BOM support without an unbounded ReadToEnd on ZIP input.
        using var input = new MemoryStream(verified.Config, writable: false);
        using var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true);
        var text = new char[verified.Config.Length];
        var length = reader.ReadBlock(text, 0, text.Length);
        var config = ConfigJson.Deserialize(new string(text, 0, length));
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, "Backup内のconfigが無効です: " + string.Join(" / ", validation.Issues.Select(issue => issue.Message)));
        ManagedShortcutApplier.EnsureOwnershipSafe(managedDirectory);
        // Apply creates the current-state backup after its conflict and ownership checks.
        return applier.Apply(config, managedDirectory, externalConflictHotkeys, backupReason: "restore");
    }
}
