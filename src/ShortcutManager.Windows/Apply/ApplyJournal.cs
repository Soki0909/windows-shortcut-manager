using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Windows.Shell;

namespace ShortcutManager.Windows.Apply;

// Written and flushed before the first product mutation. Recovery is idempotent:
// snapshots are copied, never consumed, until the complete state is durable.
internal sealed record JournalFile(string Destination, string? Snapshot, string? BeforeHash, string? AfterHash);
internal sealed record JournalData(string ConfigPath, string StatePath, string ManagedDirectory, JournalFile[] Files);

internal static class ApplyJournal
{
    public const string Filename = "journal.json";
    public const string Terminal = "complete";

    public static void FlushWrite(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static void Prepare(string root, string configPath, string statePath, string managed, Dictionary<string, byte[]?> before, Dictionary<string, byte[]?> after)
    {
        var files = new List<JournalFile>();
        foreach (var path in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var snapshot = before.GetValueOrDefault(path);
            var name = snapshot is null ? null : $"snapshot-{files.Count}";
            if (name is not null) FlushWrite(Path.Combine(root, name), snapshot!);
            files.Add(new(path, name, snapshot is null ? null : Hash(snapshot), after.GetValueOrDefault(path) is { } next ? Hash(next) : null));
        }
        FlushWrite(Path.Combine(root, Filename), JsonSerializer.SerializeToUtf8Bytes(new JournalData(configPath, statePath, managed, files.ToArray())));
    }

    public static void Complete(string root) => AtomicRestore(Path.Combine(root, Terminal), [1]);

    public static void Phase(string root, string phase) => AtomicRestore(Path.Combine(root, "phase.json"), JsonSerializer.SerializeToUtf8Bytes(new { phase, at = DateTimeOffset.UtcNow }));

    public static int Recover(string workingRoot, string configPath, string statePath, IShellChangeNotifier? notifier, Action<string>? checkpoint)
    {
        if (!Directory.Exists(workingRoot)) return 0;
        var count = 0;
        foreach (var root in Directory.EnumerateDirectories(workingRoot).Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)))
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("復旧用フォルダがreparse pointです。");
            if (File.Exists(Path.Combine(root, Terminal))) { Cleanup(root); continue; }
            var path = Path.Combine(root, Filename);
            if (!File.Exists(path))
            {
                if (File.Exists(Path.Combine(root, "recovery.json"))) throw new InvalidDataException($"以前の復旧記録を手動確認してください: {root}");
                // No durable intent means no product writes began. Leave unrecognized
                // staging contents alone rather than deleting an unowned directory.
                continue;
            }
            if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("復旧記録が大きすぎます。");
            var data = JsonSerializer.Deserialize<JournalData>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("復旧記録が空です。");
            if (!Same(data.ConfigPath, configPath) || !Same(data.StatePath, statePath) || data.Files.Length > 55)
                throw new InvalidDataException($"復旧記録の保存先が一致しません: {root}");
            var names = data.Files.Where(file => Same(Path.GetDirectoryName(file.Destination)!, data.ManagedDirectory)).Select(file => Path.GetFileName(file.Destination)).ToArray();
            ManagedShortcutApplier.EnsureOwnershipSafe(data.ManagedDirectory, names, allowMissingMarker: true);
            if (Directory.Exists(data.ManagedDirectory) && Directory.EnumerateFiles(data.ManagedDirectory).Any(file => !names.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("中断後に追加されたファイルを保護しました。");
            var snapshots = new List<(JournalFile File, byte[]? Bytes)>();
            foreach (var file in data.Files)
            {
                if (!Same(file.Destination, configPath) && !Same(file.Destination, statePath) &&
                    !(Same(Path.GetDirectoryName(file.Destination)!, data.ManagedDirectory) &&
                      (Path.GetFileName(file.Destination) == ManagedShortcutApplier.OwnershipMarker || System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file.Destination), @"^[A-Z] - .+\.lnk$"))))
                    throw new InvalidDataException("復旧記録に想定外の書込み先があります。");
                byte[]? bytes = null;
                if (file.Snapshot is not null)
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(file.Snapshot, @"^snapshot-\d+$")) throw new InvalidDataException("不正な退避名です。");
                    bytes = ReadBounded(Path.Combine(root, file.Snapshot));
                    if (Hash(bytes) != file.BeforeHash) throw new InvalidDataException($"退避データが破損しています: {root}");
                }
                if (File.Exists(file.Destination))
                {
                    var hash = Hash(ReadBounded(file.Destination));
                    if (hash != file.BeforeHash && hash != file.AfterHash)
                        throw new InvalidDataException($"中断後に変更されたファイルを保護しました。復旧先を確認してください: {file.Destination}");
                }
                snapshots.Add((file, bytes));
            }
            // Validate every source and current destination before restoring any file.
            foreach (var (file, bytes) in snapshots)
            {
                if (bytes is null) { if (File.Exists(file.Destination)) File.Delete(file.Destination); }
                else AtomicRestore(file.Destination, bytes);
                notifier?.NotifyItemChanged(file.Destination);
                checkpoint?.Invoke("recovery-item-restored");
            }
            notifier?.NotifyDirectoryChanged(data.ManagedDirectory);
            Complete(root);
            Cleanup(root);
            count++;
        }
        return count;
    }

    private static byte[] ReadBounded(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("復旧ファイルのreparse pointは許可されません。");
        using var stream = File.OpenRead(path);
        if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("復旧ファイルが大きすぎます。");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }

    public static void AtomicRestore(string path, byte[] bytes)
    {
        var temp = path + ".recover-" + Guid.NewGuid().ToString("N");
        try { FlushWrite(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static bool Same(string left, string right) => Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    private static void Cleanup(string root) { try { Directory.Delete(root, true); } catch { /* A terminal journal can be cleaned next time. */ } }
}
