using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Removal;

public sealed record RemovalArchiveRestoreItem(string RelativePath, string DestinationPath, string Kind,
    long Bytes, string Sha256, bool WillRestore, string Note);

public sealed record RemovalArchiveRestorePreview(string ArchivePath, string ArchiveSha256, string Edition,
    DateTimeOffset CreatedAtUtc, string DataDirectory, IReadOnlyList<RemovalArchiveRestoreItem> Items,
    IReadOnlyList<string> Conflicts, IReadOnlyList<string> Blockers)
{
    public bool CanRestore => Blockers.Count == 0 && Conflicts.Count == 0 && Items.Any(item => item.WillRestore);
}

public sealed record RemovalArchiveRestoreResult(int RestoredCount, int SkippedStateCount, bool RestoredConfig,
    string ArchivePath, IReadOnlyList<string> RestoredPaths);

public sealed class LocalDataRemovalRestore(ApplicationPaths paths, ApplicationPaths otherEdition, Action<string>? checkpoint = null, bool holdsInstanceLease = false)
{
    private const int ExpectedVersion = 1;
    private const string ManifestName = "removal.json";
    private const int MaxManifestBytes = 1024 * 1024;
    private const int MaxEntryCount = 4096;

    private static readonly JsonSerializerOptions StrictJson = new()
    {
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public RemovalArchiveRestorePreview Inspect(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !Path.IsPathFullyQualified(archivePath))
            throw new InvalidDataException("削除前アーカイブを絶対パスで指定してください。");

        var data = ResolveSafeDataDirectory();
        var blockers = new List<string>();
        if (RemovalFileInspection.Attributes(data) is not null)
        {
            if (RemovalJournal.FindActive(data) is not null)
                blockers.Add("未完了の削除準備または退避があります。先に解決してください。");
            var applyJournal = Path.Combine(paths.StagingDirectory, "journal.json");
            if (RemovalFileInspection.Attributes(applyJournal) is not null)
                blockers.Add("未完了の反映または復元トランザクションがあります。先に復旧してください。");
        }

        using var input = RemovalFileInspection.OpenFile(archivePath, LocalDataRemovalArchive.MaxTotalBytes + 32 * 1024 * 1024);
        var archiveSha256 = Convert.ToHexString(SHA256.HashData(input));
        input.Position = 0;

        var manifest = ReadAndVerifyArchive(input);
        var expectedEdition = paths.IsDevelopment ? "debug" : "production";
        if (!string.Equals(manifest.Edition, expectedEdition, StringComparison.Ordinal))
            throw new InvalidDataException($"このアーカイブは別エディション（{manifest.Edition}）のものです。現在のエディション（{expectedEdition}）には復元できません。");

        var items = new List<RemovalArchiveRestoreItem>(manifest.Files.Length);
        var conflicts = new List<string>();
        var stateRelative = Path.GetRelativePath(paths.DataDirectory, paths.StatePath);

        foreach (var file in manifest.Files)
        {
            var windowsRelative = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.Combine(data, windowsRelative);
            var isStateFile = string.Equals(windowsRelative, stateRelative, StringComparison.OrdinalIgnoreCase);
            if (isStateFile)
            {
                items.Add(new RemovalArchiveRestoreItem(windowsRelative, destination, file.Kind, file.Bytes, file.Sha256,
                    WillRestore: false, Note: "反映状態は復元後の「変更を反映」または既存バックアップ復元で管理リンクに合わせて再生成します。"));
                continue;
            }

            if (RemovalFileInspection.Attributes(destination) is not null)
                conflicts.Add(windowsRelative);
            items.Add(new RemovalArchiveRestoreItem(windowsRelative, destination, file.Kind, file.Bytes, file.Sha256,
                WillRestore: true, Note: "現在のエディションの保存先へ新規復元します。"));
        }

        return new RemovalArchiveRestorePreview(archivePath, archiveSha256, manifest.Edition, manifest.CreatedAt,
            data, items, conflicts, blockers);
    }

    public RemovalArchiveRestoreResult Restore(RemovalArchiveRestorePreview approval) => UnderLease(() =>
    {
        var current = Inspect(approval.ArchivePath);
        if (!string.Equals(current.ArchiveSha256, approval.ArchiveSha256, StringComparison.Ordinal)
            || !current.CanRestore)
            throw new InvalidDataException("アーカイブの内容または保存先の状態が変わったため、復元を中止しました。再確認してください。");

        Directory.CreateDirectory(current.DataDirectory);
        var locked = new List<SafeFileHandle>();
        var created = new List<string>();
        var restoredConfig = false;
        var configRelative = Path.GetRelativePath(paths.DataDirectory, paths.ConfigPath);
        try
        {
            LockDirectory(current.DataDirectory, locked);
            using var input = RemovalFileInspection.OpenFile(current.ArchivePath, LocalDataRemovalArchive.MaxTotalBytes + 32 * 1024 * 1024);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(input)), current.ArchiveSha256, StringComparison.Ordinal))
                throw new InvalidDataException("削除前アーカイブが変更されています。");
            input.Position = 0;

            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var toRestore = current.Items.Where(item => item.WillRestore).ToArray();
            var skippedState = current.Items.Count - toRestore.Length;

            foreach (var item in toRestore)
            {
                var parent = Path.GetDirectoryName(item.DestinationPath)!;
                if (RemovalFileInspection.Attributes(parent) is null) Directory.CreateDirectory(parent);
                if (!locked.Any(h => RemovalFileInspection.SamePath(RemovalFileInspection.FinalPath(h, parent), parent)))
                    LockDirectory(parent, locked);

                var entryName = "files/" + item.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
                var entry = zip.GetEntry(entryName) ?? throw new InvalidDataException($"アーカイブ内に項目が見つかりません: {entryName}");
                var bytes = BackupArchive.ReadBounded(entry, BackupArchive.MaxTotalBytes);
                if (bytes.LongLength != item.Bytes || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), item.Sha256, StringComparison.Ordinal))
                    throw new InvalidDataException($"アーカイブ内のファイルが破損しています: {item.RelativePath}");

                using var target = new RemovalArchiveDestination(item.DestinationPath);
                target.Stream.Write(bytes);
                target.Stream.Flush(true);
                target.Publish();
                created.Add(item.DestinationPath);
                if (string.Equals(item.RelativePath, configRelative, StringComparison.OrdinalIgnoreCase))
                    restoredConfig = true;
                checkpoint?.Invoke("file-restored");
            }

            return new RemovalArchiveRestoreResult(created.Count, skippedState, restoredConfig, current.ArchivePath, created);
        }
        catch
        {
            foreach (var path in created.AsEnumerable().Reverse())
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* Best-effort rollback of newly created files only. */ }
            }
            throw;
        }
        finally
        {
            foreach (var handle in locked.AsEnumerable().Reverse()) handle.Dispose();
        }
    });

    private RestoreManifest ReadAndVerifyArchive(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = zip.GetEntry(ManifestName)
            ?? throw new InvalidDataException("削除前アーカイブの一覧（removal.json）が見つかりません。");
        var manifestBytes = BackupArchive.ReadBounded(manifestEntry, MaxManifestBytes);
        var manifest = JsonSerializer.Deserialize<RestoreManifest>(manifestBytes, StrictJson)
            ?? throw new InvalidDataException("削除前アーカイブの一覧を読み取れません。");

        if (manifest.FormatVersion != ExpectedVersion
            || manifest.Files is not { Length: > 0 and <= MaxEntryCount })
            throw new InvalidDataException("削除前アーカイブの形式または件数が対応範囲外です。");

        if (zip.Entries.Count != manifest.Files.Length + 1)
            throw new InvalidDataException("削除前アーカイブに一覧と一致しないファイルが含まれています。");

        long totalBytes = 0;
        var seenWindowsPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedByZipName = new Dictionary<string, RestoreManifestFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in manifest.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.RelativePath)
                || file.Bytes is < 0 or > BackupArchive.MaxTotalBytes
                || !IsHash(file.Sha256))
                throw new InvalidDataException("削除前アーカイブの一覧に無効な項目があります。");
            totalBytes += file.Bytes;
            if (totalBytes > LocalDataRemovalArchive.MaxTotalBytes)
                throw new InvalidDataException("削除前アーカイブの合計サイズが上限を超えています。");

            var segments = file.RelativePath.Split('/');
            foreach (var segment in segments) LocalDataRemovalArchive.EnsureSafeName(segment);
            var windowsRelative = string.Join(Path.DirectorySeparatorChar, segments);
            if (!seenWindowsPaths.Add(windowsRelative) || !IsAllowedRestorePath(windowsRelative))
                throw new InvalidDataException($"復元できないパスまたは重複するパスが含まれています: {file.RelativePath}");
            expectedByZipName.Add("files/" + file.RelativePath, file);
        }

        foreach (var entry in zip.Entries.Where(entry => !string.Equals(entry.FullName, ManifestName, StringComparison.Ordinal)))
        {
            if (!expectedByZipName.Remove(entry.FullName, out var expected))
                throw new InvalidDataException($"アーカイブに想定外の項目があります: {entry.FullName}");
            var content = BackupArchive.ReadBounded(entry, BackupArchive.MaxTotalBytes);
            if (content.LongLength != expected.Bytes || !string.Equals(Convert.ToHexString(SHA256.HashData(content)), expected.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException($"アーカイブ内のファイル検証に失敗しました: {expected.RelativePath}");
        }

        if (expectedByZipName.Count != 0)
            throw new InvalidDataException("アーカイブに必要なファイルが不足しています。");

        return manifest;
    }

    private string ResolveSafeDataDirectory()
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

    private bool IsAllowedRestorePath(string relative)
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

    private static void LockDirectory(string path, List<SafeFileHandle> locked)
    {
        if (!RemovalFileInspection.SamePath(RemovalFileInspection.DirectoryPath(path, locked), path))
            throw new InvalidDataException($"復元先のフォルダが別の場所に解決されました: {path}");
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

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private sealed record RestoreManifest(int FormatVersion, string Edition, DateTimeOffset CreatedAt, RestoreManifestFile[] Files);
    private sealed record RestoreManifestFile(string RelativePath, string Kind, long Bytes, string Sha256);
}
