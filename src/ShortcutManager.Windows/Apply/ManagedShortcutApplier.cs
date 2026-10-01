using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Utilities;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Shell;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Apply;

public sealed record ApplyResult(bool Succeeded, string Message, string? BackupPath = null, int GeneratedCount = 0, string? ConfigSha256 = null);

public sealed class ManagedShortcutApplier(
    ConfigRepository configRepository,
    ConfigValidator validator,
    LaunchSpecResolver resolver,
    IShellLinkService shellLinks,
    BackupService backups,
    string workingRoot,
    string statePath,
    Action<string>? checkpoint = null,
    IShellChangeNotifier? shellNotifier = null)
{
    public const string OwnershipMarker = ".shortcut-manager-owned";

    public ApplyResult Apply(ShortcutConfig config, string managedDirectory, IReadOnlyCollection<string>? externalConflictHotkeys = null)
    {
        using var mutex = new Mutex(false, "Local\\ShortcutManager.Apply");
        if (!mutex.WaitOne(TimeSpan.Zero)) return new(false, "別の Apply/Restore が実行中です。");
        try { return ApplyCore(config, managedDirectory, externalConflictHotkeys ?? []); }
        finally { mutex.ReleaseMutex(); }
    }

    private ApplyResult ApplyCore(ShortcutConfig config, string managedDirectory, IReadOnlyCollection<string> externalConflictHotkeys)
    {
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        var conflict = config.Shortcuts.FirstOrDefault(item => item.Enabled && externalConflictHotkeys.Contains(item.Hotkey.Canonical, StringComparer.OrdinalIgnoreCase));
        if (conflict is not null) return new(false, $"外部ショートカットと {conflict.Hotkey.Canonical} が競合しています。");
        EnsureOwnershipSafe(managedDirectory);

        Directory.CreateDirectory(workingRoot);
        var operationRoot = System.IO.Path.Combine(workingRoot, Guid.NewGuid().ToString("N"));
        var stage = System.IO.Path.Combine(operationRoot, "stage");
        var old = System.IO.Path.Combine(operationRoot, "old");
        Directory.CreateDirectory(stage); Directory.CreateDirectory(old);
        var priorConfig = File.Exists(configRepository.Path) ? File.ReadAllBytes(configRepository.Path) : null;
        var movedOld = new List<(string Source, string Destination)>();
        var committedNew = new List<string>();
        string? backupPath = null;

        try
        {
            foreach (var item in config.Shortcuts.Where(item => item.Enabled))
            {
                var launch = resolver.Resolve(config, item);
                var path = System.IO.Path.Combine(stage, ManagedFilename.For(item));
                shellLinks.Write(path, new(launch.TargetPath, launch.Arguments, launch.WorkingDirectory, launch.Hotkey, launch.IconLocation, launch.Description, launch.WindowStyle));
                var read = shellLinks.Read(path);
                if (!Same(read.TargetPath, launch.TargetPath) || read.Arguments != launch.Arguments || NormalizeHotkey(read.Hotkey) != launch.Hotkey) throw new InvalidDataException($"Staged shortcut verification failed: {path}");
            }
            checkpoint?.Invoke("staged");
            backupPath = backups.Create("apply", configRepository.Path, managedDirectory, statePath);
            backups.EnforceRetention();
            checkpoint?.Invoke("backed-up");
            configRepository.SaveAtomic(config);
            checkpoint?.Invoke("config-saved");

            Directory.CreateDirectory(managedDirectory);
            foreach (var path in Directory.EnumerateFiles(managedDirectory, "*", SearchOption.TopDirectoryOnly).Where(path => !System.IO.Path.GetFileName(path).Equals(OwnershipMarker, StringComparison.OrdinalIgnoreCase)))
            {
                var destination = System.IO.Path.Combine(old, System.IO.Path.GetFileName(path));
                File.Move(path, destination); movedOld.Add((destination, path));
            }
            checkpoint?.Invoke("old-moved");
            foreach (var path in Directory.EnumerateFiles(stage, "*.lnk"))
            {
                var destination = System.IO.Path.Combine(managedDirectory, System.IO.Path.GetFileName(path));
                File.Move(path, destination); committedNew.Add(destination);
            }
            File.WriteAllText(System.IO.Path.Combine(managedDirectory, OwnershipMarker), $"Shortcut Manager{Environment.NewLine}schemaVersion=1{Environment.NewLine}updatedAt={DateTimeOffset.Now:O}{Environment.NewLine}");
            checkpoint?.Invoke("new-moved");
            foreach (var path in committedNew) shellNotifier?.NotifyItemChanged(path);
            shellNotifier?.NotifyDirectoryChanged(managedDirectory);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(statePath)!);
            var configBytes = Encoding.UTF8.GetBytes(ConfigJson.Serialize(config));
            var hash = Convert.ToHexString(SHA256.HashData(configBytes)).ToLowerInvariant();
            File.WriteAllText(statePath, JsonSerializer.Serialize(new { lastSuccessfulApply = DateTimeOffset.Now, configSha256 = hash, managedDirectory }, new JsonSerializerOptions { WriteIndented = true }));
            return new(true, "Apply が完了しました。", backupPath, committedNew.Count, hash);
        }
        catch (Exception error)
        {
            foreach (var path in committedNew) if (File.Exists(path)) File.Delete(path);
            foreach (var pair in movedOld.AsEnumerable().Reverse()) if (File.Exists(pair.Source)) File.Move(pair.Source, pair.Destination, true);
            RestoreConfig(priorConfig);
            if (Directory.Exists(managedDirectory)) shellNotifier?.NotifyDirectoryChanged(managedDirectory);
            return new(false, $"Apply に失敗し、以前の状態へロールバックしました: {error.Message}", backupPath);
        }
        finally
        {
            try { if (Directory.Exists(operationRoot)) Directory.Delete(operationRoot, true); } catch { }
        }
    }

    public static void EnsureOwnershipSafe(string managedDirectory)
    {
        if (!Directory.Exists(managedDirectory)) return;
        var entries = Directory.EnumerateFileSystemEntries(managedDirectory).ToList();
        if (entries.Count > 0 && !File.Exists(System.IO.Path.Combine(managedDirectory, OwnershipMarker))) throw new InvalidOperationException("管理対象フォルダが非空ですが ownership marker がありません。Apply を拒否しました。");
    }

    private void RestoreConfig(byte[]? bytes)
    {
        if (bytes is null) { if (File.Exists(configRepository.Path)) File.Delete(configRepository.Path); return; }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(configRepository.Path)!);
        File.WriteAllBytes(configRepository.Path, bytes);
    }

    private static bool Same(string left, string right) => string.Equals(System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    private static string NormalizeHotkey(string value)
    {
        var parts = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        if (parts.Contains("CTRL")) result.Add("CTRL");
        if (parts.Contains("ALT")) result.Add("ALT");
        if (parts.Contains("SHIFT")) result.Add("SHIFT");
        result.AddRange(parts.Where(part => part is not "CTRL" and not "ALT" and not "SHIFT").Order(StringComparer.Ordinal));
        return string.Join('+', result);
    }
}
