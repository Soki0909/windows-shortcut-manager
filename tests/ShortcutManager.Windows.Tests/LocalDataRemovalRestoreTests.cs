using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Removal;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class LocalDataRemovalRestoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"shortcut-manager-tests-restore-{Guid.NewGuid():N}");
    private readonly ApplicationPaths edition;
    private readonly ApplicationPaths other;
    private readonly Dictionary<string, string> originalHashes;
    private readonly VerifiedRemovalArchive archive;

    public LocalDataRemovalRestoreTests()
    {
        edition = Edition("data", "managed", false);
        other = Edition("debug-data", "debug-managed", true);
        Directory.CreateDirectory(edition.DataDirectory);
        File.WriteAllText(edition.ConfigPath, ConfigJson.Serialize(new ShortcutConfig()));
        File.WriteAllText(edition.StatePath, JsonSerializer.Serialize(new
        {
            lastSuccessfulApply = DateTimeOffset.Now,
            configSha256 = Hash(edition.ConfigPath),
            managedDirectory = edition.ManagedShortcutDirectory
        }));
        new FileLogger(edition.LogPath).Info("restore test");
        originalHashes = new[] { edition.ConfigPath, edition.LogPath }.ToDictionary(path => path, Hash);

        var plan = new LocalDataRemovalPreview(edition, other).Inspect(false);
        archive = new LocalDataRemovalArchive(edition, other).Create(plan, [], Path.Combine(root, "before.zip"), false);
        var staging = new LocalDataRemovalStaging(edition, other);
        staging.Stage(plan, [], archive, false);
        staging.Commit();
    }

    [Fact]
    public void RestoresDeletedDataFromVerifiedArchiveAndSkipsStaleStateFile()
    {
        var restore = new LocalDataRemovalRestore(edition, other);
        var preview = restore.Inspect(archive.Path);

        Assert.True(preview.CanRestore);
        Assert.Empty(preview.Conflicts);
        Assert.Empty(preview.Blockers);
        Assert.Equal("production", preview.Edition);
        Assert.Equal(2, preview.Items.Count(item => item.WillRestore));
        Assert.Single(preview.Items, item => !item.WillRestore && item.RelativePath.Equals("state.json", StringComparison.OrdinalIgnoreCase));

        var result = restore.Restore(preview);

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(1, result.SkippedStateCount);
        Assert.True(result.RestoredConfig);
        Assert.All(originalHashes, pair => Assert.Equal(pair.Value, Hash(pair.Key)));
        Assert.False(File.Exists(edition.StatePath));
    }

    [Fact]
    public void RejectsArchiveFromDifferentEdition()
    {
        var restoreToDebug = new LocalDataRemovalRestore(other, edition);
        Assert.Throws<InvalidDataException>(() => restoreToDebug.Inspect(archive.Path));
    }

    [Fact]
    public void BlocksRestoreWhenDestinationFileAlreadyExists()
    {
        File.WriteAllText(edition.ConfigPath, "existing-user-config");
        var restore = new LocalDataRemovalRestore(edition, other);
        var preview = restore.Inspect(archive.Path);

        Assert.False(preview.CanRestore);
        Assert.Contains("config.json", preview.Conflicts, StringComparer.OrdinalIgnoreCase);
        Assert.Throws<InvalidDataException>(() => restore.Restore(preview));
        Assert.Equal("existing-user-config", File.ReadAllText(edition.ConfigPath));
        Assert.False(File.Exists(edition.LogPath));
    }

    [Fact]
    public void RejectsTamperedArchiveBeforeWritingAnything()
    {
        var tampered = Path.Combine(root, "tampered.zip");
        File.Copy(archive.Path, tampered);
        using (var zip = ZipFile.Open(tampered, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("files/config.json")!;
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("files/config.json").Open());
            writer.Write("{\"tampered\":true}");
        }

        var restore = new LocalDataRemovalRestore(edition, other);
        Assert.Throws<InvalidDataException>(() => restore.Inspect(tampered));
        Assert.False(File.Exists(edition.ConfigPath));
    }

    [Fact]
    public void RollsBackNewlyCreatedFilesWhenInterruptedMidRestore()
    {
        var calls = 0;
        var restore = new LocalDataRemovalRestore(edition, other, point =>
        {
            if (point == "file-restored" && ++calls == 1) throw new InvalidOperationException("simulated interruption");
        });
        var preview = restore.Inspect(archive.Path);

        Assert.Throws<InvalidOperationException>(() => restore.Restore(preview));
        Assert.False(File.Exists(edition.ConfigPath));
        Assert.False(File.Exists(edition.LogPath));
    }

    [Fact]
    public void ExternalBackupDestinationValidationRejectsProtectedDirectoriesAndExistingFiles()
    {
        var insideData = Path.Combine(edition.DataDirectory, "before-disable.zip");
        var insideManaged = Path.Combine(edition.ManagedShortcutDirectory, "before-disable.zip");
        var insideOther = Path.Combine(other.DataDirectory, "before-disable.zip");
        var valid = Path.Combine(root, "outside-before-disable.zip");

        Directory.CreateDirectory(edition.ManagedShortcutDirectory);
        Directory.CreateDirectory(other.DataDirectory);

        Assert.Throws<InvalidDataException>(() =>
            LocalDataRemovalArchive.ValidateExternalBackupDestination(edition, other, edition.ManagedShortcutDirectory, insideData));
        Assert.Throws<InvalidDataException>(() =>
            LocalDataRemovalArchive.ValidateExternalBackupDestination(edition, other, edition.ManagedShortcutDirectory, insideManaged));
        Assert.Throws<InvalidDataException>(() =>
            LocalDataRemovalArchive.ValidateExternalBackupDestination(edition, other, edition.ManagedShortcutDirectory, insideOther));
        LocalDataRemovalArchive.ValidateExternalBackupDestination(edition, other, edition.ManagedShortcutDirectory, valid);

        File.WriteAllText(valid, "existing");
        Assert.Throws<IOException>(() =>
            LocalDataRemovalArchive.ValidateExternalBackupDestination(edition, other, edition.ManagedShortcutDirectory, valid));
    }

    private static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private ApplicationPaths Edition(string dataName, string managedName, bool debug)
    {
        var data = Path.Combine(root, dataName);
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"),
            Path.Combine(data, "logs", "shortcut-manager.log"), Path.Combine(data, "backups"), Path.Combine(data, "staging"),
            Path.Combine(root, managedName), debug);
    }

    public void Dispose() => Directory.Delete(root, true);
}
