using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.Windows.Backup;

public sealed record BackupManifest(int FormatVersion, DateTimeOffset CreatedAt, string AppVersion, string Reason, string ConfigSha256);

public sealed class BackupService(string backupDirectory, string appVersion)
{
    public string Create(string reason, string configPath, string managedDirectory, string? statePath = null, string? destination = null)
    {
        Directory.CreateDirectory(backupDirectory);
        var path = destination ?? System.IO.Path.Combine(backupDirectory, $"{DateTime.Now:yyyyMMdd-HHmmssfff}_{Guid.NewGuid():N}.zip");
        if (File.Exists(path)) File.Delete(path);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var configBytes = File.Exists(configPath) ? File.ReadAllBytes(configPath) : Encoding.UTF8.GetBytes(ConfigJson.Serialize(new ShortcutConfig()));
        WriteEntry(archive, "backup.json", JsonSerializer.SerializeToUtf8Bytes(new BackupManifest(1, DateTimeOffset.Now, appVersion, reason, Convert.ToHexString(SHA256.HashData(configBytes)).ToLowerInvariant()), new JsonSerializerOptions { WriteIndented = true }));
        WriteEntry(archive, "config.json", configBytes);
        if (Directory.Exists(managedDirectory))
            foreach (var file in Directory.EnumerateFiles(managedDirectory, "*", SearchOption.TopDirectoryOnly)) WriteEntry(archive, $"managed-shortcuts/{System.IO.Path.GetFileName(file)}", File.ReadAllBytes(file));
        if (statePath is not null && File.Exists(statePath)) WriteEntry(archive, "state.json", File.ReadAllBytes(statePath));
        return path;
    }

    public void EnforceRetention(int keep = 20)
    {
        if (!Directory.Exists(backupDirectory)) return;
        foreach (var file in new DirectoryInfo(backupDirectory).GetFiles("*.zip").OrderByDescending(file => file.CreationTimeUtc).Skip(keep)) file.Delete();
    }

    public static BackupManifest Inspect(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("backup.json") ?? throw new InvalidDataException("backup.json is missing.");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<BackupManifest>(stream) ?? throw new InvalidDataException("backup.json is invalid.");
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var output = entry.Open(); output.Write(bytes);
    }
}
