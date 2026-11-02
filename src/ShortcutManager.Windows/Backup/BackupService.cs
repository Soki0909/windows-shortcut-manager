using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.Windows.Backup;

public sealed record BackupManifest(int FormatVersion, DateTimeOffset CreatedAt, string AppVersion, string Reason, string ConfigSha256);

public sealed class BackupService(string backupDirectory, string appVersion, Action<string>? checkpoint = null)
{
    public string Create(string reason, string configPath, string managedDirectory, string? statePath = null, string? destination = null)
    {
        Directory.CreateDirectory(backupDirectory);
        var path = destination ?? System.IO.Path.Combine(backupDirectory, $"{DateTime.Now:yyyyMMdd-HHmmssfff}_{Guid.NewGuid():N}.zip");
        path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                long totalBytes = 0;
                void AddEntry(string name, byte[] bytes)
                {
                    totalBytes += bytes.LongLength;
                    if (totalBytes > BackupArchive.MaxTotalBytes) throw new InvalidDataException("Backup sources exceed the total size limit.");
                    checkpoint?.Invoke($"entry:{name}");
                    WriteEntry(archive, name, bytes);
                }
                var configBytes = File.Exists(configPath) ? ReadSource(configPath, BackupArchive.MaxConfigBytes) : Encoding.UTF8.GetBytes(ConfigJson.Serialize(new ShortcutConfig()));
                AddEntry("backup.json", JsonSerializer.SerializeToUtf8Bytes(new BackupManifest(1, DateTimeOffset.Now, appVersion, reason, Convert.ToHexString(SHA256.HashData(configBytes)).ToLowerInvariant()), new JsonSerializerOptions { WriteIndented = true }));
                AddEntry("config.json", configBytes);
                var sourceCount = 0;
                if (Directory.Exists(managedDirectory))
                    foreach (var file in Directory.EnumerateFiles(managedDirectory, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (++sourceCount > BackupArchive.MaxEntries - 3) throw new InvalidDataException("Too many backup files.");
                        AddEntry($"managed-shortcuts/{System.IO.Path.GetFileName(file)}", ReadSource(file, BackupArchive.MaxEntryBytes));
                    }
                if (statePath is not null && File.Exists(statePath)) AddEntry("state.json", ReadSource(statePath, BackupArchive.MaxEntryBytes));
                checkpoint?.Invoke("written");
            }
            _ = BackupArchive.Read(temporary);
            using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
            checkpoint?.Invoke("verified");
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            // A receipt identifies backups created automatically here. Imported ZIPs and
            // historical archives without receipts are deliberately never collected.
            if (destination is null && reason is "apply" or "restore")
            {
                var receipt = new AutomaticBackupReceipt(1, System.IO.Path.GetFileName(path),
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
                using var stream = new FileStream(path + ".automatic.json", FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                JsonSerializer.Serialize(stream, receipt);
                stream.Flush(true);
            }
            return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void EnforceRetention(int keep = 20)
    {
        if (keep < 1) throw new ArgumentOutOfRangeException(nameof(keep), "Keep at least one automatic backup.");
        if (!Directory.Exists(backupDirectory)) return;
        var eligible = new List<(FileInfo File, DateTimeOffset CreatedAt)>();
        foreach (var file in new DirectoryInfo(backupDirectory).GetFiles("*.zip"))
        {
            var receiptPath = file.FullName + ".automatic.json";
            try
            {
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint) || !File.Exists(receiptPath)) continue;
                var receiptFile = new FileInfo(receiptPath);
                if (receiptFile.Attributes.HasFlag(FileAttributes.ReparsePoint) || receiptFile.Length > 4096 || file.Length > BackupArchive.MaxTotalBytes) continue;
                var receipt = JsonSerializer.Deserialize<AutomaticBackupReceipt>(File.ReadAllText(receiptPath));
                if (receipt is null || receipt.FormatVersion != 1 || receipt.Filename != file.Name
                    || receipt.Sha256 != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullName)))) continue;
                var manifest = Inspect(file.FullName);
                if (manifest.Reason is "apply" or "restore") eligible.Add((file, manifest.CreatedAt));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                // Unknown, inaccessible, modified or corrupt data is not ours to remove.
            }
        }
        foreach (var entry in eligible.OrderByDescending(entry => entry.CreatedAt).ThenByDescending(entry => entry.File.Name, StringComparer.Ordinal).Skip(keep))
        {
            entry.File.Delete();
            File.Delete(entry.File.FullName + ".automatic.json");
        }
    }

    private sealed record AutomaticBackupReceipt(int FormatVersion, string Filename, string Sha256);

    public static bool HasAutomaticReceipt(string path)
    {
        try
        {
            var zip=new FileInfo(path);var receiptFile=new FileInfo(path+".automatic.json");
            if(!receiptFile.Exists || zip.Attributes.HasFlag(FileAttributes.ReparsePoint) || receiptFile.Attributes.HasFlag(FileAttributes.ReparsePoint) || receiptFile.Length>4096 || zip.Length>BackupArchive.MaxTotalBytes)return false;
            var receipt=JsonSerializer.Deserialize<AutomaticBackupReceipt>(File.ReadAllText(receiptFile.FullName));
            return receipt is not null && receipt.FormatVersion==1 && receipt.Filename==zip.Name && receipt.Sha256==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException) {return false;}
    }

    public static BackupManifest Inspect(string path)
    {
        return BackupArchive.Read(path).Manifest;
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var output = entry.Open(); output.Write(bytes);
    }

    private static byte[] ReadSource(string path, int limit)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > limit) throw new InvalidDataException($"Backup source exceeds size limit: {path}");
        var bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        return bytes;
    }
}
