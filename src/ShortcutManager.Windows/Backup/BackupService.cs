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
            return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void EnforceRetention(int keep = 20)
    {
        if (!Directory.Exists(backupDirectory)) return;
        foreach (var file in new DirectoryInfo(backupDirectory).GetFiles("*.zip").OrderByDescending(file => file.CreationTimeUtc).Skip(keep)) file.Delete();
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
