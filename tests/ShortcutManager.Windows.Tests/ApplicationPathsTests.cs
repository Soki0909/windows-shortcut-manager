using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void DevelopmentPaths_AreSeparateFromInstalledProduct()
    {
        var product = ApplicationPaths.ForCurrentUser();
        var debug = ApplicationPaths.ForCurrentUser(isDevelopment: true);
        Assert.False(product.IsDevelopment);
        Assert.True(debug.IsDevelopment);
        Assert.Equal("ShortcutManager", Path.GetFileName(product.DataDirectory));
        Assert.Equal("ShortcutManager.Debug", Path.GetFileName(debug.DataDirectory));
        Assert.Equal("Shortcut Manager", Path.GetFileName(product.ManagedShortcutDirectory));
        Assert.Equal("Shortcut Manager Debug", Path.GetFileName(debug.ManagedShortcutDirectory));
        Assert.NotEqual(product.ConfigPath, debug.ConfigPath);
        Assert.NotEqual(product.StatePath, debug.StatePath);
        Assert.NotEqual(product.LogPath, debug.LogPath);
        Assert.NotEqual(product.BackupDirectory, debug.BackupDirectory);
        Assert.NotEqual(product.StagingDirectory, debug.StagingDirectory);
    }

    [Fact]
    public void DevelopmentDirectory_IgnoresProductionPathInCopiedConfig()
    {
        var product = ApplicationPaths.ForCurrentUser();
        var debug = ApplicationPaths.ForCurrentUser(isDevelopment: true);
        Assert.Equal(debug.ManagedShortcutDirectory, debug.ResolveManagedDirectory(product.ManagedShortcutDirectory));
        Assert.Equal(debug.ManagedShortcutDirectory, debug.ResolveManagedDirectory(null));
    }

    [Fact]
    public void ProductDirectory_PreservesConfiguredOverride()
    {
        var product = ApplicationPaths.ForCurrentUser();
        var custom = Path.Combine(Path.GetTempPath(), "custom-managed");
        Assert.Equal(custom, product.ResolveManagedDirectory(custom));
        Assert.Equal(product.ManagedShortcutDirectory, product.ResolveManagedDirectory(null));
    }
}
