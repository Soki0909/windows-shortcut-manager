using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ShortcutManager.Core.Serialization;
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
    private bool incompatibleConfig;
    private bool shellReflectionPending;
    private string restoreSummary = string.Empty;
    private string savedJson = ConfigJson.Serialize(new());
    private Dictionary<Guid, string> savedEntries = [];
    private string savedSettings = JsonSerializer.Serialize(new ShortcutSettings(), ConfigJson.Options);
    private readonly Dictionary<Guid, string> physicalConfirmations = [];
    private bool explicitRecoveryInitialization;
    private bool savedConfigChangedExternally;
    private bool savedLinksVerified;
    private bool shellReloadCompleted;
    private string auditNotice = "保存リンク: 未監査。物理キーの状態は別に確認してください。";
    private string? lastApplyBackupPath;
    private string? lastAppliedSnapshot;
    private string? lastApplyBackupHash;
    private readonly AuditControlStore auditControls;
    private bool auditOnly;
    private string? auditPolicyWarning;

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
        logger.FailureChanged += () => Raise(nameof(LogWarning));
        this.shellReloader = shellReloader;
        auditControls=new(paths.DataDirectory);
        var policy=auditControls.ReadPolicy();auditOnly=policy.AuditOnly;auditPolicyWarning=policy.Warning;
        Load();
    }

    public ObservableCollection<ShortcutRow> Shortcuts { get; } = [];
    public ObservableCollection<KeyTile> TopRowKeys { get; } = [];
    public ObservableCollection<KeyTile> HomeRowKeys { get; } = [];
    public ObservableCollection<KeyTile> BottomRowKeys { get; } = [];
    public ShortcutRow? Selected
    {
        get => selected;
        set
        {
            if (ReferenceEquals(selected, value)) return;
            selected = value;
            Raise();
            foreach (var tile in TopRowKeys.Concat(HomeRowKeys).Concat(BottomRowKeys)) tile.IsSelected = value is not null && tile.Key == value.Key;
            RaiseActionState();
        }
    }
    public string Status { get => status; private set => Set(ref status, value); }
    public string LogWarning => logger.LastFailure ?? string.Empty;
    public bool ShellReflectionPending { get => shellReflectionPending; private set { if (Set(ref shellReflectionPending, value)) { Raise(nameof(RestoreNotice)); RaiseActionState(); } } }
    public string RestoreNotice => restoreSummary + (ShellReflectionPending ? "\n保存済みのHotKeyはExplorerへの反映が未確認です。実キーが反応しない場合に「Explorerを再起動」を試してください。" : string.Empty);
    public bool IsBusy { get => isBusy; private set { if (Set(ref isBusy, value)) { Raise(nameof(CanInteract)); Raise(nameof(CanInitializeRecovery)); RaiseActionState(); } } }
    public bool CanInteract => !IsBusy;
    public bool IsDirty { get => isDirty; private set { if (Set(ref isDirty, value)) RaiseActionState(); } }
    public bool RecoveryRequired { get => recoveryRequired; private set { if (Set(ref recoveryRequired, value)) { RaiseActionState(); Raise(nameof(CanInitializeRecovery)); Raise(nameof(ShowEmptyState)); } } }
    public bool CanApply => !RecoveryRequired && !IsBusy && IsDirty && !savedConfigChangedExternally;
    public bool CanModify => !RecoveryRequired && !IsBusy;
    public bool CanRestore => !IsBusy && !incompatibleConfig;
    public bool CanUndoLastApply => CanRestore && !IsDirty && !savedConfigChangedExternally && lastApplyBackupPath is not null && lastAppliedSnapshot==savedJson && File.Exists(lastApplyBackupPath);
    public string? LastApplyBackupPath => CanUndoLastApply ? lastApplyBackupPath : null;
    public bool CanEditSelected => !RecoveryRequired && !IsBusy && Selected is not null;
    public bool CanConfirmHotkey => CanEditSelected && !IsDirty && !savedConfigChangedExternally && savedLinksVerified && Selected!.Enabled;
    public bool CanAuditSaved => !IsBusy && configLoaded;
    public bool CanRepairSaved => CanAuditSaved && !incompatibleConfig && !savedConfigChangedExternally;
    public bool AuditOnly
    {
        get=>auditOnly;
        set
        {
            if(IsBusy || value==auditOnly)return;
            try {auditControls.SavePolicy(value);auditPolicyWarning=null;Set(ref auditOnly,value);Status=value?"次回起動から監査のみ行い、自動修復を保留します。明示した反映・修復は実行できます。":"次回起動から差分の自動修復を有効にします。同じ差分の再試行や未確認の走査は保留します。";}
            catch(Exception error){Status="監査方針を保存できません: "+error.Message;logger.Error("Could not save audit policy",error);Raise();}
        }
    }
    public Task<IReadOnlyList<AuditHistoryItem>> ReadAuditHistoryAsync()=>Task.Run(()=>auditControls.ReadHistory());
    public IReadOnlyList<AuditDifference> LastAuditDetails {get;private set;}=[];
    public int PendingChangeCount { get; private set; }
    public string ReloadLabel => IsDirty ? "編集を破棄して再読込" : "保存済みを再読込";
    public string SelectionText => Selected is null ? "一覧またはキー枠から対象を選んでください。" : $"選択: {Selected.Hotkey} → {Selected.Name}";
    public string EditingNotice => RecoveryRequired ? "復旧モード: 変更の反映は停止しています。" : savedConfigChangedExternally ? "保存設定が外部で変わりました。候補を保全してから再読込してください。反映は停止中です。" : IsDirty ? $"未保存: {PendingChangeCount} 項目。まだWindowsには反映されていません。" : configLoaded ? "保存済み設定を表示中です。保存だけで実キー確認済みとは判断しません。" : "保存済み設定はありません。最初の候補を追加してください。";
    public string AuditNotice => auditNotice;
    public string ShellNotice => ShellReflectionPending ? "Explorer反映: 未確認（再起動を見送った場合や失敗した場合も保持）。" : shellReloadCompleted ? "Explorerは再起動済み。物理キー動作は別に確認してください。" : "Explorerの有効キー一覧は取得できません。物理キー動作は別に確認してください。";
    public string PhysicalConfirmationNotice => $"今回の起動中に利用者が実キー確認済みと申告した割当: {candidate.Shortcuts.Count(item => physicalConfirmations.TryGetValue(item.Id, out var proof) && proof == DefinitionVersion(item))} 件。保存・対象テスト・Explorer再起動からは推定しません。";
    public bool CanInitializeRecovery => RecoveryRequired && !incompatibleConfig && !IsBusy;
    public ShortcutConfig Candidate => candidate;
    public string ConfigPath => paths.ConfigPath;
    public string ManagedDirectory => paths.ResolveManagedDirectory(candidate.Settings.ManagedShortcutDirectory);
    public bool ShowEmptyState => Shortcuts.Count == 0 && !RecoveryRequired;

    public void Load()
    {
        if (IsBusy) return;
        configLoaded = false;
        incompatibleConfig = false;
        explicitRecoveryInitialization = false;
        savedConfigChangedExternally = false;
        savedLinksVerified = false;
        auditNotice = "保存リンク: 再読込後は未監査です。編集の破棄とリンク監査は別の操作です。";
        try
        {
            candidate = repository.Load();
            var validation = validator.Validate(candidate, checkFileSystem: false);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
            CaptureSaved();
            configLoaded = File.Exists(paths.ConfigPath);
            Refresh();
            IsDirty = false;
            RecoveryRequired = false;
            Status = File.Exists(paths.ConfigPath) ? "保存済み設定を読み込みました。" : "初回起動: ショートカットを追加するか既存をスキャンしてください。";
        }
        catch (Exception error)
        {
            incompatibleConfig = error is Core.Serialization.UnsupportedConfigVersionException;
            RecoveryRequired = true;
            try { if (!incompatibleConfig && File.Exists(paths.ConfigPath)) File.Copy(paths.ConfigPath, $"{paths.ConfigPath}.broken-{Guid.NewGuid():N}.json", false); }
            catch (Exception preservationError) { Status = $"破損設定のコピーを保存できません。元ファイルは保全しています: {preservationError.Message}"; }
            candidate = new();
            CaptureSaved();
            Refresh();
            Status = $"復旧モード: 反映は停止中です。「復元」でバックアップを選ぶか、config.jsonを修復して「再読込」してください。既存ショートカットは変更していません: {error.Message}";
            if (incompatibleConfig) Status = error.Message;
            logger.Error("Config load failed", error);
        }
        Raise(nameof(CanInitializeRecovery));
        RaiseActionState();
    }

    public void InitializeRecovery()
    {
        if (IsBusy) return;
        if (!RecoveryRequired) return;
        repository.EnsureCompatibleVersion();
        // Preserve the broken source in a verified ZIP before allowing an empty candidate.
        backups.Create("recovery-initialize", paths.ConfigPath, paths.ManagedShortcutDirectory, paths.StatePath);
        candidate = new();
        RecoveryRequired = false;
        explicitRecoveryInitialization = true;
        Refresh();
        Status = "破損設定をバックアップし、空の候補を準備しました。既存リンクの削除は「変更を反映」まで行いません。";
    }

    public async Task<bool> AuditStartupAsync(ManagedShortcutAuditor auditor,bool auditOnlyOverride=false,bool explicitRepair=false)
    {
        if (!configLoaded || !explicitRepair && IsDirty || IsBusy) return false;
        if(explicitRepair && !CheckSavedSourceUnchanged())return false;
        IsBusy = true;
        Status = "起動時監査: 保存済み設定とショートカットを照合しています…";
        try
        {
            var saved=explicitRepair?repository.Load():candidate;
            var directory=paths.ResolveManagedDirectory(saved.Settings.ManagedShortcutDirectory);
            async Task<IReadOnlyList<ShortcutInventoryItem>> Scan() {LastScanReport=await scanner.ScanReportAsync(ShortcutInventoryScanner.DefaultRoots.Concat(LastScanReport?.Roots ?? []),directory);return LastScanReport.Items;}
            var result = await new StartupShortcutReconciler(auditor, applier).ReconcileAsync(saved,directory,Scan,AuditOnly || auditOnlyOverride,explicitRepair,auditControls,()=>LastScanReport?.IsComplete==true,()=>ConfigJson.Serialize(repository.Load())==ConfigJson.Serialize(saved));
            logger.Info($"Startup audit: repaired={result.Repaired}, differences={result.Differences}, backup={result.BackupPath}, message={result.Message}");
            Status = result.Message+(auditPolicyWarning is null?string.Empty:" "+auditPolicyWarning);
            savedLinksVerified = result.LinksMatch;
            LastAuditDetails=result.Repaired?result.After:result.Before;
            if(result.SourceChanged)savedConfigChangedExternally=true;
            auditNotice = savedLinksVerified ? "保存リンク: 設定と一致しています。実キー動作は未確認です。" : "保存リンク: 不一致が残っています。";
            if (result.Repaired) { physicalConfirmations.Clear(); ShellReflectionPending = true; }
            return result.Repaired;
        }
        catch (Exception error)
        {
            savedLinksVerified = false;
            auditNotice = "保存リンク: 起動時監査が完了していません。";
            Status = $"起動時監査: 自動修復を中止しました。{error.Message}";
            logger.Error("Startup audit failed", error);
            return false;
        }
        finally { IsBusy = false; Refresh(); }
    }

    public async Task RecoverPendingAsync()
    {
        if(IsBusy || IsDirty)return;
        IsBusy=true;
        try {var result=await Task.Run(()=>applier.RecoverPending());if(result.Succeeded){IsBusy=false;Load();if(result.GeneratedCount>0){physicalConfirmations.Clear();ShellReflectionPending=true;}}Status=result.Message;}
        catch(Exception error){Status="中断処理の復旧を停止しました: "+error.Message;logger.Error("Explicit pending recovery failed",error);}
        finally {IsBusy=false;Refresh();}
    }

    public void Upsert(ShortcutDefinition definition)
    {
        if (!CanModify) return;
        var index = candidate.Shortcuts.FindIndex(item => item.Id == definition.Id);
        if (index >= 0) candidate.Shortcuts[index] = definition;
        else candidate.Shortcuts.Add(definition);
        Refresh();
        Status = IsDirty ? "未保存の変更があります。" : "保存済みとの差分はありません。";
    }

    public void DeleteSelected()
    {
        if (!CanModify) return;
        if (Selected is null) return;
        candidate.Shortcuts.RemoveAll(item => item.Id == Selected.Definition.Id);
        Refresh();
        Status = IsDirty ? "削除は次回「変更を反映」で保存されます。" : "候補を元に戻したため、保存済みとの差分はありません。";
    }

    public void UpdateSettings(string? chromePath, string extensionId)
    {
        if (!CanModify) return;
        candidate = candidate with { Settings = candidate.Settings with { Chrome = new() { ExecutablePath = string.IsNullOrWhiteSpace(chromePath) ? null : chromePath.Trim(), ExtensionId = extensionId.Trim() } } };
        Refresh();
        Status = IsDirty ? "設定を変更しました。「変更を反映」で保存してください。" : "設定の差分はありません。";
    }

    public InventoryScanReport? LastScanReport { get; private set; }
    public IReadOnlyList<ShortcutInventoryItem> ConflictingItems {get;private set;}=[];
    public async Task<IReadOnlyList<ShortcutInventoryItem>> ScanAsync(IEnumerable<string>? roots = null)
    {
        if (IsBusy) return [];
        IsBusy = true;
        Status = "既存 .lnk / .url をスキャンしています…";
        try
        {
            LastScanReport = await scanner.ScanReportAsync(roots ?? ShortcutInventoryScanner.DefaultRoots,ManagedDirectory);
            Status = LastScanReport.Summary;
            logger.Info($"Inventory scan: {LastScanReport.Items.Count} items, {LastScanReport.Issues.Count} excluded ranges");
            return LastScanReport.Items;
        }
        finally { IsBusy = false; }
    }

    public bool Import(ShortcutInventoryItem item, string? key = null)
    {
        if (!CanModify) return false;
        if (candidate.Shortcuts.Count>=26) {Status="割当は最大26件です。不要な候補を整理してから移行してください。";return false;}
        if (!ImportKeyChoices.CanImport(item)) { Status="この項目は移行できません。読取り結果と元ファイルを確認してください。";return false; }
        var inventory=(LastScanReport?.Items ?? []).Append(item).DistinctBy(entry=>entry.SourcePath,StringComparer.OrdinalIgnoreCase).ToArray();
        if (key is null || !ImportKeyChoices.Build(candidate,inventory).Any(choice=>choice.Key==key && choice.Available)) { Status = "元キーの外部競合を避け、移行先の文字キーを明示的に選んでください。"; return false; }
        Upsert(importer.CreateProposal(item, key));
        Status = $"{item.DisplayName} を {key} にインポートしました。元ファイルは変更していません。";
        return true;
    }

    public async Task<bool> ApplyAsync()
    {
        if (IsBusy) return false;
        ConflictingItems=[];
        if (RecoveryRequired) { Status = "復旧モードでは反映できません。バックアップ復元または設定修復後の再読込が必要です。"; return false; }
        if (!IsDirty) { Status = "保存済みとの差分はありません。反映は行いませんでした。"; return false; }
        if (!explicitRecoveryInitialization && !CheckSavedSourceUnchanged()) return false;
        IsBusy = true;
        Status = "入力と既存キーを確認し、自動バックアップして変更を反映しています…";
        try
        {
            LastScanReport = await scanner.ScanReportAsync(ShortcutInventoryScanner.DefaultRoots.Concat(LastScanReport?.Roots ?? []),ManagedDirectory);
            var scan = LastScanReport.Items;
            ConflictingItems=scan.Where(item=>!item.IsManaged && candidate.Shortcuts.Any(entry=>entry.Enabled && entry.Hotkey.Canonical.Equals(item.Hotkey,StringComparison.OrdinalIgnoreCase))).ToArray();
            var external = scan.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
            var result = await Task.Run(() => applier.Apply(candidate, ManagedDirectory, external));
            Status = result.Message + (LastScanReport.IsComplete ? string.Empty : "\n"+LastScanReport.Summary);
            logger.Info($"Apply: success={result.Succeeded}, generated={result.GeneratedCount}, backup={result.BackupPath}");
            if (result.Succeeded)
            {
                CaptureSaved();
                lastApplyBackupPath=result.BackupPath;
                lastAppliedSnapshot=savedJson;
                try {lastApplyBackupHash=lastApplyBackupPath is null?null:Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(lastApplyBackupPath)));}
                catch(Exception error) {lastApplyBackupPath=null;lastApplyBackupHash=null;logger.Error("Could not associate undo backup",error);}
                configLoaded = true;
                explicitRecoveryInitialization = false;
                physicalConfirmations.Clear();
                savedLinksVerified = true;
                auditNotice = "保存リンク: 反映後の読戻しで一致を確認しました。実キー動作は未確認です。";
                shellReloadCompleted = false;
                restoreSummary = string.Empty;
                ShellReflectionPending = true;
                Raise(nameof(RestoreNotice));
            }
            Refresh();
            return result.Succeeded;
        }
        catch (Exception error)
        {
            Status = $"変更の反映に失敗しました: {error.Message}";
            logger.Error("Apply failed", error);
            return false;
        }
        finally { IsBusy = false; }
    }

    public async Task ReloadShellAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Explorer（デスクトップとタスクバー）を再起動しています…";
        logger.Info("Shell reload requested by user.");
        try
        {
            var result = await shellReloader.ReloadAsync();
            Status = result.Message;
            if (result.Succeeded) { ShellReflectionPending = false; shellReloadCompleted = true; }
            logger.Info($"Shell reload completed: success={result.Succeeded}, previousPid={result.PreviousProcessId?.ToString() ?? "unknown"}, currentPid={result.CurrentProcessId?.ToString() ?? "unknown"}, message={result.Message}");
        }
        catch (Exception error)
        {
            Status = $"Explorerの再起動に失敗しました: {error.Message}";
            logger.Error("Shell reload failed", error);
        }
        finally { IsBusy = false; Refresh(); }
    }

    public void Test(ShortcutDefinition definition)
    {
        var spec=PrepareLaunch(definition);
        if(spec is not null) ExecutePreview(definition,spec);
    }

    public LaunchSpec? PrepareLaunch(ShortcutDefinition definition)
    {
        if (!CanModify) return null;
        try
        {
            var currentDefinition=candidate.Shortcuts.FirstOrDefault(item=>item.Id==definition.Id);
            if(currentDefinition is null || JsonSerializer.Serialize(currentDefinition,ConfigJson.Options)!=JsonSerializer.Serialize(definition,ConfigJson.Options))
            {
                Status="割当が変わったため起動を停止しました。対象を選び直してください。";
                return null;
            }
            // Target tests do not register a key. Validate the selected target as enabled,
            // without letting another draft's key conflict prevent a read-only preview.
            var result = validator.Validate(candidate with { Shortcuts=[definition with { Enabled=true }] });
            if (!result.IsValid)
            {
                Status = string.Join(" / ", result.Issues.Select(issue => issue.Message));
                return null;
            }
            return resolver.Resolve(candidate, definition);
        }
        catch(Exception error) { Status=$"起動内容を確認できません: {error.Message}"; logger.Error("Launch preview failed",error); return null; }
    }

    public bool ExecutePreview(ShortcutDefinition definition, LaunchSpec preview)
    {
        if(!CanModify) return false;
        try
        {
            var current=PrepareLaunch(definition);
            if(current is null) return false;
            if(current!=preview) { Status="起動内容が変わったため実行を中止しました。プレビューを開き直してください。"; return false; }
            launcher.Launch(preview);
            Status = $"{definition.Name} をテスト起動しました。";
            logger.Info($"Test launch: {definition.Name}");
            return true;
        }
        catch (Exception error)
        {
            Status = $"テスト起動に失敗しました: {error.Message}";
            logger.Error("Test launch failed", error);
            return false;
        }
    }

    public string Backup(string? destination = null)
    {
        if (IsBusy) throw new InvalidOperationException("処理中はバックアップできません。");
        var path = backups.Create("manual", paths.ConfigPath, ManagedDirectory, paths.StatePath, destination);
        Status = $"バックアップを作成しました: {path}";
        logger.Info($"Manual backup: {path}");
        return path;
    }

    public string PreviewRestore(string path) => restore.Preview(path);

    public Task<IReadOnlyList<BackupHistoryEntry>> ReadBackupHistoryAsync() => Task.Run(()=>BackupHistory.Read(paths.BackupDirectory));
    public async Task<PreparedRestore> PrepareUndoRestoreAsync()
    {
        if(!CanUndoLastApply || !CheckSavedSourceUnchanged())throw new InvalidOperationException("直前の反映との対応を確認できません。保存済み設定と履歴を確認してください。");
        var prepared=await PrepareRestoreAsync(lastApplyBackupPath!);
        if(prepared.Preview.ArchiveSha256!=lastApplyBackupHash)throw new InvalidDataException("直前の反映に対応するバックアップが変わりました。自動では戻しません。");
        return prepared;
    }
    public async Task<PreparedRestore> PrepareRestoreAsync(string path,RestoreMode mode=RestoreMode.Entire)
    {
        if(!CanRestore)throw new InvalidOperationException("現在は復元を確認できません。");
        IsBusy=true;
        try
        {
            repository.EnsureCompatibleVersion();
            var before=CurrentSourceHash();ShortcutConfig? saved;
            try {saved=repository.Load();} catch(System.Text.Json.JsonException){saved=null;}
            LastScanReport=await scanner.ScanReportAsync(ShortcutInventoryScanner.DefaultRoots.Concat(LastScanReport?.Roots ?? []),ManagedDirectory);
            var external=LastScanReport.Items.Where(item=>!item.IsManaged).Select(item=>item.Hotkey).ToArray();
            var preview=await Task.Run(()=>restore.Prepare(path,saved,external,mode,ManagedDirectory));
            if(before!=CurrentSourceHash())throw new InvalidOperationException("確認中に保存設定が変わりました。プレビューを開き直してください。");
            return new(Path.GetFullPath(path),before,mode,preview);
        }
        finally {IsBusy=false;}
    }

    private string CurrentSourceHash()
    {
        if(!File.Exists(repository.Path))return "absent";
        if(new FileInfo(repository.Path).Length>ConfigStructure.MaxJsonCharacters)throw new InvalidDataException("現在の設定がサイズ上限を超えています。原本を保全して確認してください。");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(repository.Path)));
    }

    public async Task AuditSavedAsync(ManagedShortcutAuditor auditor)
    {
        if (!CanAuditSaved) return;
        IsBusy = true;
        savedLinksVerified = false;
        Status = "保存済みの設定とリンクを監査しています。編集中の候補は変更しません…";
        try
        {
            var result = await Task.Run(() =>
            {
                var saved = repository.Load();
                return (Config: saved, Audit: auditor.Audit(saved, paths.ResolveManagedDirectory(saved.Settings.ManagedShortcutDirectory)));
            });
            LastScanReport=await scanner.ScanReportAsync(ShortcutInventoryScanner.DefaultRoots.Concat(LastScanReport?.Roots ?? []),paths.ResolveManagedDirectory(result.Config.Settings.ManagedShortcutDirectory));
            var inventory = LastScanReport.Items;
            LastAuditDetails=result.Audit.Details;
            ConflictingItems=inventory.Where(item=>!item.IsManaged && result.Config.Shortcuts.Any(entry=>entry.Enabled && entry.Hotkey.Canonical.Equals(item.Hotkey,StringComparison.OrdinalIgnoreCase))).ToArray();
            var external = inventory.Where(item => !item.IsManaged).Select(item => item.Hotkey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var conflicts = result.Config.Shortcuts.Count(item => item.Enabled && external.Contains(item.Hotkey.Canonical));
            savedLinksVerified = result.Audit.Matches;
            savedConfigChangedExternally = ConfigJson.Serialize(result.Config) != savedJson;
            if (!savedLinksVerified || conflicts > 0 || savedConfigChangedExternally) physicalConfirmations.Clear();
            auditNotice = $"保存済みのみ監査: リンク差分 {result.Audit.Differences.Count} 件 / 外部キー競合 {conflicts} 件。編集中の候補は未反映です。";
            try {auditControls.Record(new(1,DateTimeOffset.Now,"保存済みの手動監査","監査のみ",result.Audit.Details,result.Audit.Details,null,auditNotice));}
            catch(Exception error) {logger.Error("Could not save manual audit history",error);auditNotice+=" 履歴を保存できません。";}
            Status = auditNotice + (savedConfigChangedExternally ? " 保存設定が外部で変わりました。候補を保全してから再読込してください。" : string.Empty);
            logger.Info($"Manual persisted audit: differences={result.Audit.Differences.Count}, conflicts={conflicts}, editedCandidatePreserved=True");
        }
        catch (Exception error)
        {
            auditNotice = "保存リンク: 監査できませんでした。候補と保存データは変更していません。";
            Status = auditNotice + error.Message;
            logger.Error("Manual persisted audit failed", error);
        }
        finally { IsBusy = false; Refresh(); }
    }

    // Called only after an explicit user declaration, never after Test/Apply/reload.
    public void ConfirmSelectedHotkey()
    {
        if (!CanConfirmHotkey) return;
        physicalConfirmations[Selected!.Definition.Id] = DefinitionVersion(Selected.Definition);
        Status = $"{Selected.Hotkey} の実キー動作を、今回の起動中に利用者が確認したと記録しました。";
        Refresh();
    }

    public bool SelectKey(string key)
    {
        if (!CanModify) return false;
        var matches = Shortcuts.Where(row => row.Key == key).ToArray();
        if (matches.Length > 1)
        {
            if (Selected?.Key == key) return true;
            Status = $"{key} は {matches.Length} 件の割当があります。一覧で編集する対象を選んでください。";
            return false;
        }
        Selected = matches.SingleOrDefault();
        return true;
    }

    public async Task<bool> RestoreAsync(string path,PreparedRestore? prepared=null)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Status = "バックアップを検証して復元しています…";
        try
        {
            if(prepared is not null && (Path.GetFullPath(path)!=prepared.BackupPath || CurrentSourceHash()!=prepared.CurrentConfigSha256)) {Status="確認後に現在の設定が変わりました。復元せず、プレビューを開き直してください。";return false;}
            LastScanReport=await scanner.ScanReportAsync(ShortcutInventoryScanner.DefaultRoots.Concat(LastScanReport?.Roots ?? []),ManagedDirectory);
            var inventory = LastScanReport.Items;
            var external = inventory.Where(item => !item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item => item.Hotkey).ToArray();
            var result = await Task.Run(() => prepared is not null && CurrentSourceHash()!=prepared.CurrentConfigSha256 ? new ApplyResult(false,"確認後に現在の設定が変わりました。プレビューを開き直してください。") : restore.Restore(path, ManagedDirectory, external,prepared?.Mode ?? RestoreMode.Entire,prepared?.Preview.ArchiveSha256,prepared is null?null:ConfigJson.Serialize(prepared.Preview.Config)));
            Status = result.Message;
            logger.Info($"Restore: success={result.Succeeded}, source={path}");
            if (result.Succeeded)
            {
                IsBusy = false;
                Load();
                lastApplyBackupPath=null;lastAppliedSnapshot=null;lastApplyBackupHash=null;
                physicalConfirmations.Clear();
                savedLinksVerified = !RecoveryRequired;
                auditNotice = "保存リンク: 復元後の読戻しで一致を確認しました。実キー動作は未確認です。";
                shellReloadCompleted = false;
                ShellReflectionPending = true;
                restoreSummary = $"復元しました: {result.GeneratedCount} 件。復元前バックアップ: {result.BackupPath}";
                Raise(nameof(RestoreNotice));
                if (RecoveryRequired) { Status = restoreSummary + "\n復元後の読込に失敗しました。" + Status; return false; }
                Status = "バックアップの復元が完了しました。";
            }
            return result.Succeeded;
        }
        catch (Exception error)
        {
            Status = $"復元に失敗しました: {error.Message}";
            logger.Error("Restore failed", error);
            return false;
        }
        finally { IsBusy = false; Refresh(); }
    }

    private void CaptureSaved()
    {
        savedJson = ConfigJson.Serialize(candidate);
        savedEntries = candidate.Shortcuts.ToDictionary(item => item.Id, item => JsonSerializer.Serialize(item, ConfigJson.Options));
        savedSettings = JsonSerializer.Serialize(candidate.Settings, ConfigJson.Options);
    }

    private bool CheckSavedSourceUnchanged()
    {
        try
        {
            if (ConfigJson.Serialize(repository.Load()) == savedJson) return true;
            Status = "保存設定が読込み後に変わったため、上書きせず反映を停止しました。未保存候補は保持しています。";
        }
        catch (Exception error) { Status = "保存設定を再確認できないため、上書きせず反映を停止しました。" + error.Message; }
        savedConfigChangedExternally = true;
        RaiseActionState();
        return false;
    }

    private string DefinitionVersion(ShortcutDefinition item) => JsonSerializer.Serialize(item, ConfigJson.Options)
        + (item.Target is ChromeTabGroupTarget ? JsonSerializer.Serialize(candidate.Settings.Chrome, ConfigJson.Options) : string.Empty);

    private void RaiseActionState()
    {
        foreach (var name in new[] { nameof(CanApply), nameof(CanModify), nameof(CanRestore), nameof(CanUndoLastApply), nameof(CanEditSelected), nameof(CanConfirmHotkey), nameof(CanAuditSaved), nameof(CanRepairSaved), nameof(PendingChangeCount), nameof(ReloadLabel), nameof(SelectionText), nameof(EditingNotice), nameof(AuditNotice), nameof(ShellNotice), nameof(PhysicalConfirmationNotice) }) Raise(name);
    }

    private void Refresh()
    {
        var selectedId = Selected?.Definition.Id;
        var current = candidate.Shortcuts.ToDictionary(item => item.Id, item => JsonSerializer.Serialize(item, ConfigJson.Options));
        var settingsChanged = savedSettings != JsonSerializer.Serialize(candidate.Settings, ConfigJson.Options);
        PendingChangeCount = current.Keys.Union(savedEntries.Keys).Count(id => !current.TryGetValue(id, out var value) || !savedEntries.TryGetValue(id, out var prior) || value != prior) + (settingsChanged ? 1 : 0);
        if (explicitRecoveryInitialization && PendingChangeCount == 0) PendingChangeCount = 1;
        IsDirty = PendingChangeCount > 0;
        var validation = validator.Validate(candidate, checkFileSystem: true);
        Shortcuts.Clear();
        foreach (var definition in candidate.Shortcuts.OrderBy(item => item.Hotkey.Key))
        {
            var issues = validation.Issues.Where(issue => issue.ShortcutId == definition.Id).ToArray();
            var changed = !savedEntries.TryGetValue(definition.Id, out var saved) || current[definition.Id] != saved || (settingsChanged && definition.Target is ChromeTabGroupTarget);
            var confirmed = physicalConfirmations.TryGetValue(definition.Id, out var proof) && proof == DefinitionVersion(definition);
            var code = !definition.Enabled ? "Disabled" : issues.Length > 0 ? "Error" : changed ? "Unapplied Changes" : "Ready";
            var label = changed ? "未保存" : !definition.Enabled ? "無効" : issues.Length > 0 ? "入力要確認" : confirmed ? "実キー確認済み（今回の申告）" : ShellReflectionPending ? "保存済み・反映未確認" : savedLinksVerified ? "保存済み・実キー未確認" : "保存済み・リンク未確認";
            Shortcuts.Add(new(definition, TargetSummary(definition.Target), code, string.Join(" / ", issues.Select(issue => issue.Message)), label));
        }
        var tiles = KeyboardLayout.Keys.ToDictionary(
            key => key,
            key =>
            {
                var group = Shortcuts.Where(item => item.Key == key).ToArray();
                var row = group.FirstOrDefault(item => item.Enabled) ?? group.FirstOrDefault();
                var code = group.Any(item => item.Status == "Error") ? "Error" : group.Any(item => item.StateLabel == "未保存") ? "Unapplied Changes" : row?.Status ?? "Empty";
                return new KeyTile(key, group.Length > 1 ? $"{group.Length}件の割当" : row?.Name ?? "空き", code, row, group.Length, group.Length > 1 ? string.Join("\n\n", group.Select(item => item.FullDescription + (item.Enabled ? "\n有効" : "\n無効"))) : null);
            });
        Replace(TopRowKeys, KeyboardLayout.Rows[0].Select(key => tiles[key.ToString()]));
        Replace(HomeRowKeys, KeyboardLayout.Rows[1].Select(key => tiles[key.ToString()]));
        Replace(BottomRowKeys, KeyboardLayout.Rows[2].Select(key => tiles[key.ToString()]));
        Selected = Shortcuts.FirstOrDefault(row => row.Definition.Id == selectedId);
        Raise(nameof(ManagedDirectory));
        Raise(nameof(ShowEmptyState));
        RaiseActionState();
    }

    private static void Replace(ObservableCollection<KeyTile> target, IEnumerable<KeyTile> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
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

public sealed record ShortcutRow(ShortcutDefinition Definition, string Target, string Status, string Error, string StateLabel)
{
    public bool Enabled => Definition.Enabled;
    public string Key => Definition.Hotkey.Key;
    public string Hotkey => Definition.Hotkey.Canonical;
    public string Name => Definition.Name;
    public string Type => Definition.Target switch { WebsiteTarget => "Webサイト", ApplicationTarget => "アプリ", BatTarget => "BAT / CMD", ChromeTabGroupTarget => "Chromeグループ", _ => "未対応" };
    public string FullDescription => $"{Hotkey} → {Name}\n種類: {Type}\n{Target}\n{StateLabel}" + (Definition.Target switch { ApplicationTarget app=>$"\n追加引数: {app.Arguments}\n作業フォルダ: {app.WorkingDirectory}" + (app.LaunchMode!=ApplicationLaunchMode.Executable ? "\nこの起動方法では追加引数を渡しません。" : string.Empty), BatTarget bat=>$"\n追加引数: {bat.Arguments}\n作業フォルダ: {bat.WorkingDirectory}", _=>string.Empty }) + (string.IsNullOrEmpty(Error) ? string.Empty : "\n検証: " + Error);
}

public sealed class KeyTile(string key, string name, string status, ShortcutRow? row, int entryCount = 0, string? description = null) : ObservableObject
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    public string Status { get; } = status;
    public ShortcutRow? Row { get; } = row;
    public int EntryCount { get; } = entryCount;
    private bool isSelected;
    public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }
    public string Description => description ?? Row?.FullDescription ?? $"Ctrl+Alt+{Key}: 空きキーに追加";
    public string StatusLabel => Status switch
    {
        "Ready" => "保存済み",
        "Disabled" => "無効",
        "Error" => "要確認",
        "Unapplied Changes" => "未反映",
        _ => "空き",
    };
}
