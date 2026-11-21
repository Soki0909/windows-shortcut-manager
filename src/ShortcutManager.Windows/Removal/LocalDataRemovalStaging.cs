using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Removal;

public enum RemovalRecoveryOutcome { NothingPending, AwaitingDecision, Restored, Deleted }

public sealed record PendingRemovalSummary(string OperationDirectory, string ArchivePath, string ArchiveSha256,
    IReadOnlyList<string> RelativePaths, bool IsStaged, bool IsCancelling, bool IsCommitting);

public sealed record RemovalCommitResult(int DeletedCount, string ArchivePath, string OperationDirectory,
    IReadOnlyList<string> DeletedRelativePaths);

public sealed class LocalDataRemovalStaging(ApplicationPaths paths, ApplicationPaths otherEdition, Action<string>? checkpoint = null, bool holdsInstanceLease = false)
{
    public int Stage(LocalDataRemovalPlan approval, IReadOnlyCollection<string> individuallySelected,
        VerifiedRemovalArchive archive, bool hasUnsavedChanges) => UnderLease(() =>
    {
        var current = new LocalDataRemovalPreview(paths, otherEdition).Inspect(hasUnsavedChanges);
        LocalDataRemovalArchive.EnsureUnchanged(approval, current);
        var selected = LocalDataRemovalArchive.SelectItems(current, individuallySelected).ToArray();
        if (selected.Length is 0 or > RemovalJournal.MaxCount || !selected.SequenceEqual(archive.Files))
            throw new InvalidDataException("削除前アーカイブの対象と今回の確認一覧が一致しません。");

        using var archived = RemovalFileInspection.OpenFile(archive.Path, LocalDataRemovalArchive.MaxTotalBytes + 32 * 1024 * 1024);
        if (Convert.ToHexString(SHA256.HashData(archived)) != archive.Sha256)
            throw new InvalidDataException("削除前アーカイブが変更されています。");

        var data = GetDataDirectory();
        var journal = new RemovalJournalData(1, paths.IsDevelopment, CurrentUserSid(), data, archive.Path, archive.Sha256,
            selected.Select(item => new RemovalJournalEntry(Path.GetRelativePath(data, item.Path), item.Identity!.Value, item.Bytes!.Value, item.Sha256!)).ToArray());
        var locked = new List<SafeFileHandle>();
        try
        {
            LockDirectory(data, locked);
            if (RemovalJournal.FindActive(data) is not null) throw new InvalidDataException("先に以前の退避を確認してください。");
            var recovery = Path.Combine(data, RemovalJournal.RootName);
            if (RemovalFileInspection.Attributes(recovery) is null) Directory.CreateDirectory(recovery);
            LockDirectory(recovery, locked);
            var operation = Path.Combine(recovery, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(operation);
            var volume = LockDirectory(operation, locked);
            var entries = ResolveEntries(operation, data, journal, volume, locked);
            if (CheckLocations(entries).Count != 0) throw new InvalidDataException("空の退避先を確認できません。");

            RemovalJournal.WriteIntent(operation, journal);
            checkpoint?.Invoke("journal-prepared");
            foreach (var entry in entries)
            {
                MoveChecked(entry.Original, entry.Stashed, entry.File);
                checkpoint?.Invoke("file-staged");
            }
            if (CheckLocations(entries).Count != entries.Length)
                throw new InvalidDataException("すべての退避を確認できません。");
            RemovalJournal.Mark(operation, RemovalJournal.Staged);
            checkpoint?.Invoke("staging-complete");
            return entries.Length;
        }
        finally { ReleaseDirectories(locked); }
    });

    public PendingRemovalSummary? InspectPending()
    {
        if (RemovalFileInspection.Attributes(paths.DataDirectory) is null) return null;
        var data = GetDataDirectory();
        var operation = RemovalJournal.FindActive(data);
        if (operation is null) return null;
        var journal = RemovalJournal.ReadIntent(operation);
        ValidateJournal(journal, data);
        return new PendingRemovalSummary(
            operation,
            journal.ArchivePath,
            journal.ArchiveSha256,
            journal.Files.Select(file => file.RelativePath).ToArray(),
            RemovalJournal.HasMarker(operation, RemovalJournal.Staged),
            RemovalJournal.HasMarker(operation, RemovalJournal.Cancelling),
            RemovalJournal.HasMarker(operation, RemovalJournal.Committing));
    }

    public RemovalRecoveryOutcome RecoverInterrupted() => UnderLease(() => Recover(cancelStaged: false));

    public RemovalRecoveryOutcome Cancel() => UnderLease(() => Recover(cancelStaged: true));

    public RemovalCommitResult Commit() => UnderLease(() => CommitPending(fromRecovery: false));

    private RemovalRecoveryOutcome Recover(bool cancelStaged)
    {
        var data = GetDataDirectory();
        var operation = RemovalJournal.FindActive(data);
        if (operation is null) return RemovalRecoveryOutcome.NothingPending;
        if (RemovalJournal.HasMarker(operation, RemovalJournal.Committing))
        {
            if (cancelStaged) throw new InvalidOperationException("確定消去がすでに開始されているため取り消せません。");
            CommitPending(fromRecovery: true);
            return RemovalRecoveryOutcome.Deleted;
        }
        var locked = new List<SafeFileHandle>();
        try
        {
            var volume = LockDirectory(operation, locked);
            var journal = RemovalJournal.ReadIntent(operation);
            ValidateJournal(journal, data);
            var entries = ResolveEntries(operation, data, journal, volume, locked);
            var stashed = CheckLocations(entries);
            var startedCancelling = RemovalJournal.HasMarker(operation, RemovalJournal.Cancelling);
            if (RemovalJournal.HasMarker(operation, RemovalJournal.Staged) && !startedCancelling && !cancelStaged)
            {
                if (stashed.Count != entries.Length) throw new InvalidDataException("退避内容が完了記録と一致しません。");
                return RemovalRecoveryOutcome.AwaitingDecision;
            }

            if (!startedCancelling) RemovalJournal.Mark(operation, RemovalJournal.Cancelling);
            checkpoint?.Invoke("cancel-prepared");
            foreach (var entry in stashed)
            {
                MoveChecked(entry.Stashed, entry.Original, entry.File);
                checkpoint?.Invoke("file-restored");
            }
            if (CheckLocations(entries).Count != 0) throw new InvalidDataException("すべての取り消しを確認できません。");
            RemovalJournal.Mark(operation, RemovalJournal.Cancelled);
            checkpoint?.Invoke("cancel-complete");
            return RemovalRecoveryOutcome.Restored;
        }
        finally { ReleaseDirectories(locked); }
    }

    private RemovalCommitResult CommitPending(bool fromRecovery)
    {
        var data = GetDataDirectory();
        var operation = RemovalJournal.FindActive(data)
            ?? throw new InvalidOperationException("確定消去できる退避済みのデータがありません。");
        var locked = new List<SafeFileHandle>();
        try
        {
            var volume = LockDirectory(operation, locked);
            var journal = RemovalJournal.ReadIntent(operation);
            ValidateJournal(journal, data);
            var startedCancelling = RemovalJournal.HasMarker(operation, RemovalJournal.Cancelling);
            var startedCommitting = RemovalJournal.HasMarker(operation, RemovalJournal.Committing);
            if (startedCancelling) throw new InvalidOperationException("取り消しが開始されているため確定消去できません。");
            if (!RemovalJournal.HasMarker(operation, RemovalJournal.Staged))
                throw new InvalidOperationException("退避が完了していないため確定消去できません。");
            if (fromRecovery && !startedCommitting)
                throw new InvalidOperationException("確定消去の開始記録がありません。");

            VerifyArchiveUnchanged(journal.ArchivePath, journal.ArchiveSha256);
            var entries = ResolveEntries(operation, data, journal, volume, locked);
            var remaining = CheckCommitLocations(entries, allowAlreadyDeleted: startedCommitting);
            if (!startedCommitting) RemovalJournal.Mark(operation, RemovalJournal.Committing);
            checkpoint?.Invoke("commit-prepared");
            foreach (var entry in remaining)
            {
                DeleteChecked(entry.Stashed, entry.File);
                checkpoint?.Invoke("file-deleted");
            }
            if (CheckCommitLocations(entries, allowAlreadyDeleted: true).Count != 0)
                throw new InvalidDataException("すべての退避ファイルの消去を確認できません。");
            RemovalJournal.Mark(operation, RemovalJournal.Committed);
            checkpoint?.Invoke("commit-complete");
            return new RemovalCommitResult(
                entries.Length,
                journal.ArchivePath,
                operation,
                entries.Select(entry => entry.File.RelativePath).ToArray());
        }
        finally { ReleaseDirectories(locked); }
    }

    private string GetDataDirectory()
    {
        var data = RemovalFileInspection.DirectoryPath(paths.DataDirectory);
        var otherData = RemovalFileInspection.DirectoryPath(otherEdition.DataDirectory);
        var otherManaged = RemovalFileInspection.DirectoryPath(otherEdition.ManagedShortcutDirectory);
        var ownManaged = RemovalFileInspection.DirectoryPath(paths.ManagedShortcutDirectory);
        if (new[] { otherData, otherManaged, ownManaged }.Any(protectedDirectory =>
                RemovalFileInspection.IsWithin(data, protectedDirectory) || RemovalFileInspection.IsWithin(protectedDirectory, data)))
            throw new InvalidDataException("現在の保存先が管理フォルダや他エディションと重なっています。");
        return data;
    }

    private void ValidateJournal(RemovalJournalData journal, string data)
    {
        if (journal.Version != 1 || journal.IsDevelopment != paths.IsDevelopment || journal.UserSid != CurrentUserSid()
            || journal.DataDirectory is null || !RemovalFileInspection.SamePath(journal.DataDirectory, data)
            || journal.Files is not { Length: > 0 and <= RemovalJournal.MaxCount }
            || journal.ArchivePath is null || !Path.IsPathFullyQualified(journal.ArchivePath) || !IsHash(journal.ArchiveSha256))
            throw new InvalidDataException("退避記録の形式、エディション、または保存先が一致しません。");
    }

    private ResolvedEntry[] ResolveEntries(string operation, string data, RemovalJournalData journal, uint volume, List<SafeFileHandle> locked)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ResolvedEntry>(journal.Files.Length);
        for (var index = 0; index < journal.Files.Length; index++)
        {
            var file = journal.Files[index];
            if (file is null || file.RelativePath is null || file.Bytes is < 0 or > BackupArchive.MaxTotalBytes
                || !IsHash(file.Sha256) || file.Identity.Volume != volume)
                throw new InvalidDataException("退避記録に無効なファイル情報があります。");
            var components = file.RelativePath.Split(Path.DirectorySeparatorChar);
            foreach (var component in components) LocalDataRemovalArchive.EnsureSafeName(component);
            var original = Path.Combine(data, file.RelativePath);
            if (!names.Add(original) || !IsAllowedOriginal(file.RelativePath))
                throw new InvalidDataException("退避記録が想定外または重複するパスを指しています。");
            entries.Add(new(original, RemovalJournal.Slot(operation, index), file));
        }

        foreach (var parent in entries.Select(entry => Path.GetDirectoryName(entry.Original)!).Distinct(StringComparer.OrdinalIgnoreCase))
            if (LockDirectory(parent, locked) != volume)
                throw new InvalidDataException("元ファイルと退避先のボリュームが一致しません。");
        var knownNames = entries.Select(entry => entry.Stashed)
            .Concat(new[] { RemovalJournal.Intent, RemovalJournal.Staged, RemovalJournal.Cancelling, RemovalJournal.Cancelled, RemovalJournal.Committing, RemovalJournal.Committed }
                .Select(name => Path.Combine(operation, name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Directory.EnumerateFileSystemEntries(operation).Any(path => !knownNames.Contains(path)))
            throw new InvalidDataException("退避フォルダに確認できない項目があります。");
        return entries.ToArray();
    }

    private bool IsAllowedOriginal(string relative)
    {
        string Relative(string path) => Path.GetRelativePath(paths.DataDirectory, path);
        if (new[] { Relative(paths.ConfigPath), Relative(paths.StatePath), AuditControlStore.PolicyFilename, AuditControlStore.AttemptFilename }
            .Contains(relative, StringComparer.OrdinalIgnoreCase)) return true;
        var log = Relative(paths.LogPath);
        if (Enumerable.Range(1, 4).Select(index => log + "." + index).Append(log)
            .Contains(relative, StringComparer.OrdinalIgnoreCase)) return true;
        var parent = Path.GetDirectoryName(relative);
        var leaf = Path.GetFileName(relative);
        if (string.Equals(parent, AuditControlStore.HistoryDirectoryName, StringComparison.OrdinalIgnoreCase))
            return LocalDataRemovalPreview.IsAuditHistoryName(leaf);
        return string.Equals(parent, Relative(paths.BackupDirectory), StringComparison.OrdinalIgnoreCase)
            && (leaf.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || leaf.EndsWith(".zip.automatic.json", StringComparison.OrdinalIgnoreCase));
    }

    private static List<ResolvedEntry> CheckLocations(IEnumerable<ResolvedEntry> entries)
    {
        var moved = new List<ResolvedEntry>();
        foreach (var entry in entries)
        {
            var originalExists = RemovalFileInspection.Attributes(entry.Original) is not null;
            var stashExists = RemovalFileInspection.Attributes(entry.Stashed) is not null;
            if (originalExists == stashExists)
                throw new InvalidDataException($"元ファイルと退避ファイルのどちらか一方を確認できません: {entry.Original}");
            using var source = OpenChecked(stashExists ? entry.Stashed : entry.Original, entry.File, forMove: false);
            if (stashExists) moved.Add(entry);
        }
        return moved;
    }

    private static List<ResolvedEntry> CheckCommitLocations(IEnumerable<ResolvedEntry> entries, bool allowAlreadyDeleted)
    {
        var remaining = new List<ResolvedEntry>();
        foreach (var entry in entries)
        {
            if (RemovalFileInspection.Attributes(entry.Original) is not null)
                throw new InvalidDataException($"元の場所にファイルが存在するため確定消去できません: {entry.Original}");
            var stashExists = RemovalFileInspection.Attributes(entry.Stashed) is not null;
            if (!stashExists)
            {
                if (!allowAlreadyDeleted)
                    throw new InvalidDataException($"退避ファイルが見つからないため確定消去できません: {entry.Stashed}");
                continue;
            }
            using var source = OpenChecked(entry.Stashed, entry.File, forMove: false);
            remaining.Add(entry);
        }
        return remaining;
    }

    private static void DeleteChecked(string path, RemovalJournalEntry file)
    {
        using (var input = OpenChecked(path, file, forMove: true))
            RemovalArchiveDestination.DeleteOnClose(input.SafeFileHandle, path);
        if (RemovalFileInspection.Attributes(path) is not null)
            throw new InvalidDataException($"退避ファイルの消去を確認できません: {path}");
    }

    private static void VerifyArchiveUnchanged(string archivePath, string expectedSha256)
    {
        if (RemovalFileInspection.Attributes(archivePath) is null)
            throw new InvalidDataException($"削除前アーカイブが見つからないため確定消去できません: {archivePath}");
        using var archived = RemovalFileInspection.OpenFile(archivePath, LocalDataRemovalArchive.MaxTotalBytes + 32 * 1024 * 1024);
        if (Convert.ToHexString(SHA256.HashData(archived)) != expectedSha256)
            throw new InvalidDataException("削除前アーカイブが変更されているため確定消去できません。");
    }

    private static void MoveChecked(string source, string destination, RemovalJournalEntry file)
    {
        using var input = OpenChecked(source, file, forMove: true);
        RemovalArchiveDestination.RenameWithoutReplacement(input.SafeFileHandle, destination);
        input.Position = 0;
        if (!RemovalFileInspection.SamePath(RemovalFileInspection.FinalPath(input.SafeFileHandle, destination), destination)
            || Convert.ToHexString(SHA256.HashData(input)) != file.Sha256)
            throw new InvalidDataException($"移動後のファイルが退避記録と一致しません: {destination}");
    }

    private static FileStream OpenChecked(string path, RemovalJournalEntry file, bool forMove)
    {
        var input = RemovalFileInspection.OpenFile(path, BackupArchive.MaxTotalBytes, forMove);
        try
        {
            if (input.Length != file.Bytes || RemovalFileInspection.Identity(input, path) != file.Identity
                || Convert.ToHexString(SHA256.HashData(input)) != file.Sha256)
                throw new InvalidDataException($"保存ファイルが退避記録と一致しません: {path}");
            return input;
        }
        catch { input.Dispose(); throw; }
    }

    private static uint LockDirectory(string path, List<SafeFileHandle> locked)
    {
        if (!RemovalFileInspection.SamePath(RemovalFileInspection.DirectoryPath(path, locked), path))
            throw new InvalidDataException($"退避または復旧用のフォルダが別の場所に解決されました: {path}");
        return RemovalFileInspection.Identity(locked[^1], path).Volume;
    }

    private static void ReleaseDirectories(List<SafeFileHandle> locked)
    {
        foreach (var handle in locked.AsEnumerable().Reverse()) handle.Dispose();
    }

    private T UnderLease<T>(Func<T> action)
    {
        using var instance = holdsInstanceLease ? null : new InstanceLease(paths.ConfigPath);
        if (instance is not null && !instance.Acquired) throw new InvalidOperationException("このエディションの別の操作が実行中です。");
        using var apply = new Mutex(false, ManagedShortcutApplier.MutexName);
        try
        {
            if (!apply.WaitOne(TimeSpan.Zero)) throw new InvalidOperationException("反映・復元・退避が他で実行中です。");
        }
        catch (AbandonedMutexException) { /* Inspect the durable state under the acquired mutex. */ }
        try { return action(); }
        finally { apply.ReleaseMutex(); }
    }

    private static string CurrentUserSid()
    {
        using var user = WindowsIdentity.GetCurrent();
        return user.User?.Value ?? throw new InvalidOperationException("現在のWindowsユーザーを確認できません。");
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private sealed record ResolvedEntry(string Original, string Stashed, RemovalJournalEntry File);
}
