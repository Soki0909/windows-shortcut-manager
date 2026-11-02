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
        var dialog = new ShortcutEditorWindow(definition, key, usedKeys, initialType) { Owner = owner ?? this };
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
        var results = await viewModel.ScanAsync();
        var dialog = new ImportWindow(results) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Selected is not null) viewModel.Import(dialog.Selected);
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
            var preview = viewModel.PreviewRestore(dialog.FileName);
            if (MessageBox.Show(this, preview + "\n\n現在の状態を自動バックアップしてから復元します。続行しますか？", "復元の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (await viewModel.RestoreAsync(dialog.FileName)) await OfferShellReloadAsync();
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "復元できません", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(viewModel.Candidate.Settings.Chrome.ExecutablePath, viewModel.Candidate.Settings.Chrome.ExtensionId, viewModel.ConfigPath, viewModel.ManagedDirectory) { Owner = this };
        if (settings.ShowDialog() == true) viewModel.UpdateSettings(settings.ChromePath, settings.ExtensionId);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Selected is not null && MessageBox.Show(this, $"「{viewModel.Selected.Name}」を候補設定から削除しますか？実ファイルはApplyまで変更されません。", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) viewModel.DeleteSelected();
    }

    private void Test_Click(object sender, RoutedEventArgs e) { if (viewModel.Selected is not null) viewModel.Test(viewModel.Selected.Definition); }
    private void Reload_Click(object sender, RoutedEventArgs e) { if (!viewModel.IsDirty || MessageBox.Show(this, $"未保存の変更 {viewModel.PendingChangeCount} 項目を破棄し、保存済み設定を読み直しますか？\nWindowsのリンク監査やExplorer再起動は行いません。", "編集を破棄して再読込", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) viewModel.Load(); }
    private async void AuditSaved_Click(object sender, RoutedEventArgs e) => await viewModel.AuditSavedAsync(auditor);
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
        if (!await viewModel.ApplyAsync()) return;
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
        var result = MessageBox.Show(this, "未適用の変更を破棄して終了しますか？", "Shortcut Manager", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) e.Cancel = true;
    }
}
