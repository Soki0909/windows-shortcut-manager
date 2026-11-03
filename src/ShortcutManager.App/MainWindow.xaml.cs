using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ShortcutManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly Windows.Apply.ManagedShortcutAuditor auditor;

    public MainWindow(MainViewModel viewModel, Windows.Apply.ManagedShortcutAuditor auditor)
    {
        InitializeComponent();
        Title = BuildIdentity.DisplayName;
        ProductTitle.Text = BuildIdentity.DisplayName;
        if (BuildIdentity.IsDevelopment)
        {
            DevelopmentNotice.Visibility = Visibility.Visible;
            Icon = BuildIdentity.CreateDevelopmentIcon();
            ProductLogo.Source = Icon;
        }
        this.viewModel = viewModel;
        this.auditor = auditor;
        DataContext = viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Edit(null, null);
    private void LaunchDiagnostics_Click(object sender, RoutedEventArgs e) { if (!viewModel.IsBusy) new LaunchDiagnosticsWindow { Owner = this }.ShowDialog(); }
    private void ExportDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.IsBusy) return;
        try { new DiagnosticExportWindow(viewModel.CaptureDiagnostic(typeof(App).Assembly.GetName().Version!, BuildIdentity.IsDevelopment)) { Owner = this }.ShowDialog(); }
        catch (Exception error) { MessageBox.Show(this, "診断を作成できませんでした: " + error.Message, "報告用の診断", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void Edit_Click(object sender, RoutedEventArgs e) { if (viewModel.Selected is not null) Edit(viewModel.Selected.Definition, null); }
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (viewModel.Selected is not null) Edit(viewModel.Selected.Definition, null); }
    private void KeyTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KeyTile tile } && viewModel.SelectKey(tile.Key)) Edit(viewModel.Selected?.Definition, tile.Key);
    }

    private bool Edit(Core.Models.ShortcutDefinition? definition, string? key, Window? owner = null, int initialType = 0)
    {
        if (viewModel.IsBusy || viewModel.RecoveryRequired) return false;
        var usedKeys = viewModel.Shortcuts.Where(row => row.Definition.Id != definition?.Id && row.Enabled).GroupBy(row => row.Key).ToDictionary(group => group.Key, group => string.Join(" / ", group.Select(row => row.Name)));
        var invalid=definition is not null && viewModel.Shortcuts.Any(row=>row.Definition.Id==definition.Id && !string.IsNullOrEmpty(row.Error));
        var dialog = new ShortcutEditorWindow(definition, key, usedKeys, initialType, viewModel.Candidate, invalid) { Owner = owner ?? this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return false;
        viewModel.Upsert(dialog.Result);
        viewModel.Selected = viewModel.Shortcuts.First(row => row.Definition.Id == dialog.Result.Id);
        return true;
    }

    private void AddWebsite_Click(object sender, RoutedEventArgs e) => Edit(null, null);
    private void AddApplication_Click(object sender, RoutedEventArgs e) => Edit(null, null, initialType: 1);
    private void Help_Click(object sender, RoutedEventArgs e) => new HelpWindow(owner => ShowGettingStarted(owner)) { Owner = this }.ShowDialog();
    public void ShowGettingStarted(Window? owner = null) => new GettingStartedWindow(viewModel, guide => Edit(null, null, guide)) { Owner = owner ?? this }.ShowDialog();
    private void Guide_Click(object sender, RoutedEventArgs e) => ShowGettingStarted();

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if(!viewModel.CanModify) return;
        var options=new ScanOptionsWindow {Owner=this};if(options.ShowDialog()!=true)return;
        try {
            var results = await viewModel.ScanAsync(options.Roots);
            var dialog = new ImportWindow(results,viewModel.Candidate,viewModel.LastScanReport) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Selected is not null) viewModel.Import(dialog.Selected,dialog.SelectedKey);
        }
        catch(Exception error) {MessageBox.Show(this,error.Message,"スキャンできません",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Windows.Backup.BackupScope.Description + "\n\n保存先を選びますか？", "バックアップの範囲", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        var dialog = new SaveFileDialog { Filter = "Shortcut Manager backup (*.zip)|*.zip", FileName = $"ShortcutManager-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip" };
        if (dialog.ShowDialog(this) != true) return;
        try { viewModel.Backup(dialog.FileName); }
        catch (Exception error) { MessageBox.Show(this, error.Message, "バックアップ失敗", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Shortcut Manager backup (*.zip)|*.zip" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await ShowRestorePreviewAsync(await viewModel.PrepareRestoreAsync(dialog.FileName));
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "復元できません", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task ShowRestorePreviewAsync(Windows.Backup.PreparedRestore prepared)
    {
        var dialog=new RestorePreviewWindow(prepared,viewModel.PendingChangeCount,()=>viewModel.LastScanReport?.Summary ?? string.Empty,mode=>viewModel.PrepareRestoreAsync(prepared.BackupPath,mode)) {Owner=this};
        if(dialog.ShowDialog()==true && await viewModel.RestoreAsync(dialog.Prepared.BackupPath,dialog.Prepared))await OfferShellReloadAsync();
    }
    private async void BackupHistory_Click(object sender,RoutedEventArgs e)
    {
        if(!viewModel.CanRestore)return;
        try {var dialog=new BackupHistoryWindow(await viewModel.ReadBackupHistoryAsync()) {Owner=this};if(dialog.ShowDialog()==true && dialog.SelectedPath is { } path)await ShowRestorePreviewAsync(await viewModel.PrepareRestoreAsync(path));}
        catch(Exception error) {MessageBox.Show(this,error.Message,"履歴を確認できません",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    private async void UndoApply_Click(object sender,RoutedEventArgs e)
    {
        try {await ShowRestorePreviewAsync(await viewModel.PrepareUndoRestoreAsync());}
        catch(Exception error) {MessageBox.Show(this,error.Message,"直前の変更を戻せません",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(viewModel.Candidate.Settings.Chrome.ExecutablePath, viewModel.Candidate.Settings.Chrome.ExtensionId, viewModel.ConfigPath, viewModel.ManagedDirectory) { Owner = this };
        if (settings.ShowDialog() == true) viewModel.UpdateSettings(settings.ChromePath, settings.ExtensionId);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Selected is not null && MessageBox.Show(this, $"「{viewModel.Selected.Name}」を候補設定から削除しますか？実ファイルは「変更を反映」まで変更されません。", "割当を削除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) viewModel.DeleteSelected();
    }

    private void Test_Click(object sender, RoutedEventArgs e) => PreviewSelected(false);
    private void Preview_Click(object sender, RoutedEventArgs e) => PreviewSelected(true);
    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if(!viewModel.CanEditSelected) return;
        var row=viewModel.Selected!;
        new ShortcutDetailsWindow(row,owner=>Edit(row.Definition,null,owner)) {Owner=this}.ShowDialog();
    }
    private void PreviewSelected(bool alwaysShow)
    {
        if(!viewModel.CanEditSelected) return;
        var definition=viewModel.Selected!.Definition;
        if(!alwaysShow && definition.Target is Core.Models.WebsiteTarget) {viewModel.Test(definition);return;}
        var spec=viewModel.PrepareLaunch(definition);
        if(spec is null) {MessageBox.Show(this,viewModel.Status,"起動内容を確認できません",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var notice=definition.Target is Core.Models.ApplicationTarget app ? Windows.Launching.ApplicationLaunchAdvice.Describe(app,true) : string.Empty;
        new LaunchPreviewWindow(definition.Name,spec,notice,()=> {var succeeded=viewModel.ExecutePreview(definition,spec);return(succeeded,viewModel.Status);}) {Owner=this}.ShowDialog();
    }
    private void Reload_Click(object sender, RoutedEventArgs e) { if (!viewModel.IsDirty || MessageBox.Show(this, $"未保存の変更 {viewModel.PendingChangeCount} 項目を破棄し、保存済み設定を読み直しますか？\nWindowsのリンク監査やExplorer再起動は行いません。", "編集を破棄して再読込", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) viewModel.Load(); }
    private async void AuditSaved_Click(object sender, RoutedEventArgs e) => await viewModel.AuditSavedAsync(auditor);
    private async void AuditHistory_Click(object sender,RoutedEventArgs e)
    {
        if(viewModel.IsBusy)return;
        try {var dialog=new AuditHistoryWindow(await viewModel.ReadAuditHistoryAsync(),owner=>new HelpWindow(guideOwner=>ShowGettingStarted(guideOwner)){Owner=owner}.ShowDialog()) {Owner=this};if(dialog.ShowDialog()==true && dialog.SelectedBackupPath is { } path)await ShowRestorePreviewAsync(await viewModel.PrepareRestoreAsync(path));}
        catch(Exception error) {MessageBox.Show(this,error.Message,"監査履歴を確認できません",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    private async void RepairSaved_Click(object sender,RoutedEventArgs e)
    {
        if(!viewModel.CanRepairSaved)return;
        await viewModel.AuditSavedAsync(auditor);if(!viewModel.CanRepairSaved)return;
        if(viewModel.ConflictingItems.Count>0) {
            var conflicts=new ConflictWindow(viewModel.ConflictingItems,viewModel.LastScanReport){Owner=this};
            if(conflicts.ShowDialog()==true && conflicts.Selected is { } item){var row=viewModel.Shortcuts.FirstOrDefault(entry=>entry.Enabled && entry.Hotkey.Equals(item.Hotkey,StringComparison.OrdinalIgnoreCase));if(row is not null){viewModel.Selected=row;Edit(row.Definition,null);}}
            return;
        }
        if(new AuditRepairWindow(viewModel.LastAuditDetails,viewModel.LastScanReport?.Summary ?? string.Empty){Owner=this}.ShowDialog()==true && await viewModel.AuditStartupAsync(auditor,explicitRepair:true))await OfferShellReloadAsync();
    }
    private async void RecoverPending_Click(object sender,RoutedEventArgs e)
    {
        if(viewModel.IsBusy || viewModel.IsDirty){MessageBox.Show(this,"未保存の候補がある場合は、先に候補を保全または破棄してください。","中断処理の復旧");return;}
        if(MessageBox.Show(this,"監査のみの起動では保留した、中断した反映・復元のjournalを明示復旧します。退避した完全な状態へ戻します。続行しますか？","中断処理の復旧",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes)await viewModel.RecoverPendingAsync();
    }
    private void StateDetails_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, viewModel.EditingNotice + "\n\n" + viewModel.AuditNotice + "\n\n" + viewModel.ShellNotice + "\n\n" + viewModel.PhysicalConfirmationNotice, "保存・リンク・Windowsの状態", MessageBoxButton.OK, MessageBoxImage.Information);
    private void ConfirmHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.CanConfirmHotkey) return;
        if (MessageBox.Show(this, $"実際の {viewModel.Selected!.Hotkey} を押して「{viewModel.Selected.Name}」が意図どおり動くことを確認しましたか？\n対象のテスト起動・保存・Explorer再起動だけでは確認済みにしません。今回の起動中の利用者の申告として記録します。", "選択キーの実動作確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) viewModel.ConfirmSelectedHotkey();
    }
    private void InitializeRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "破損設定をバックアップして空の候補設定を用意します。後で「変更を反映」を押すと既存の管理HotKeyが削除されます。初期化しますか？", "復旧用初期化", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { viewModel.InitializeRecovery(); }
        catch (Exception error) { MessageBox.Show(this, $"バックアップに失敗したため初期化を中止しました: {error.Message}", "復旧", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!await viewModel.ApplyAsync()) {
            if(viewModel.ConflictingItems.Count>0) {
                var conflicts=new ConflictWindow(viewModel.ConflictingItems,viewModel.LastScanReport) {Owner=this};
                if(conflicts.ShowDialog()==true && conflicts.Selected is { } item) {
                    var row=viewModel.Shortcuts.FirstOrDefault(entry=>entry.Enabled && entry.Hotkey.Equals(item.Hotkey,StringComparison.OrdinalIgnoreCase));
                    if(row is not null) {viewModel.Selected=row;Edit(row.Definition,null);}
                }
            }
            return;
        }
        if (ConfirmShellReload()) await viewModel.ReloadShellAsync();
    }

    private async void ShellReload_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmShellReload()) await viewModel.ReloadShellAsync();
    }

    public async Task OfferShellReloadAsync()
    {
        if (ConfirmShellReload()) await viewModel.ReloadShellAsync();
    }

    internal const string ShellReloadExplanation = "新規・変更したHotKeyの有効化を試すため、Windows Explorerを再起動します。\n\n公開APIではHotKeyの即時反映は保証されないため、反映されない場合はWindowsの再起動が必要です。デスクトップとタスクバーが一瞬消え、開いているエクスプローラー画面が閉じる可能性があります。ファイルのコピーや移動中でないことを確認して続行してください。";
    private bool ConfirmShellReload() => MessageBox.Show(
        this,
        ShellReloadExplanation,
        "Windows Explorerを再起動",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning) == MessageBoxResult.Yes;
    private void About_Click(object sender, RoutedEventArgs e) => new HelpWindow(owner => ShowGettingStarted(owner), showInformation: true) { Owner = this }.ShowDialog();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (viewModel.IsBusy) { e.Cancel = true; return; }
        if (!viewModel.IsDirty) return;
        var result = MessageBox.Show(this, $"未保存の変更 {viewModel.PendingChangeCount} 項目を破棄して終了しますか？\n保存済み設定とWindowsのリンクは変更しません。", "Shortcut Manager", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) e.Cancel = true;
    }
}
