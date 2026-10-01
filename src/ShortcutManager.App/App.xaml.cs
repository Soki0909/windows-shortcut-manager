using System.Configuration;
using System.Data;
using System.Windows;
using System.Text.Json;
using System.IO;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var paths = ShortcutManager.Windows.Storage.ApplicationPaths.ForCurrentUser();
        var repository = new ShortcutManager.Windows.Storage.ConfigRepository(paths.ConfigPath);
        var validator = new ShortcutManager.Core.Validation.ConfigValidator();
        var shellLinks = new ShortcutManager.Windows.ShellLinks.ShellLinkService();
        var resolver = new ShortcutManager.Windows.Launching.LaunchSpecResolver(new ShortcutManager.Windows.Chrome.ChromeLocator(), new ShortcutManager.Core.Protocol.DispatcherUriBuilder());
        var backups = new ShortcutManager.Windows.Backup.BackupService(paths.BackupDirectory, "0.1.0");
        var applier = new ShortcutManager.Windows.Apply.ManagedShortcutApplier(repository, validator, resolver, shellLinks, backups, paths.StagingDirectory, paths.StatePath, shellNotifier: new ShortcutManager.Windows.Shell.ShellChangeNotifier());
        var scanner = new ShortcutManager.Windows.Inventory.ShortcutInventoryScanner(shellLinks, new ShortcutManager.Windows.Inventory.UrlShortcutReader());
        var logger = new ShortcutManager.Windows.Logging.FileLogger(paths.LogPath);
        var restore = new ShortcutManager.Windows.Backup.RestoreService(repository, validator, backups, applier, paths.StatePath);
        if (TryRunCommandLine(e.Args, paths, repository, validator, scanner, applier, restore, logger))
        {
            Shutdown();
            return;
        }
        var viewModel = new MainViewModel(paths, repository, validator, resolver, new ShortcutManager.Windows.Launching.ProcessLauncher(), scanner, new ShortcutManager.Windows.Inventory.ShortcutImporter(), backups, applier, restore, logger);
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    private static bool TryRunCommandLine(string[] args, ShortcutManager.Windows.Storage.ApplicationPaths paths, ShortcutManager.Windows.Storage.ConfigRepository repository, ShortcutManager.Core.Validation.ConfigValidator validator, ShortcutManager.Windows.Inventory.ShortcutInventoryScanner scanner, ShortcutManager.Windows.Apply.ManagedShortcutApplier applier, ShortcutManager.Windows.Backup.RestoreService restore, ShortcutManager.Windows.Logging.FileLogger logger)
    {
        var resultPath = ValueAfter(args, "--result");
        var validatePath = ValueAfter(args, "--validate-config");
        var applyPath = ValueAfter(args, "--apply-config");
        var restorePath = ValueAfter(args, "--restore-backup");
        var scanRequested = args.Contains("--scan", StringComparer.OrdinalIgnoreCase);
        if (validatePath is null && applyPath is null && restorePath is null && !scanRequested) return false;
        if (string.IsNullOrWhiteSpace(resultPath)) throw new ArgumentException("--result <path> is required in command mode.");

        object result;
        try
        {
            if (restorePath is not null)
            {
                result = restore.Restore(restorePath, paths.ManagedShortcutDirectory);
            }
            else if (scanRequested)
            {
                var items = scanner.ScanDefaultAsync(paths.ManagedShortcutDirectory).GetAwaiter().GetResult();
                result = new { ok = true, count = items.Count, items };
            }
            else
            {
                var source = validatePath ?? applyPath!;
                var config = ConfigJson.Deserialize(File.ReadAllText(source));
                var validation = validator.Validate(config);
                if (validatePath is not null) result = new { ok = validation.IsValid, issues = validation.Issues };
                else if (!validation.IsValid) result = new { ok = false, message = "Validation failed.", issues = validation.Issues };
                else
                {
                    var managed = config.Settings.ManagedShortcutDirectory ?? paths.ManagedShortcutDirectory;
                    var inventory = scanner.ScanDefaultAsync(managed).GetAwaiter().GetResult();
                    var external = inventory.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
                    result = applier.Apply(config, managed, external);
                }
            }
        }
        catch (Exception error)
        {
            logger.Error("Command mode failed", error);
            result = new { ok = false, message = error.Message, type = error.GetType().Name };
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        return true;
    }

    private static string? ValueAfter(string[] args, string key)
    {
        var index = Array.FindIndex(args, value => value.Equals(key, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

