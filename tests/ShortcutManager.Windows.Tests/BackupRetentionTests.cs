using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Backup;

namespace ShortcutManager.Windows.Tests;

public sealed class BackupRetentionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"backup-retention-{Guid.NewGuid():N}");
    private readonly BackupService backups;
    private readonly string config;

    public BackupRetentionTests()
    {
        Directory.CreateDirectory(root);
        config = Path.Combine(root, "config.json");
        File.WriteAllText(config, ConfigJson.Serialize(new ShortcutConfig()));
        backups = new(root, "test");
    }

    private string Create(string reason = "apply", string? destination = null) => backups.Create(reason, config, Path.Combine(root, "managed"), destination: destination);

    [Fact]
    public void RetainsOnlyReceiptedAutomaticBackupsAndPreservesManualUnknownAndCopies()
    {
        var manual = Create("manual");
        var named = Create("apply", Path.Combine(root, "chosen.zip"));
        var historical = Create();
        File.Delete(historical + ".automatic.json");
        var broken = Path.Combine(root, "unknown.zip");
        File.WriteAllText(broken, "unknown data");
        var originals = Enumerable.Range(0, 4).Select(_ => Create()).ToArray();
        var imported = Path.Combine(root, "imported.zip");
        File.Copy(originals[0], imported);
        // Even copying a receipt does not claim an archive with a different name.
        File.Copy(originals[0] + ".automatic.json", imported + ".automatic.json");
        foreach (var file in Directory.GetFiles(root, "*.zip")) File.SetCreationTimeUtc(file, DateTime.UnixEpoch);
        var protectedFiles = new[] { manual, named, historical, broken, imported };
        var before = protectedFiles.ToDictionary(path => path, File.ReadAllBytes);

        backups.EnforceRetention(2);

        Assert.Equal(2, originals.Count(File.Exists));
        foreach (var pair in before) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        foreach (var removed in originals.Where(path => !File.Exists(path))) Assert.False(File.Exists(removed + ".automatic.json"));
        backups.EnforceRetention(2);
        Assert.Equal(2, originals.Count(File.Exists));
    }

    [Fact]
    public void ChangedArchiveAndInvalidReceiptArePreserved()
    {
        var changed = Create();
        File.AppendAllText(changed, "external modification");
        var invalid = Create();
        File.WriteAllText(invalid + ".automatic.json", "{broken");
        var bytes = File.ReadAllBytes(changed);
        Create("restore");
        Create();
        backups.EnforceRetention(1);
        Assert.Equal(bytes, File.ReadAllBytes(changed));
        Assert.True(File.Exists(invalid));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefusesPolicyThatWouldDeleteAllRecoveryPoints(int keep)
    {
        var path = Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => backups.EnforceRetention(keep));
        Assert.True(File.Exists(path));
    }

    public void Dispose() => Directory.Delete(root, true);
}
