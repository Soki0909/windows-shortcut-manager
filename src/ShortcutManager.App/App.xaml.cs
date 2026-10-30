using System.Configuration;
using System.Data;
using System.Windows;
using System.Text.Json;
using System.IO;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.App;

public partial class App : Application
{
    private ShortcutManager.Windows.Storage.InstanceLease? instance;
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        BuildIdentity.ConfigureTaskbar();
        var paths = ShortcutManager.Windows.Storage.ApplicationPaths.ForCurrentUser(BuildIdentity.IsDevelopment);
        instance = new(paths.ConfigPath);
        if (!instance.Acquired)
        {
            const string message = "この版のShortcut Managerは既に起動しています。開いている画面で操作し、変更を反映または破棄してから終了してください。";
            var resultPath = ValueAfter(e.Args, "--result");
            if (resultPath is not null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!); File.WriteAllText(resultPath, JsonSerializer.Serialize(new { ok = false, message })); }
            else MessageBox.Show(message, "Shortcut Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        var repository = new ShortcutManager.Windows.Storage.ConfigRepository(paths.ConfigPath);
        var validator = new ShortcutManager.Core.Validation.ConfigValidator();
        var shellLinks = new ShortcutManager.Windows.ShellLinks.ShellLinkService();
        var resolver = new ShortcutManager.Windows.Launching.LaunchSpecResolver(new ShortcutManager.Windows.Chrome.ChromeLocator(), new ShortcutManager.Core.Protocol.DispatcherUriBuilder(), BuildIdentity.IsDevelopment ? ValueAfter(e.Args, "--chrome-test-profile") : null);
        var backups = new ShortcutManager.Windows.Backup.BackupService(paths.BackupDirectory, BuildIdentity.Version);
        var applier = new ShortcutManager.Windows.Apply.ManagedShortcutApplier(repository, validator, resolver, shellLinks, backups, paths.StagingDirectory, paths.StatePath, shellNotifier: new ShortcutManager.Windows.Shell.ShellChangeNotifier());
        var scanner = new ShortcutManager.Windows.Inventory.ShortcutInventoryScanner(shellLinks, new ShortcutManager.Windows.Inventory.UrlShortcutReader());
        var logger = new ShortcutManager.Windows.Logging.FileLogger(paths.LogPath);
        var restore = new ShortcutManager.Windows.Backup.RestoreService(validator, applier);
        if (!e.Args.Contains("--scan") && !e.Args.Contains("--validate-config") && !e.Args.Contains("--diagnose-launch"))
        {
            var recovery = applier.RecoverPending();
            if (!recovery.Succeeded)
            {
                var resultPath = ValueAfter(e.Args, "--result");
                if (resultPath is not null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!); File.WriteAllText(resultPath, JsonSerializer.Serialize(new { ok = false, message = recovery.Message })); }
                else MessageBox.Show(recovery.Message, "復旧が必要です", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(); return;
            }
            if (recovery.GeneratedCount > 0 && e.Args.Length == 0) MessageBox.Show(recovery.Message, "中断処理の復旧", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        if (TryRunCommandLine(e.Args, paths, repository, validator, scanner, applier, restore, logger))
        {
            Shutdown();
            return;
        }
        var viewModel = new MainViewModel(paths, repository, validator, resolver, new ShortcutManager.Windows.Launching.ProcessLauncher(), scanner, new ShortcutManager.Windows.Inventory.ShortcutImporter(), backups, applier, restore, logger, new ShortcutManager.Windows.Shell.ExplorerShellReloader());
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Loaded += async (_, _) =>
        {
            window.IsEnabled = false;
            bool repaired;
            try { repaired = await viewModel.AuditStartupAsync(new ShortcutManager.Windows.Apply.ManagedShortcutAuditor(validator, resolver, shellLinks)); }
            finally { window.IsEnabled = true; }
            if (repaired) await window.OfferShellReloadAsync();
        };
        window.Show();
    }

    private static bool TryRunCommandLine(string[] args, ShortcutManager.Windows.Storage.ApplicationPaths paths, ShortcutManager.Windows.Storage.ConfigRepository repository, ShortcutManager.Core.Validation.ConfigValidator validator, ShortcutManager.Windows.Inventory.ShortcutInventoryScanner scanner, ShortcutManager.Windows.Apply.ManagedShortcutApplier applier, ShortcutManager.Windows.Backup.RestoreService restore, ShortcutManager.Windows.Logging.FileLogger logger)
    {
        var resultPath = ValueAfter(args, "--result");
        var validatePath = ValueAfter(args, "--validate-config");
        var applyPath = ValueAfter(args, "--apply-config");
        var restorePath = ValueAfter(args, "--restore-backup");
        var scanRequested = args.Contains("--scan", StringComparer.OrdinalIgnoreCase);
        var diagnoseRequested = args.Contains("--diagnose-launch", StringComparer.OrdinalIgnoreCase);
        if (validatePath is null && applyPath is null && restorePath is null && !scanRequested && !diagnoseRequested) return false;
        if (string.IsNullOrWhiteSpace(resultPath)) throw new ArgumentException("--result <path> is required in command mode.");

        object result;
        try
        {
            if (diagnoseRequested) result = new ShortcutManager.Windows.Diagnostics.ShellResponsivenessProbe().Run();
            else if (restorePath is not null)
            {
                var inventory = scanner.ScanDefaultAsync(paths.ManagedShortcutDirectory).GetAwaiter().GetResult();
                var external = inventory.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
                result = restore.Restore(restorePath, paths.ManagedShortcutDirectory, external);
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
                    var managed = paths.ResolveManagedDirectory(config.Settings.ManagedShortcutDirectory);
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

