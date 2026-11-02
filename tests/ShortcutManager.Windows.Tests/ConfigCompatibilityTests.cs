using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class ConfigCompatibilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"config-compat-{Guid.NewGuid():N}");
    public ConfigCompatibilityTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("schemaVersion", "0")]
    [InlineData("dispatchProtocolVersion", "2")]
    [InlineData("schemaVersion", "2147483648")]
    [InlineData("schemaVersion", "-2147483649")]
    [InlineData("schemaVersion", "1.5")]
    [InlineData("schemaVersion", "1e100")]
    [InlineData("dispatchProtocolVersion", "1e100")]
    public async Task UnsupportedVersionCannotBeOverwrittenByApplyRestoreSaveOrInitialize(string property, string version)
    {
        var paths = new ApplicationPaths(root, Path.Combine(root, "config.json"), Path.Combine(root, "state.json"), Path.Combine(root, "log.txt"), Path.Combine(root, "backups"), Path.Combine(root, "stage"), Path.Combine(root, "managed"));
        var repository = new ConfigRepository(paths.ConfigPath);
        var resolver = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder());
        var backups = new BackupService(paths.BackupDirectory, "test");
        var links = new Windows.ShellLinks.ShellLinkService();
        var applier = new ManagedShortcutApplier(repository, new(), resolver, links, backups, paths.StagingDirectory, paths.StatePath);
        var restore = new RestoreService(new(), applier);
        Assert.True(applier.Apply(new(), paths.ManagedShortcutDirectory).Succeeded);
        var compatibleBackup = backups.Create("manual", paths.ConfigPath, paths.ManagedShortcutDirectory);
        // Unknown target/member must not hide the version-specific diagnostic.
        var future = ConfigJson.Serialize(new ShortcutConfig()).Replace($"\"{property}\": 1", $"\"{property}\": {version}").Replace("\"settings\": {", "\"futureMember\": true, \"settings\": {");
        File.WriteAllText(paths.ConfigPath, future);
        var marker = File.ReadAllBytes(Path.Combine(paths.ManagedShortcutDirectory, ManagedShortcutApplier.OwnershipMarker));
        var state = File.ReadAllBytes(paths.StatePath);
        var count = Directory.GetFiles(paths.BackupDirectory).Length;

        Assert.Throws<UnsupportedConfigVersionException>(repository.Load);
        Assert.Throws<UnsupportedConfigVersionException>(() => repository.SaveAtomic(new()));
        var apply = applier.Apply(new(), paths.ManagedShortcutDirectory);
        Assert.False(apply.Succeeded);
        Assert.Contains(property, apply.Message);
        Assert.False(restore.Restore(compatibleBackup, paths.ManagedShortcutDirectory).Succeeded);
        var vm = new App.MainViewModel(paths, repository, new(), resolver, new(), new(links, new()), new(), backups, applier, restore, new(paths.LogPath), new Windows.Shell.ExplorerShellReloader());
        Assert.True(vm.RecoveryRequired);
        Assert.False(vm.CanApply);
        Assert.False(vm.CanInitializeRecovery);
        Assert.Throws<UnsupportedConfigVersionException>(vm.InitializeRecovery);
        Assert.False(await vm.AuditStartupAsync(new(new(), resolver, links)));
        Assert.Equal(future, File.ReadAllText(paths.ConfigPath));
        Assert.Equal(marker, File.ReadAllBytes(Path.Combine(paths.ManagedShortcutDirectory, ManagedShortcutApplier.OwnershipMarker)));
        Assert.Equal(state, File.ReadAllBytes(paths.StatePath));
        Assert.Equal(count, Directory.GetFiles(paths.BackupDirectory).Length);
    }

    [Fact]
    public void PublishedSchemaOneLoadsWithoutRewritingAndMalformedJsonStillAllowsExplicitRecovery()
    {
        var repository = new ConfigRepository(Path.Combine(root, "config.json"));
        var legacy = ConfigJson.Serialize(new ShortcutConfig());
        File.WriteAllText(repository.Path, legacy);
        Assert.Equal(1, repository.Load().SchemaVersion);
        Assert.Equal(legacy, File.ReadAllText(repository.Path));
        File.WriteAllText(repository.Path, "broken JSON");
        repository.EnsureCompatibleVersion();
        repository.SaveAtomic(new());
        Assert.Equal(1, repository.Load().SchemaVersion);
    }

    [Fact]
    public void DirectSaveCannotIntroduceUnsupportedVersion()
    {
        var repository = new ConfigRepository(Path.Combine(root, "new-config.json"));
        Assert.Throws<UnsupportedConfigVersionException>(() => repository.SaveAtomic(new() { SchemaVersion = 2 }));
        Assert.False(File.Exists(repository.Path));
    }

    [Fact]
    public void ExtremeVersionNumberHasBoundedDiagnostic()
    {
        var json = ConfigJson.Serialize(new ShortcutConfig()).Replace("\"schemaVersion\": 1", "\"schemaVersion\": " + new string('9', 4096));
        var error = Assert.Throws<UnsupportedConfigVersionException>(() => ConfigJson.Deserialize(json));
        Assert.True(error.Message.Length < 256);
    }

    public void Dispose() => Directory.Delete(root, true);
}
