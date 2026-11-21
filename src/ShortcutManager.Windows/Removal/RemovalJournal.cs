using System.Text.Json;

namespace ShortcutManager.Windows.Removal;

internal sealed record RemovalJournalEntry(string RelativePath, RemovalFileIdentity Identity, long Bytes, string Sha256);
internal sealed record RemovalJournalData(int Version, bool IsDevelopment, string UserSid, string DataDirectory,
    string ArchivePath, string ArchiveSha256, RemovalJournalEntry[] Files);

internal static class RemovalJournal
{
    internal const string RootName = "removal-recovery";
    internal const string Intent = "journal.json";
    internal const string Staged = "staged";
    internal const string Cancelling = "cancelling";
    internal const string Cancelled = "cancelled";
    internal const string Committing = "committing";
    internal const string Committed = "committed";
    private const int MaxJournalBytes = 4 * 1024 * 1024;
    internal const int MaxCount = 4096;

    private static readonly JsonSerializerOptions StrictJson = new()
    {
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static string? FindActive(string data)
    {
        var root = Path.Combine(data, RootName);
        if (RemovalFileInspection.Attributes(root) is null) return null;
        RemovalFileInspection.DirectoryPath(root);
        string? active = null;
        foreach (var operation in Directory.EnumerateFileSystemEntries(root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(operation), "N", out _))
                throw new InvalidDataException($"退避フォルダに確認できない項目があります: {operation}");
            RemovalFileInspection.DirectoryPath(operation);
            if (HasMarker(operation, Cancelled) || HasMarker(operation, Committed))
            {
                if (Directory.EnumerateFileSystemEntries(operation).Any(path => !IsRecordName(Path.GetFileName(path))))
                    throw new InvalidDataException($"完了済みの退避フォルダに項目が残っています: {operation}");
                continue;
            }
            // The operation directory can be left empty if the process exited
            // before the immutable journal was published.
            if (!Directory.EnumerateFileSystemEntries(operation).Any()) continue;
            if (active is not null) throw new InvalidDataException("未完了の退避記録が複数あります。");
            active = operation;
        }
        return active;
    }

    public static void WriteIntent(string operation, RemovalJournalData data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        if (bytes.Length > MaxJournalBytes) throw new InvalidDataException("退避記録のサイズが上限を超えています。");
        WriteNew(operation, Intent, bytes);
    }

    public static RemovalJournalData ReadIntent(string operation)
    {
        using var input = RemovalFileInspection.OpenFile(Path.Combine(operation, Intent), MaxJournalBytes);
        return JsonSerializer.Deserialize<RemovalJournalData>(input, StrictJson)
            ?? throw new InvalidDataException("退避記録を読み取れません。");
    }

    public static bool HasMarker(string operation, string name)
    {
        var path = Path.Combine(operation, name);
        if (RemovalFileInspection.Attributes(path) is null) return false;
        using var marker = RemovalFileInspection.OpenFile(path, 1);
        if (marker.ReadByte() != 1) throw new InvalidDataException($"退避の進行記録が不正です: {path}");
        return true;
    }

    public static void Mark(string operation, string name) => WriteNew(operation, name, [1]);
    public static string Slot(string operation, int index) => Path.Combine(operation, $"stash-{index:D4}");

    private static void WriteNew(string operation, string name, byte[] bytes)
    {
        using var record = new RemovalArchiveDestination(Path.Combine(operation, name));
        record.Stream.Write(bytes);
        record.Stream.Flush(true);
        record.Publish();
    }

    private static bool IsRecordName(string name) => name is Intent or Staged or Cancelling or Cancelled or Committing or Committed;
}
