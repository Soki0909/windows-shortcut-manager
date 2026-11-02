using ShortcutManager.Core.Protocol;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

public sealed class LoggingSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"logging-safety-{Guid.NewGuid():N}");
    public LoggingSafetyTests() => Directory.CreateDirectory(root);

    [Fact]
    public void LockedLogReportsFailureThenRecoversWithoutThrowing()
    {
        var path = Path.Combine(root, "log.txt");
        var logger = new FileLogger(path);
        var changes = 0;
        logger.FailureChanged += () => changes++;
        using (File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            logger.Error("original failure", new IOException("original"));
            Assert.NotNull(logger.LastFailure);
        }
        logger.Info("recovered");
        Assert.Null(logger.LastFailure);
        Assert.Contains("recovered", File.ReadAllText(path));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void UnreadableConfigAndUnwritableLogPreserveConfigAndRecoveryMode()
    {
        var blocker = Path.Combine(root, "not-a-directory");
        File.WriteAllText(blocker, "preserve");
        var config = Path.Combine(root, "config.json");
        File.WriteAllText(config, "broken JSON");
        var paths = new ApplicationPaths(root, config, Path.Combine(root, "state.json"), Path.Combine(blocker, "log.txt"), Path.Combine(root, "backups"), Path.Combine(root, "stage"), Path.Combine(root, "managed"));
        var repository = new ConfigRepository(config);
        var resolver = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder());
        var backups = new BackupService(paths.BackupDirectory, "test");
        var links = new Windows.ShellLinks.ShellLinkService();
        var applier = new ManagedShortcutApplier(repository, new(), resolver, links, backups, paths.StagingDirectory, paths.StatePath);
        var logger = new FileLogger(paths.LogPath);
        var vm = new App.MainViewModel(paths, repository, new(), resolver, new(), new(links, new()), new(), backups, applier, new(new(), applier), logger, new Windows.Shell.ExplorerShellReloader());

        Assert.True(vm.RecoveryRequired);
        Assert.False(vm.CanApply);
        Assert.Contains("復旧モード", vm.Status);
        Assert.NotEmpty(vm.LogWarning);
        Assert.Equal("broken JSON", File.ReadAllText(config));
        Assert.Equal("preserve", File.ReadAllText(blocker));
        // Backup storage failure must stop explicit recovery initialization.
        File.WriteAllText(paths.BackupDirectory, "block backup creation");
        Assert.Throws<IOException>(vm.InitializeRecovery);
        Assert.True(vm.RecoveryRequired);
        Assert.Equal("broken JSON", File.ReadAllText(config));
    }

    public void Dispose() => Directory.Delete(root, true);
}
