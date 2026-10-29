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

    public ApplyResult Apply(ShortcutConfig config, string managedDirectory, IReadOnlyCollection<string>? externalConflictHotkeys = null, string backupReason = "apply")
    {
        using var mutex = new Mutex(false, "Local\\ShortcutManager.Apply");
        if (!mutex.WaitOne(TimeSpan.Zero)) return new(false, "別の Apply/Restore が実行中です。");
        try { return ApplyCore(config, managedDirectory, externalConflictHotkeys ?? [], backupReason); }
        finally { mutex.ReleaseMutex(); }
    }

    private ApplyResult ApplyCore(ShortcutConfig config, string managedDirectory, IReadOnlyCollection<string> externalConflictHotkeys, string backupReason)
    {
        var validation = validator.Validate(config);
        if (!validation.IsValid) return new(false, string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
        if (Directory.Exists(workingRoot))
        {
            var pendingRecovery = Directory.EnumerateDirectories(workingRoot).FirstOrDefault(path => File.Exists(System.IO.Path.Combine(path, "recovery.json")));
            if (pendingRecovery is not null) return new(false, $"前回の復旧が未完了です。退避データを確認してから再実行してください: {pendingRecovery}");
        }
        var conflict = config.Shortcuts.FirstOrDefault(item => item.Enabled && externalConflictHotkeys.Contains(item.Hotkey.Canonical, StringComparer.OrdinalIgnoreCase));
        if (conflict is not null) return new(false, $"外部ショートカットと {conflict.Hotkey.Canonical} が競合しています。");
        IReadOnlyCollection<string> priorNames;
        try { priorNames = configRepository.Load().Shortcuts.Where(item => item.Enabled).Select(ManagedFilename.For).ToArray(); }
        catch { priorNames = []; } // Recovery can still identify generated links by their Shell description.
        EnsureOwnershipSafe(managedDirectory, priorNames, shellLinks);

        var priorConfig = ReadSnapshot(configRepository.Path, BackupArchive.MaxConfigBytes);
        var priorMarkerPath = System.IO.Path.Combine(managedDirectory, OwnershipMarker);
        var priorMarker = ReadSnapshot(priorMarkerPath, 1024);
        var priorState = ReadSnapshot(statePath, BackupArchive.MaxEntryBytes);

        Directory.CreateDirectory(workingRoot);
        var operationRoot = System.IO.Path.Combine(workingRoot, Guid.NewGuid().ToString("N"));
        var stage = System.IO.Path.Combine(operationRoot, "stage");
        var old = System.IO.Path.Combine(operationRoot, "old");
        Directory.CreateDirectory(stage); Directory.CreateDirectory(old);
        var movedOld = new List<(string Source, string Destination)>();
        var committedNew = new List<string>();
        var retainRecovery = false;
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
            backupPath = backups.Create(backupReason, configRepository.Path, managedDirectory, statePath);
            backups.EnforceRetention();
            checkpoint?.Invoke("backed-up");
            configRepository.SaveAtomic(config);
            checkpoint?.Invoke("config-saved");

            Directory.CreateDirectory(managedDirectory);
            foreach (var path in Directory.EnumerateFiles(managedDirectory, "*", SearchOption.TopDirectoryOnly).Where(path => !System.IO.Path.GetFileName(path).Equals(OwnershipMarker, StringComparison.OrdinalIgnoreCase)))
            {
                var destination = System.IO.Path.Combine(old, System.IO.Path.GetFileName(path));
                File.Move(path, destination);
                movedOld.Add((destination, path));
                shellNotifier?.NotifyItemDeleted(path);
            }
            checkpoint?.Invoke("old-moved");
            foreach (var path in Directory.EnumerateFiles(stage, "*.lnk"))
            {
                var destination = System.IO.Path.Combine(managedDirectory, System.IO.Path.GetFileName(path));
                File.Move(path, destination);
                committedNew.Add(destination);
                shellNotifier?.NotifyItemCreated(destination);
            }
            File.WriteAllText(System.IO.Path.Combine(managedDirectory, OwnershipMarker), $"Shortcut Manager{Environment.NewLine}schemaVersion=1{Environment.NewLine}updatedAt={DateTimeOffset.Now:O}{Environment.NewLine}");
            checkpoint?.Invoke("new-moved");
            foreach (var path in committedNew) shellNotifier?.NotifyItemChanged(path);
            shellNotifier?.NotifyDirectoryChanged(managedDirectory);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(statePath)!);
            var configBytes = Encoding.UTF8.GetBytes(ConfigJson.Serialize(config));
            var hash = Convert.ToHexString(SHA256.HashData(configBytes)).ToLowerInvariant();
            File.WriteAllText(statePath, JsonSerializer.Serialize(new { lastSuccessfulApply = DateTimeOffset.Now, configSha256 = hash, managedDirectory }, new JsonSerializerOptions { WriteIndented = true }));
            return new(true, "変更を書き込みました。新規・変更したHotKeyは、Windows Shellの再読み込みまたはWindows再起動後に有効になります。", backupPath, committedNew.Count, hash);
        }
        catch (Exception error)
        {
            var recoveryErrors = new List<string>();
            void Recover(string destination, Action action)
            {
                try { action(); }
                catch (Exception recoveryError) { recoveryErrors.Add($"{destination}: {recoveryError.Message}"); }
            }
            foreach (var path in committedNew)
            {
                Recover(path, () =>
                {
                    checkpoint?.Invoke("rollback-delete");
                    if (File.Exists(path)) File.Delete(path);
                    shellNotifier?.NotifyItemDeleted(path);
                });
            }
            foreach (var pair in movedOld.AsEnumerable().Reverse())
            {
                Recover(pair.Destination, () =>
                {
                    checkpoint?.Invoke("rollback-restore");
                    if (!File.Exists(pair.Source)) throw new IOException($"退避ファイルがありません: {pair.Source}");
                    File.Copy(pair.Source, pair.Destination, true);
                    shellNotifier?.NotifyItemCreated(pair.Destination);
                    shellNotifier?.NotifyItemChanged(pair.Destination);
                });
            }
            Recover(configRepository.Path, () => RestoreConfig(priorConfig));
            Recover(priorMarkerPath, () => RestoreFile(priorMarkerPath, priorMarker));
            Recover(statePath, () => RestoreFile(statePath, priorState));
            Recover(managedDirectory, () => { if (Directory.Exists(managedDirectory)) shellNotifier?.NotifyDirectoryChanged(managedDirectory); });
            if (recoveryErrors.Count > 0)
            {
                retainRecovery = true;
                var message = $"Apply失敗: {error.Message}{Environment.NewLine}復旧未完了。退避先: {operationRoot}{Environment.NewLine}" + string.Join(Environment.NewLine, recoveryErrors);
                // Preserve all available originals even if writing the diagnostic file also fails.
                try
                {
                    if (priorConfig is not null) File.WriteAllBytes(System.IO.Path.Combine(operationRoot, "prior-config.json"), priorConfig);
                    if (priorMarker is not null) File.WriteAllBytes(System.IO.Path.Combine(operationRoot, "prior-marker"), priorMarker);
                    if (priorState is not null) File.WriteAllBytes(System.IO.Path.Combine(operationRoot, "prior-state.json"), priorState);
                    File.WriteAllText(System.IO.Path.Combine(operationRoot, "recovery.json"), JsonSerializer.Serialize(new { error = error.ToString(), recoveryErrors, movedOld, configPath = configRepository.Path, managedDirectory, statePath }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
                }
                catch (Exception evidenceError) { message += $"{Environment.NewLine}復旧記録の保存失敗: {evidenceError.Message}"; }
                return new(false, message, backupPath);
            }
            return new(false, $"Apply に失敗し、以前の状態へロールバックしました: {error.Message}", backupPath);
        }
        finally
        {
            try { if (!retainRecovery && Directory.Exists(operationRoot)) Directory.Delete(operationRoot, true); } catch { }
        }
    }

    public static void EnsureOwnershipSafe(string managedDirectory, IReadOnlyCollection<string>? knownManagedNames = null, IShellLinkService? links = null)
    {
        if (!System.IO.Path.IsPathFullyQualified(managedDirectory)) throw new InvalidOperationException("管理フォルダは絶対パスが必要です。");
        var full = System.IO.Path.GetFullPath(managedDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        foreach (var system in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Where(value => !string.IsNullOrWhiteSpace(value)))
            if (full.Equals(System.IO.Path.GetFullPath(system).TrimEnd(System.IO.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || full.StartsWith(System.IO.Path.GetFullPath(system).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("システム・インストールフォルダ配下は管理対象にできません。");
        var protectedRoots = new[] { System.IO.Path.GetPathRoot(managedDirectory), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
        foreach (var protectedRoot in protectedRoots.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var protectedFull = System.IO.Path.GetFullPath(protectedRoot!).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            if (protectedFull.Equals(full, StringComparison.OrdinalIgnoreCase) || protectedFull.StartsWith(full + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("共有・システム・ユーザールートは管理フォルダにできません。");
        }
        for (var current = new DirectoryInfo(managedDirectory); current is not null; current = current.Parent)
        {
            try
            {
                if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"管理フォルダのreparse pointは許可していません: {current.FullName}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (File.Exists(managedDirectory)) throw new InvalidOperationException("管理フォルダのパスが既存ファイルを指しています。");
        if (!Directory.Exists(managedDirectory)) return;
        var entries = Directory.EnumerateFileSystemEntries(managedDirectory).ToList();
        if (entries.Count > 0 && !File.Exists(System.IO.Path.Combine(managedDirectory, OwnershipMarker))) throw new InvalidOperationException("管理対象フォルダが非空ですが ownership marker がありません。Apply を拒否しました。");
        var marker = System.IO.Path.Combine(managedDirectory, OwnershipMarker);
        foreach (var entry in entries)
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new InvalidOperationException($"管理フォルダ内のリンク・サブフォルダを保護しました: {entry}");
            var name = System.IO.Path.GetFileName(entry);
            if (name.Equals(OwnershipMarker, StringComparison.OrdinalIgnoreCase)) continue;
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Z] - .+\.lnk$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw new InvalidOperationException($"管理フォルダ内に想定外のファイルがあります: {name}");
            if (links is not null && !(knownManagedNames?.Contains(name, StringComparer.OrdinalIgnoreCase) ?? false))
            {
                bool owned;
                try { owned = links.Read(entry).Description.StartsWith("Shortcut Manager: ", StringComparison.Ordinal); }
                catch { owned = false; }
                if (!owned) throw new InvalidOperationException($"設定にない管理外ショートカットを保護しました: {name}");
            }
        }
        if (File.Exists(marker))
        {
            if (new FileInfo(marker).Length > 1024) throw new InvalidOperationException("ownership marker が大きすぎます。");
            var lines = File.ReadAllLines(marker);
            if (lines.Length != 3 || lines[0] != "Shortcut Manager" || lines[1] != "schemaVersion=1" || !lines[2].StartsWith("updatedAt=", StringComparison.Ordinal) || !DateTimeOffset.TryParse(lines[2][10..], out _))
                throw new InvalidOperationException("ownership marker の内容が不正です。自動で上書きしません。");
        }
    }

    private static byte[]? ReadSnapshot(string path, int limit)
    {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > limit) throw new InvalidDataException($"復旧用スナップショットが容量上限を超えています。元ファイルを保全して中止しました: {path}");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private void RestoreConfig(byte[]? bytes)
        => RestoreFile(configRepository.Path, bytes);

    private static void RestoreFile(string path, byte[]? bytes)
    {
        if (bytes is null) { if (File.Exists(path)) File.Delete(path); return; }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
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
