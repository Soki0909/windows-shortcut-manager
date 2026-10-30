using System.Collections.ObjectModel;
using System.IO;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Validation;
using ShortcutManager.Core.Utilities;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Shell;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.App;

public sealed class MainViewModel : ObservableObject
{
    private readonly ApplicationPaths paths;
    private readonly ConfigRepository repository;
    private readonly ConfigValidator validator;
    private readonly LaunchSpecResolver resolver;
    private readonly ProcessLauncher launcher;
    private readonly ShortcutInventoryScanner scanner;
    private readonly ShortcutImporter importer;
    private readonly BackupService backups;
    private readonly ManagedShortcutApplier applier;
    private readonly RestoreService restore;
    private readonly FileLogger logger;
    private readonly IExplorerShellReloader shellReloader;
    private ShortcutConfig candidate = new();
    private ShortcutRow? selected;
    private string status = "準備完了";
    private bool isBusy;
    private bool isDirty;
    private bool configLoaded;
    private bool recoveryRequired;

    public MainViewModel(ApplicationPaths paths, ConfigRepository repository, ConfigValidator validator, LaunchSpecResolver resolver, ProcessLauncher launcher, ShortcutInventoryScanner scanner, ShortcutImporter importer, BackupService backups, ManagedShortcutApplier applier, RestoreService restore, FileLogger logger, IExplorerShellReloader shellReloader)
    {
        this.paths = paths;
        this.repository = repository;
        this.validator = validator;
        this.resolver = resolver;
        this.launcher = launcher;
        this.scanner = scanner;
        this.importer = importer;
        this.backups = backups;
        this.applier = applier;
        this.restore = restore;
        this.logger = logger;
        this.shellReloader = shellReloader;
        Load();
    }

    public ObservableCollection<ShortcutRow> Shortcuts { get; } = [];
    public ObservableCollection<KeyTile> TopRowKeys { get; } = [];
    public ObservableCollection<KeyTile> HomeRowKeys { get; } = [];
    public ObservableCollection<KeyTile> BottomRowKeys { get; } = [];
    public ShortcutRow? Selected { get => selected; set => Set(ref selected, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public bool IsBusy { get => isBusy; private set => Set(ref isBusy, value); }
    public bool IsDirty { get => isDirty; private set => Set(ref isDirty, value); }
    public bool RecoveryRequired { get => recoveryRequired; private set { if (Set(ref recoveryRequired, value)) Raise(nameof(CanApply)); } }
    public bool CanApply => !RecoveryRequired;
    public ShortcutConfig Candidate => candidate;
    public string ConfigPath => paths.ConfigPath;
    public string ManagedDirectory => paths.ResolveManagedDirectory(candidate.Settings.ManagedShortcutDirectory);

    public void Load()
    {
        configLoaded = false;
        try
        {
            candidate = repository.Load();
            var validation = validator.Validate(candidate, checkFileSystem: false);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
            Refresh();
            IsDirty = false;
            RecoveryRequired = false;
            configLoaded = File.Exists(paths.ConfigPath);
            Status = File.Exists(paths.ConfigPath) ? "config.json を読み込みました。" : "初回起動: ショートカットを追加するか既存をスキャンしてください。";
        }
        catch (Exception error)
        {
            RecoveryRequired = true;
            try { if (File.Exists(paths.ConfigPath)) File.Copy(paths.ConfigPath, $"{paths.ConfigPath}.broken-{Guid.NewGuid():N}.json", false); }
            catch (Exception preservationError) { Status = $"破損設定のコピーを保存できません。元ファイルは保全しています: {preservationError.Message}"; }
            candidate = new();
            Refresh();
            Status = $"復旧モード: 反映は停止中です。「復元」でバックアップを選ぶか、config.jsonを修復して「再読込」してください。既存ショートカットは変更していません: {error.Message}";
            logger.Error("Config load failed", error);
        }
    }

    public void InitializeRecovery()
    {
        if (!RecoveryRequired) return;
        // Preserve the broken source in a verified ZIP before allowing an empty candidate.
        backups.Create("recovery-initialize", paths.ConfigPath, paths.ManagedShortcutDirectory, paths.StatePath);
        candidate = new();
        RecoveryRequired = false;
        IsDirty = true;
        Refresh();
        Status = "破損設定をバックアップし、空の候補を準備しました。既存リンクの削除は「変更を反映」まで行いません。";
    }

    public async Task<bool> AuditStartupAsync(ManagedShortcutAuditor auditor)
    {
        if (!configLoaded || IsDirty || IsBusy) return false;
        IsBusy = true;
        Status = "起動時監査: 保存済み設定とショートカットを照合しています…";
        try
        {
            var result = await new StartupShortcutReconciler(auditor, applier).ReconcileAsync(candidate, ManagedDirectory, () => scanner.ScanDefaultAsync(ManagedDirectory));
            logger.Info($"Startup audit: repaired={result.Repaired}, differences={result.Differences}, backup={result.BackupPath}, message={result.Message}");
            Status = result.Message;
            return result.Repaired;
        }
        catch (Exception error)
        {
            Status = $"起動時監査: 自動修復を中止しました。{error.Message}";
            logger.Error("Startup audit failed", error);
            return false;
        }
        finally { IsBusy = false; }
    }

    public void Upsert(ShortcutDefinition definition)
    {
        var index = candidate.Shortcuts.FindIndex(item => item.Id == definition.Id);
        if (index >= 0) candidate.Shortcuts[index] = definition;
        else candidate.Shortcuts.Add(definition);
        IsDirty = true;
        Refresh();
        Status = "未適用の変更があります。";
    }

    public void DeleteSelected()
    {
        if (Selected is null) return;
        candidate.Shortcuts.RemoveAll(item => item.Id == Selected.Definition.Id);
        IsDirty = true;
        Refresh();
        Status = "削除は次回 Apply で反映されます。";
    }

    public void UpdateSettings(string? chromePath, string extensionId)
    {
        candidate = candidate with { Settings = candidate.Settings with { Chrome = new() { ExecutablePath = string.IsNullOrWhiteSpace(chromePath) ? null : chromePath.Trim(), ExtensionId = extensionId.Trim() } } };
        IsDirty = true;
        Refresh();
        Status = "設定を変更しました。Apply で反映してください。";
    }

    public async Task<IReadOnlyList<ShortcutInventoryItem>> ScanAsync()
    {
        IsBusy = true;
        Status = "既存 .lnk / .url をスキャンしています…";
        try
        {
            var results = await scanner.ScanDefaultAsync(ManagedDirectory);
            Status = $"{results.Count} 件を検出しました。";
            logger.Info($"Inventory scan: {results.Count} items");
            return results;
        }
        finally { IsBusy = false; }
    }

    public void Import(ShortcutInventoryItem item)
    {
        var key = ExtractKey(item.Hotkey);
        if (key is null || candidate.Shortcuts.Any(existing => existing.Enabled && existing.Hotkey.Key == key))
            key = Enumerable.Range('A', 26).Select(value => ((char)value).ToString()).FirstOrDefault(value => candidate.Shortcuts.All(existing => !existing.Enabled || existing.Hotkey.Key != value));
        if (key is null) { Status = "空きキーがありません。"; return; }
        Upsert(importer.CreateProposal(item, key));
        Status = $"{item.DisplayName} を {key} にインポートしました。元ファイルは変更していません。";
    }

    public async Task<bool> ApplyAsync()
    {
        if (RecoveryRequired) { Status = "復旧モードでは反映できません。バックアップ復元または設定修復後の再読込が必要です。"; return false; }
        IsBusy = true;
        Status = "validate → scan → stage → backup → apply を実行しています…";
        try
        {
            var scan = await scanner.ScanDefaultAsync(ManagedDirectory);
            var external = scan.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
            var result = await Task.Run(() => applier.Apply(candidate, ManagedDirectory, external));
            Status = result.Message;
            logger.Info($"Apply: success={result.Succeeded}, generated={result.GeneratedCount}, backup={result.BackupPath}");
            if (result.Succeeded) IsDirty = false;
            Refresh();
            return result.Succeeded;
        }
        catch (Exception error)
        {
            Status = $"Apply に失敗しました: {error.Message}";
            logger.Error("Apply failed", error);
            return false;
        }
        finally { IsBusy = false; }
    }

    public async Task ReloadShellAsync()
    {
        IsBusy = true;
        Status = "Windows Shellを再読み込みしています…";
        logger.Info("Shell reload requested by user.");
        try
        {
            var result = await shellReloader.ReloadAsync();
            Status = result.Message;
            logger.Info($"Shell reload completed: success={result.Succeeded}, previousPid={result.PreviousProcessId?.ToString() ?? "unknown"}, currentPid={result.CurrentProcessId?.ToString() ?? "unknown"}, message={result.Message}");
        }
        catch (Exception error)
        {
            Status = $"Windows Shellの再読み込みに失敗しました: {error.Message}";
            logger.Error("Shell reload failed", error);
        }
        finally { IsBusy = false; }
    }

    public void Test(ShortcutDefinition definition)
    {
        try
        {
            var result = validator.Validate(candidate);
            if (result.Issues.Any(issue => issue.ShortcutId == definition.Id))
            {
                Status = string.Join(" / ", result.Issues.Where(issue => issue.ShortcutId == definition.Id).Select(issue => issue.Message));
                return;
            }
            launcher.Launch(resolver.Resolve(candidate, definition));
            Status = $"{definition.Name} をテスト起動しました。";
            logger.Info($"Test launch: {definition.Name}");
        }
        catch (Exception error)
        {
            Status = $"テスト起動に失敗しました: {error.Message}";
            logger.Error("Test launch failed", error);
        }
    }

    public string Backup(string? destination = null)
    {
        var path = backups.Create("manual", paths.ConfigPath, ManagedDirectory, paths.StatePath, destination);
        Status = $"バックアップを作成しました: {path}";
        logger.Info($"Manual backup: {path}");
        return path;
    }

    public async Task RestoreAsync(string path)
    {
        IsBusy = true;
        Status = "バックアップを検証して復元しています…";
        try
        {
            var inventory = await scanner.ScanDefaultAsync(ManagedDirectory);
            var external = inventory.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
            var result = await Task.Run(() => restore.Restore(path, ManagedDirectory, external));
            Status = result.Message;
            logger.Info($"Restore: success={result.Succeeded}, source={path}");
            if (result.Succeeded) Load();
        }
        catch (Exception error)
        {
            Status = $"復元に失敗しました: {error.Message}";
            logger.Error("Restore failed", error);
        }
        finally { IsBusy = false; }
    }

    private void Refresh()
    {
        var validation = validator.Validate(candidate, checkFileSystem: true);
        Shortcuts.Clear();
        foreach (var definition in candidate.Shortcuts.OrderBy(item => item.Hotkey.Key))
        {
            var issues = validation.Issues.Where(issue => issue.ShortcutId == definition.Id).ToArray();
            Shortcuts.Add(new(definition, TargetSummary(definition.Target), !definition.Enabled ? "Disabled" : issues.Length > 0 ? "Error" : IsDirty ? "Unapplied Changes" : "Ready", string.Join(" / ", issues.Select(issue => issue.Message))));
        }
        var tiles = KeyboardLayout.Keys.ToDictionary(
            key => key,
            key =>
            {
                var row = Shortcuts.FirstOrDefault(item => item.Key == key);
                return new KeyTile(key, row?.Name ?? "空き", row?.Status ?? "Empty", row);
            });
        Replace(TopRowKeys, KeyboardLayout.Rows[0].Select(key => tiles[key.ToString()]));
        Replace(HomeRowKeys, KeyboardLayout.Rows[1].Select(key => tiles[key.ToString()]));
        Replace(BottomRowKeys, KeyboardLayout.Rows[2].Select(key => tiles[key.ToString()]));
        Raise(nameof(ManagedDirectory));
    }

    private static void Replace(ObservableCollection<KeyTile> target, IEnumerable<KeyTile> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static string? ExtractKey(string hotkey)
    {
        var parts = hotkey.Split('+');
        var key = parts.LastOrDefault();
        return parts.Contains("CTRL") && parts.Contains("ALT") && key is { Length: 1 } && key[0] is >= 'A' and <= 'Z' ? key : null;
    }

    private static string TargetSummary(ShortcutTarget target) => target switch
    {
        WebsiteTarget item => item.Url,
        ApplicationTarget item => item.Value,
        BatTarget item => item.Path,
        ChromeTabGroupTarget item => item.GroupName,
        _ => string.Empty,
    };
}

public sealed record ShortcutRow(ShortcutDefinition Definition, string Target, string Status, string Error)
{
    public bool Enabled => Definition.Enabled;
    public string Key => Definition.Hotkey.Key;
    public string Hotkey => Definition.Hotkey.Canonical;
    public string Name => Definition.Name;
    public string Type => Definition.Target switch { WebsiteTarget => "Website", ApplicationTarget => "Application", BatTarget => "BAT/CMD", ChromeTabGroupTarget => "Chrome Group", _ => "Unknown" };
}

public sealed record KeyTile(string Key, string Name, string Status, ShortcutRow? Row)
{
    public string StatusLabel => Status switch
    {
        "Ready" => "使用中",
        "Disabled" => "無効",
        "Error" => "要確認",
        "Unapplied Changes" => "未反映",
        _ => "空き",
    };
}
