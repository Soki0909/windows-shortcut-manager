using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Removal;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

public sealed class LocalDataRemovalArchiveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"removal-archive-{Guid.NewGuid():N}");
    private readonly ApplicationPaths edition;
    private readonly ApplicationPaths other;
    private readonly LocalDataRemovalPreview preview;
    private readonly string destination;

    public LocalDataRemovalArchiveTests()
    {
        edition = Edition("data", "managed", false);
        other = Edition("debug-data", "debug-managed", true);
        Directory.CreateDirectory(edition.DataDirectory);
        Directory.CreateDirectory(edition.ManagedShortcutDirectory);
        File.WriteAllText(edition.ConfigPath, ConfigJson.Serialize(new ShortcutConfig()));
        File.WriteAllText(edition.StatePath, JsonSerializer.Serialize(new { lastSuccessfulApply = DateTimeOffset.Now, configSha256 = Hash(edition.ConfigPath), managedDirectory = edition.ManagedShortcutDirectory }));
        File.WriteAllText(Path.Combine(edition.ManagedShortcutDirectory, ManagedShortcutApplier.OwnershipMarker), $"Shortcut Manager{Environment.NewLine}schemaVersion=1{Environment.NewLine}updatedAt={DateTimeOffset.Now:O}{Environment.NewLine}");
        new FileLogger(edition.LogPath).Info("test");
        preview = new(edition, other);
        destination = Path.Combine(root, "削除前.zip");
    }

    [Fact]
    public void ArchiveContainsAllApprovedFilesAndOnlySelectedManualBackup()
    {
        var backups = new BackupService(edition.BackupDirectory, "fixture");
        var automatic = backups.Create("apply", edition.ConfigPath, edition.ManagedShortcutDirectory);
        var chosen = backups.Create("manual", edition.ConfigPath, edition.ManagedShortcutDirectory, destination: Path.Combine(edition.BackupDirectory, "選択.zip"));
        var unchosen = backups.Create("manual", edition.ConfigPath, edition.ManagedShortcutDirectory, destination: Path.Combine(edition.BackupDirectory, "preserve.zip"));
        var unknown = Path.Combine(edition.DataDirectory, "unknown.cmd");
        File.WriteAllText(unknown, "preserve");
        var plan = Preview();
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);

        var receipt = Service().Create(plan, [chosen], destination, false);

        Assert.Equal(destination, receipt.Path);
        Assert.Equal(Hash(destination), receipt.Sha256);
        var expected = new[] { edition.ConfigPath, edition.StatePath, edition.LogPath, automatic, automatic + ".automatic.json", chosen };
        Assert.Equal(expected.Length, receipt.FileCount);
        using var zip = ZipFile.OpenRead(destination);
        Assert.Equal(expected.Length + 1, zip.Entries.Count);
        using var manifestEntry = zip.GetEntry("removal.json")!.Open();
        using var manifest = JsonDocument.Parse(manifestEntry);
        Assert.Equal(1, manifest.RootElement.GetProperty("FormatVersion").GetInt32());
        Assert.Equal("production", manifest.RootElement.GetProperty("Edition").GetString());
        var files = manifest.RootElement.GetProperty("Files").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("RelativePath").GetString()!, entry => entry.GetProperty("Sha256").GetString());
        foreach (var original in expected)
        {
            var relative = Relative(original);
            Assert.Equal(before[original], files[relative]);
            using var contents = zip.GetEntry("files/" + relative)!.Open();
            Assert.Equal(before[original], Convert.ToHexString(SHA256.HashData(contents)));
        }
        Assert.DoesNotContain(Relative(unchosen), files.Keys);
        Assert.DoesNotContain(Relative(unknown), files.Keys);
        Assert.Equal(before.Count + 1, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        Assert.All(before, pair => Assert.Equal(pair.Value, Hash(pair.Key)));
    }

    [Theory]
    [InlineData("modified")]
    [InlineData("identical replacement")]
    [InlineData("new unknown")]
    [InlineData("updated marker")]
    [InlineData("unsaved")]
    public void ChangedConfirmationRefusesBeforeCreatingArchive(string change)
    {
        var approved = Preview();
        switch (change)
        {
            case "modified": new FileLogger(edition.LogPath).Info("after confirmation"); break;
            case "identical replacement":
                var old = edition.LogPath + ".original";
                File.Move(edition.LogPath, old);
                File.Copy(old, edition.LogPath);
                Assert.Equal(Hash(old), Hash(edition.LogPath));
                File.Delete(old);
                break;
            case "new unknown": File.WriteAllText(Path.Combine(edition.DataDirectory, "added.txt"), "do not archive"); break;
            case "updated marker":
                var marker = Path.Combine(edition.ManagedShortcutDirectory, ManagedShortcutApplier.OwnershipMarker);
                File.WriteAllText(marker, $"Shortcut Manager{Environment.NewLine}schemaVersion=1{Environment.NewLine}updatedAt={DateTimeOffset.UnixEpoch:O}{Environment.NewLine}");
                break;
        }

        Assert.Throws<InvalidDataException>(() => Service().Create(approved, [], destination, change == "unsaved"));

        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        Assert.True(File.Exists(edition.ConfigPath));
    }

    [Fact]
    public void RejectsUnrecognizedSelectionWithoutCreatingArchive()
    {
        var userFile = Path.Combine(edition.DataDirectory, "user.txt");
        File.WriteAllText(userFile, "user bytes");

        Assert.Throws<InvalidDataException>(() => Service().Create(Preview(), [userFile], destination, false));

        Assert.False(File.Exists(destination));
        Assert.Equal("user bytes", File.ReadAllText(userFile));
    }

    [Fact]
    public void NeverOverwritesExistingDestinationAndRejectsProtectedAndAliasedDirectories()
    {
        var approved = Preview();
        foreach (var directory in new[] { edition.DataDirectory, edition.ManagedShortcutDirectory, other.DataDirectory, other.ManagedShortcutDirectory })
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "new.zip");
            Assert.Throws<InvalidDataException>(() => Service().Create(approved, [], path, false));
            Assert.False(File.Exists(path));
        }
        File.WriteAllText(destination, "existing bytes");
        Assert.Throws<IOException>(() => Service().Create(approved, [], destination, false));
        Assert.Equal("existing bytes", File.ReadAllText(destination));

        var alias = Path.Combine(root, "alias");
        Directory.CreateSymbolicLink(alias, edition.DataDirectory);
        try
        {
            Assert.Throws<InvalidDataException>(() => Service().Create(approved, [], Path.Combine(alias, "alias.zip"), false));
            Assert.False(File.Exists(Path.Combine(edition.DataDirectory, "alias.zip")));
        }
        finally { Directory.Delete(alias); }

        Assert.Throws<DirectoryNotFoundException>(() => Service().Create(approved, [], Path.Combine(root, "absent", "archive.zip"), false));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Fact]
    public void AbortedWriteLeavesNoPartialArchiveOrSourceChanges()
    {
        var plan = Preview();
        var original = Hash(edition.ConfigPath);
        var archive = Service(point => { if (point == "entry-written") throw new IOException("injected failure"); });

        Assert.Throws<IOException>(() => archive.Create(plan, [], destination, false));

        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        Assert.Equal(original, Hash(edition.ConfigPath));
    }

    [Fact]
    public void ReadbackDetectsSourceBytesChangedAfterConfirmationWithSameSize()
    {
        var plan = Preview();
        var original = File.ReadAllText(edition.LogPath);
        var affected = false;
        var archive = Service(point =>
        {
            if (point != "entry-written" || affected) return;
            affected = true;
            // The configuration sorts first. Change a later source without changing its size or file identity.
            File.WriteAllText(edition.LogPath, original.Replace("test", "next", StringComparison.Ordinal));
        });

        var error = Assert.Throws<InvalidDataException>(() => archive.Create(plan, [], destination, false));

        Assert.Contains("承認済みの内容", error.Message);
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        Assert.Contains("next", File.ReadAllText(edition.LogPath));
    }

    [Fact]
    public void RefusesWhenSourceChangesDuringCreation()
    {
        var plan = Preview();
        var archive = Service(point => { if (point == "archive-verified") new FileLogger(edition.LogPath).Info("new log"); });

        Assert.Throws<InvalidDataException>(() => archive.Create(plan, [], destination, false));

        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        Assert.Contains("new log", File.ReadAllText(edition.LogPath));
    }

    [Fact]
    public void CompetingDestinationCreatedAfterReadbackIsPreserved()
    {
        var plan = Preview();
        var archive = Service(point => { if (point == "archive-verified") File.WriteAllText(destination, "another writer"); });

        Assert.Throws<IOException>(() => archive.Create(plan, [], destination, false));

        Assert.Equal("another writer", File.ReadAllText(destination));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    private ApplicationPaths Edition(string dataName, string managedName, bool debug)
    {
        var data = Path.Combine(root, dataName);
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"),
            Path.Combine(data, "logs", "shortcut-manager.log"), Path.Combine(data, "backups"), Path.Combine(data, "staging"), Path.Combine(root, managedName), debug);
    }

    private LocalDataRemovalPlan Preview()
    {
        var result = preview.Inspect(false);
        Assert.True(result.CanReviewCandidates, string.Join(Environment.NewLine, result.Blockers));
        return result;
    }

    private LocalDataRemovalArchive Service(Action<string>? checkpoint = null) => new(edition, other, checkpoint);

    private string Relative(string path) => Path.GetRelativePath(edition.DataDirectory, path).Replace('\\', '/');

    private static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    public void Dispose() => Directory.Delete(root, true);
}
