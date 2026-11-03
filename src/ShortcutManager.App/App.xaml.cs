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
    private ShortcutManager.Windows.Logging.FileLogger? applicationLogger;
    private string[] startupArgs = [];
    private bool stoppingAfterError;
    private bool guiLaunch;
    private ThemeResources? theme;
    internal void RefreshTheme()=>theme?.QueueRefresh();
    protected override void OnExit(ExitEventArgs e) { theme?.Dispose(); instance?.Dispose(); base.OnExit(e); }

    protected override void OnStartup(StartupEventArgs e)
    {
        startupArgs = e.Args;
        guiLaunch=e.Args.Length==0;
        DispatcherUnhandledException += (_, error) =>
        {
            error.Handled = true;
            StopAfterError(error.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, error) => applicationLogger?.Error("Unhandled process failure", error.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, error) => applicationLogger?.Error("Unobserved task failure", error.Exception);
        try { StartApplication(e); }
        catch (Exception error) { StopAfterError(error); }
    }

    private void StopAfterError(Exception error)
    {
        if (stoppingAfterError) return;
        stoppingAfterError = true;
        applicationLogger?.Error("Application stopped", error);
        var message = $"処理を続行できないため終了します。保存済み設定や復旧用データを削除せず、再起動時に復旧を確認してください。\n{error.Message}";
        var resultPath = ValueAfter(startupArgs, "--result");
        if (resultPath is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
                File.WriteAllText(resultPath, JsonSerializer.Serialize(new { ok = false, message, type = error.GetType().Name }));
            }
            catch (Exception outputError) { applicationLogger?.Error("Could not write command result", outputError); }
        }
        if (guiLaunch)
            MessageBox.Show(message, "Shortcut Manager — 処理を停止しました", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }

    private void StartApplication(StartupEventArgs e)
    {
        base.OnStartup(e);
        theme=new ThemeResources(this);
        BuildIdentity.ConfigureTaskbar();
        ValidateCommandArguments(e.Args);
        guiLaunch=!IsCommandMode(e.Args);
        var paths = ShortcutManager.Windows.Storage.ApplicationPaths.ForCurrentUser(BuildIdentity.IsDevelopment);
        instance = new(paths.ConfigPath);
        if (!instance.Acquired)
        {
            const string message = "この版のShortcut Managerは既に起動しています。開いている画面で操作し、変更を反映または破棄してから終了してください。";
            var resultPath = ValueAfter(e.Args, "--result");
            if (resultPath is not null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!); File.WriteAllText(resultPath, JsonSerializer.Serialize(new { ok = false, message })); }
            else if (guiLaunch) MessageBox.Show(message, "Shortcut Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(1); return;
        }
        var repository = new ShortcutManager.Windows.Storage.ConfigRepository(paths.ConfigPath);
        var validator = new ShortcutManager.Core.Validation.ConfigValidator();
        var shellLinks = new ShortcutManager.Windows.ShellLinks.ShellLinkService();
        var resolver = new ShortcutManager.Windows.Launching.LaunchSpecResolver(new ShortcutManager.Windows.Chrome.ChromeLocator(), new ShortcutManager.Core.Protocol.DispatcherUriBuilder(), BuildIdentity.IsDevelopment ? ValueAfter(e.Args, "--chrome-test-profile") : null);
        var backups = new ShortcutManager.Windows.Backup.BackupService(paths.BackupDirectory, BuildIdentity.Version);
        var applier = new ShortcutManager.Windows.Apply.ManagedShortcutApplier(repository, validator, resolver, shellLinks, backups, paths.StagingDirectory, paths.StatePath, shellNotifier: new ShortcutManager.Windows.Shell.ShellChangeNotifier());
        var scanner = new ShortcutManager.Windows.Inventory.ShortcutInventoryScanner(shellLinks, new ShortcutManager.Windows.Inventory.UrlShortcutReader());
        var logger = new ShortcutManager.Windows.Logging.FileLogger(paths.LogPath);
        applicationLogger = logger;
        var restore = new ShortcutManager.Windows.Backup.RestoreService(validator, applier);
        var guiAuditOnly=e.Args.Contains("--audit-only",StringComparer.OrdinalIgnoreCase) || new ShortcutManager.Windows.Apply.AuditControlStore(paths.DataDirectory).ReadPolicy().AuditOnly;
        var commandMode=IsCommandMode(e.Args);
        if ((!guiAuditOnly || commandMode) && !e.Args.Contains("--scan", StringComparer.OrdinalIgnoreCase) && !e.Args.Contains("--validate-config", StringComparer.OrdinalIgnoreCase) && !e.Args.Contains("--diagnose-launch", StringComparer.OrdinalIgnoreCase))
        {
            var recovery = applier.RecoverPending();
            if (!recovery.Succeeded)
            {
                var resultPath = ValueAfter(e.Args, "--result");
                if (resultPath is not null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!); File.WriteAllText(resultPath, JsonSerializer.Serialize(new { ok = false, message = recovery.Message })); }
                else if (e.Args.Length == 0) MessageBox.Show(recovery.Message, "復旧が必要です", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1); return;
            }
            if (recovery.GeneratedCount > 0 && e.Args.Length == 0) MessageBox.Show(recovery.Message, "中断処理の復旧", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        if (TryRunCommandLine(e.Args, paths, repository, validator, scanner, applier, restore, logger, out var exitCode))
        {
            Shutdown(exitCode);
            return;
        }
        var viewModel = new MainViewModel(paths, repository, validator, resolver, new ShortcutManager.Windows.Launching.ProcessLauncher(), scanner, new ShortcutManager.Windows.Inventory.ShortcutImporter(), backups, applier, restore, logger, new ShortcutManager.Windows.Shell.ExplorerShellReloader());
        var auditor = new ShortcutManager.Windows.Apply.ManagedShortcutAuditor(validator, resolver, shellLinks);
        var window = new MainWindow(viewModel, auditor);
        MainWindow = window;
        window.Loaded += async (_, _) =>
        {
            window.IsEnabled = false;
            bool repaired;
            try { repaired = await viewModel.AuditStartupAsync(auditor,guiAuditOnly); }
            finally { window.IsEnabled = true; }
            if (repaired) await window.OfferShellReloadAsync();
            if (!viewModel.RecoveryRequired && !File.Exists(paths.ConfigPath) && viewModel.Shortcuts.Count == 0) window.ShowGettingStarted();
        };
        window.Show();
    }

    private static bool TryRunCommandLine(string[] args, ShortcutManager.Windows.Storage.ApplicationPaths paths, ShortcutManager.Windows.Storage.ConfigRepository repository, ShortcutManager.Core.Validation.ConfigValidator validator, ShortcutManager.Windows.Inventory.ShortcutInventoryScanner scanner, ShortcutManager.Windows.Apply.ManagedShortcutApplier applier, ShortcutManager.Windows.Backup.RestoreService restore, ShortcutManager.Windows.Logging.FileLogger logger, out int exitCode)
    {
        exitCode = 0;
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
                var report = scanner.ScanReportAsync(ShortcutManager.Windows.Inventory.ShortcutInventoryScanner.DefaultRoots,paths.ManagedShortcutDirectory).GetAwaiter().GetResult();
                result = new { ok = true, complete = report.IsComplete, count = report.Items.Count, items = report.Items, summary = report.Summary, scanIssues = report.Issues, readFailureCount = report.ReadFailureCount, roots = report.Roots };
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
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if ((json.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
            || (json.TryGetProperty("succeeded", out var succeeded) && succeeded.ValueKind == JsonValueKind.False)) exitCode = 1;
        File.WriteAllText(resultPath, json.GetRawText());
        return true;
    }

    private static string? ValueAfter(string[] args, string key)
    {
        var index = Array.FindIndex(args, value => value.Equals(key, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void ValidateCommandArguments(string[] args)
    {
        if (args.Length == 0) return;
        var commands = new[] { "--scan", "--validate-config", "--apply-config", "--restore-backup", "--diagnose-launch" };
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var flag = args[index];
            if (!flags.Add(flag)) throw new ArgumentException($"Duplicate option: {flag}");
            if (flag.Equals("--scan", StringComparison.OrdinalIgnoreCase) || flag.Equals("--diagnose-launch", StringComparison.OrdinalIgnoreCase) || flag.Equals("--audit-only",StringComparison.OrdinalIgnoreCase)) continue;
            if (!new[] { "--validate-config", "--apply-config", "--restore-backup", "--result", "--chrome-test-profile" }.Contains(flag, StringComparer.OrdinalIgnoreCase)
                || (flag.Equals("--chrome-test-profile", StringComparison.OrdinalIgnoreCase) && !BuildIdentity.IsDevelopment)) throw new ArgumentException($"Unknown option: {flag}");
            if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Value required: {flag}");
        }
        var commandCount = commands.Count(flags.Contains);
        if(commandCount>0 && flags.Contains("--audit-only"))throw new ArgumentException("--audit-only はGUI起動専用です。");
        if(commandCount==0 && flags.All(flag=>flag.Equals("--audit-only",StringComparison.OrdinalIgnoreCase) || flag.Equals("--chrome-test-profile",StringComparison.OrdinalIgnoreCase)))return;
        if (commandCount == 0 && flags.Count == 1 && flags.Contains("--chrome-test-profile")) return;
        if (commandCount != 1 || !flags.Contains("--result")) throw new ArgumentException("Specify exactly one command and --result <path>.");
    }
    private static bool IsCommandMode(string[] args)=>args.Any(arg=>new[]{"--scan","--validate-config","--apply-config","--restore-backup","--diagnose-launch"}.Contains(arg,StringComparer.OrdinalIgnoreCase));
}

