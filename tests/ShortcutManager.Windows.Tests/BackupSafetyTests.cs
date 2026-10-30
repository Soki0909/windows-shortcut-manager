using System.IO.Compression;
using System.Text;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Backup;

namespace ShortcutManager.Windows.Tests;

public sealed class BackupSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"backup-safety-{Guid.NewGuid():N}");
    public BackupSafetyTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private string Create(Action<string>? checkpoint = null, string? destination = null)
    {
        var config = Path.Combine(root, "config.json");
        File.WriteAllText(config, ConfigJson.Serialize(new ShortcutConfig()));
        return new BackupService(root, "test", checkpoint).Create("manual", config, Path.Combine(root, "managed"), destination: destination);
    }

    [Theory]
    [InlineData("written")]
    [InlineData("verified")]
    [InlineData("entry:config.json")]
    public void FailedReplacementPreservesPreviouslyRestorableBackup(string phase)
    {
        var path = Create();
        var bytes = File.ReadAllBytes(path);
        Assert.Throws<IOException>(() => Create(point => { if (point == phase) throw new IOException("Injected disk full"); }, path));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal("manual", BackupService.Inspect(path).Reason);
        Assert.Empty(Directory.GetFiles(root, "*.tmp-*"));
        Create(destination: path);
        Assert.Equal("manual", BackupService.Inspect(path).Reason);
    }

    [Theory]
    [InlineData("config.json", "{}")] // duplicate exact name
    [InlineData("CONFIG.JSON", "{}")] // duplicate Windows name
    [InlineData("../escape", "x")]
    [InlineData("managed-shortcuts/../escape", "x")]
    [InlineData("managed-shortcuts/nested/file", "x")]
    [InlineData("managed-shortcuts/C:escape.lnk", "x")]
    public void RejectsDuplicateAndUnexpectedEntries(string name, string content)
    {
        var path = Create();
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write(content);
        Assert.Throws<InvalidDataException>(() => BackupArchive.Read(path));
    }

    [Fact]
    public void RejectsTamperedConfig()
    {
        var path = Create();
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            archive.GetEntry("config.json")!.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("config.json").Open());
            writer.Write("{}");
        }
        Assert.Throws<InvalidDataException>(() => BackupArchive.Read(path));
    }

    [Fact]
    public void RejectsHugeConfigAndMalformedZip()
    {
        var path = Create();
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            archive.GetEntry("config.json")!.Delete();
            using var stream = archive.CreateEntry("config.json").Open();
            stream.Write(new byte[BackupArchive.MaxConfigBytes + 1]);
        }
        Assert.Throws<InvalidDataException>(() => BackupArchive.Read(path));
        File.WriteAllText(path, "broken zip");
        Assert.Throws<InvalidDataException>(() => BackupArchive.Read(path));
    }

    [Theory]
    [InlineData(129, 0)]
    [InlineData(9, BackupArchive.MaxEntryBytes)]
    [InlineData(1, BackupArchive.MaxEntryBytes + 1)]
    public void RejectsExcessiveEntryCountExpandedTotalAndUnusedEntrySize(int count, int size)
    {
        var path = Create();
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var bytes = new byte[size];
            for (var index = 0; index < count; index++)
            {
                using var stream = archive.CreateEntry($"managed-shortcuts/test-{index}.lnk", CompressionLevel.Optimal).Open();
                stream.Write(bytes);
            }
        }
        Assert.Throws<InvalidDataException>(() => BackupArchive.Read(path));
    }
}
