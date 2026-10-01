namespace ShortcutManager.Windows.Storage;

public sealed record ApplicationPaths(string DataDirectory, string ConfigPath, string StatePath, string LogPath, string BackupDirectory, string StagingDirectory, string ManagedShortcutDirectory)
{
    public static ApplicationPaths ForCurrentUser()
    {
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShortcutManager");
        var managed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Shortcut Manager");
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"), Path.Combine(data, "logs", "shortcut-manager.log"), Path.Combine(data, "backups"), Path.Combine(data, "staging"), managed);
    }
}

