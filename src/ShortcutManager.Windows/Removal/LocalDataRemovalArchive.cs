using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Removal;

public sealed record VerifiedRemovalArchive(string Path, string Sha256, int FileCount);

public sealed class LocalDataRemovalArchive(ApplicationPaths paths, ApplicationPaths otherEdition, Action<string>? checkpoint = null)
{
    private const int FormatVersion = 1;
    private const string ManifestName = "removal.json";
    private const int MaxManifestBytes = 1024 * 1024;
    private const int MaxEntryCount = 4096;
    private const long MaxTotalBytes = 1024L * 1024 * 1024;

    public VerifiedRemovalArchive Create(LocalDataRemovalPlan approval, IReadOnlyCollection<string> individuallySelected,
        string destination, bool hasUnsavedChanges)
    {
        var preview = new LocalDataRemovalPreview(paths, otherEdition);
        var current = preview.Inspect(hasUnsavedChanges);
        EnsureUnchanged(approval, current);

        var selected = individuallySelected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eligible = current.Items.Where(item => item.Disposition == RemovalDisposition.RequiresIndividualSelection)
            .Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Count != individuallySelected.Count || !selected.IsSubsetOf(eligible))
            throw new InvalidDataException("確認一覧から個別に選べるバックアップだけを指定してください。");

        var sources = current.Items.Where(item => item.Disposition == RemovalDisposition.Candidate || selected.Contains(item.Path))
            .Select(item => new SourceEntry(item, ManifestEntry(current.DataDirectory, item))).ToArray();
        if (sources.Length is 0 or > MaxEntryCount)
            throw new InvalidDataException("削除前アーカイブに保存できる件数を確認してください。");
        var total = sources.Sum(source => source.Entry.Bytes);
        if (total > MaxTotalBytes)
            throw new InvalidDataException("削除前アーカイブの合計サイズ上限を超えています。");
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new Manifest(FormatVersion,
            current.IsDevelopment ? "debug" : "production", DateTimeOffset.UtcNow, sources.Select(source => source.Entry).ToArray()));
        if (manifest.Length > MaxManifestBytes)
            throw new InvalidDataException("削除前アーカイブの一覧が上限を超えています。");

        if (!Path.IsPathFullyQualified(destination)) throw new InvalidDataException("保存先を絶対パスで指定してください。");
        var fileName = Path.GetFileName(destination);
        EnsureSafeName(fileName);
        var lockedDirectories = new List<SafeFileHandle>();
        try
        {
            var parent = RemovalFileInspection.DirectoryPath(Path.GetDirectoryName(destination)!, lockedDirectories);
            var output = Path.Combine(parent, fileName);
            var protectedRoots = current.ManagedDirectories.Append(current.DataDirectory)
                .Append(RemovalFileInspection.DirectoryPath(otherEdition.DataDirectory))
                .Append(RemovalFileInspection.DirectoryPath(otherEdition.ManagedShortcutDirectory));
            if (protectedRoots.Any(root => RemovalFileInspection.IsWithin(output, root)))
                throw new InvalidDataException("この保存先は管理フォルダまたはエディションのデータ保存先に含まれます。");
            if (RemovalFileInspection.Attributes(output) is not null)
                throw new IOException("指定の保存先には既にファイルまたはフォルダがあります。");

            // Allow for ZIP metadata, the manifest, and compression's worst-case overhead.
            var namesBytes = sources.Sum(source => (long)Encoding.UTF8.GetByteCount(source.Entry.RelativePath));
            RemovalArchiveDestination.RequireSpace(parent, (ulong)(total + total / 100 + 65536
                + manifest.Length + 2 * namesBytes + 256L * sources.Length));

            using var prepared = new RemovalArchiveDestination(output);
            using (var zip = new ZipArchive(prepared.Stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var entry = zip.CreateEntry(ManifestName).Open()) entry.Write(manifest);
                foreach (var source in sources)
                {
                    WriteSource(zip, source);
                    checkpoint?.Invoke("entry-written");
                }
            }
            prepared.Stream.Flush(true);
            var hash = Verify(prepared.Stream, manifest, sources);
            checkpoint?.Invoke("archive-verified");
            EnsureUnchanged(approval, preview.Inspect(hasUnsavedChanges));
            prepared.Publish();
            return new(output, hash, sources.Length);
        }
        finally
        {
            foreach (var handle in lockedDirectories.AsEnumerable().Reverse()) handle.Dispose();
        }
    }

    private static void EnsureUnchanged(LocalDataRemovalPlan approval, LocalDataRemovalPlan current)
    {
        if (!approval.CanReviewCandidates || !current.CanReviewCandidates || current.UserSid is null
            || current.UserSid != approval.UserSid || current.IsDevelopment != approval.IsDevelopment
            || current.SavedAssignments != approval.SavedAssignments
            || !RemovalFileInspection.SamePath(current.DataDirectory, approval.DataDirectory)
            || !current.ManagedDirectories.SequenceEqual(approval.ManagedDirectories, StringComparer.OrdinalIgnoreCase)
            || !current.Items.SequenceEqual(approval.Items))
            throw new InvalidDataException("削除候補や管理フォルダの状態を再確認してください。");
    }

    private static ManifestFile ManifestEntry(string dataDirectory, RemovalItem item)
    {
        if (!RemovalFileInspection.IsWithin(item.Path, dataDirectory) || item.Bytes is not (>= 0 and <= BackupArchive.MaxTotalBytes) || item.Sha256 is null)
            throw new InvalidDataException("データ保存先内の検証済みファイルだけを保存できます。");
        var components = Path.GetRelativePath(dataDirectory, item.Path).Split(Path.DirectorySeparatorChar);
        foreach (var name in components) EnsureSafeName(name);
        return new(string.Join('/', components), item.Kind, item.Bytes.Value, item.Sha256);
    }

    private static void EnsureSafeName(string name)
    {
        var stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
        if (name.Length == 0 || name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
            && "123456789¹²³".Contains(stem[3]))
            throw new InvalidDataException("アーカイブに使用できないファイル名です。");
    }

    private static void WriteSource(ZipArchive zip, SourceEntry source)
    {
        var (item, entry) = source;
        using var input = RemovalFileInspection.OpenFile(item.Path, BackupArchive.MaxTotalBytes);
        if (input.Length != entry.Bytes || RemovalFileInspection.Identity(input, item.Path) != item.Identity)
            throw new InvalidDataException($"確認後に保存ファイルが置き換わりました: {item.Path}");
        using var output = zip.CreateEntry("files/" + entry.RelativePath).Open();
        input.CopyTo(output);
    }

    private static string Verify(Stream stream, byte[] manifest, SourceEntry[] sources)
    {
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (zip.Entries.Count != sources.Length + 1
                || !BackupArchive.ReadBounded(zip.GetEntry(ManifestName) ?? throw new InvalidDataException("アーカイブの一覧がありません。"), MaxManifestBytes)
                    .AsSpan().SequenceEqual(manifest))
                throw new InvalidDataException("アーカイブの一覧が保存前と一致しません。");

            var expected = sources.ToDictionary(source => "files/" + source.Entry.RelativePath, source => source.Entry,
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries.Where(entry => entry.FullName != ManifestName))
            {
                if (!expected.Remove(entry.FullName, out var source) || source.Bytes != entry.Length)
                    throw new InvalidDataException("アーカイブに想定外または重複する項目があります。");
                using var sha = SHA256.Create();
                using var hashed = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write, leaveOpen: true);
                BackupArchive.CopyBounded(entry, hashed, (int)source.Bytes);
                hashed.FlushFinalBlock();
                if (Convert.ToHexString(sha.Hash!) != source.Sha256)
                    throw new InvalidDataException("アーカイブ内のファイルが承認済みの内容と一致しません。");
            }
            if (expected.Count > 0) throw new InvalidDataException("アーカイブに必要な項目がありません。");
        }
        stream.Position = 0;
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record Manifest(int FormatVersion, string Edition, DateTimeOffset CreatedAt, ManifestFile[] Files);
    private sealed record ManifestFile(string RelativePath, string Kind, long Bytes, string Sha256);
    private sealed record SourceEntry(RemovalItem Item, ManifestFile Entry);
}
