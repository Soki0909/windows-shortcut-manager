namespace ShortcutManager.Windows.Storage;

public sealed record ApplicationPaths(string DataDirectory, string ConfigPath, string StatePath, string LogPath, string BackupDirectory, string StagingDirectory, string ManagedShortcutDirectory, bool IsDevelopment = false)
{
    public static ApplicationPaths ForCurrentUser(bool isDevelopment = false)
    {
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), isDevelopment ? "ShortcutManager.Debug" : "ShortcutManager");
        var managed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), isDevelopment ? "Shortcut Manager Debug" : "Shortcut Manager");
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"), Path.Combine(data, "logs", "shortcut-manager.log"), Path.Combine(data, "backups"), Path.Combine(data, "staging"), managed, isDevelopment);
    }

    // A copied production config must never redirect a debug Apply/audit/restore
    // into the production shortcut folder.
    public string ResolveManagedDirectory(string? configuredDirectory) =>
        IsDevelopment ? ManagedShortcutDirectory : configuredDirectory ?? ManagedShortcutDirectory;
}

