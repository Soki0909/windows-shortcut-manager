namespace ShortcutManager.Windows.Tests;

public sealed class LaunchProbeCleanupTests
{
    [Fact]
    public async Task CleanupWaitsForFileToBecomeDeletable()
    {
        var root = Directory.CreateTempSubdirectory("shortcut-manager-launch-").FullName;
        Task cleanup;
        using (File.Open(Path.Combine(root, "locked.exe"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
        {
            cleanup = LaunchProbeCleanup.RemoveAsync(root);
            Assert.False(cleanup.IsCompleted);
        }

        await cleanup;
        Assert.False(Directory.Exists(root));
    }
}
