using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.Windows.Backup;

// Read the same bounded bytes for verification and restoration; never extract ZIP paths.
public static class BackupArchive
{
    public const int MaxEntries = 128;
    public const int MaxConfigBytes = 1024 * 1024;
    public const int MaxEntryBytes = 4 * 1024 * 1024;
    public const int MaxTotalBytes = 32 * 1024 * 1024;

    public static (BackupManifest Manifest, byte[] Config) Read(string path)
    {
        if (new FileInfo(path).Length > MaxTotalBytes) throw new InvalidDataException("Backup ZIP exceeds 32 MiB.");
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("Too many backup entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (!names.Add(name)) throw new InvalidDataException($"Duplicate backup entry: {name}");
            var managed = name.StartsWith("managed-shortcuts/", StringComparison.Ordinal)
                && name[18..].Length > 0 && !name[18..].Contains('/') && !name.Contains('\\')
                && name[18..] is not "." and not "..";
            if (name is not "backup.json" and not "config.json" and not "state.json" && !managed)
                throw new InvalidDataException($"Unexpected backup entry: {name}");
            var limit = name == "config.json" ? MaxConfigBytes : name == "backup.json" ? 16384 : MaxEntryBytes;
            if (entry.Length > limit || (total += entry.Length) > MaxTotalBytes)
                throw new InvalidDataException($"Backup entry exceeds size limit: {name}");
            // Consume every entry with a hard limit, including state/link data not restored directly.
            _ = ReadBounded(entry, limit);
        }
        var manifestBytes = ReadBounded(archive.GetEntry("backup.json") ?? throw new InvalidDataException("backup.json is missing."), 16384);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestBytes) ?? throw new InvalidDataException("Invalid backup manifest.");
        if (manifest.FormatVersion != 1 || string.IsNullOrWhiteSpace(manifest.AppVersion) || string.IsNullOrWhiteSpace(manifest.Reason)
            || manifest.CreatedAt == default || manifest.ConfigSha256 is null || manifest.ConfigSha256.Length != 64)
            throw new InvalidDataException("Unsupported or invalid backup manifest.");
        var config = ReadBounded(archive.GetEntry("config.json") ?? throw new InvalidDataException("config.json is missing."), MaxConfigBytes);
        var hash = Convert.ToHexString(SHA256.HashData(config));
        if (!hash.Equals(manifest.ConfigSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup config SHA-256 mismatch.");
        return (manifest, config);
    }

    private static byte[] ReadBounded(ZipArchiveEntry entry, int limit)
    {
        using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = input.Read(buffer)) != 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException($"Backup entry exceeds size limit: {entry.FullName}");
            output.Write(buffer, 0, count);
        }
        if (output.Length != entry.Length) throw new InvalidDataException("Backup entry length mismatch.");
        return output.ToArray();
    }
}
