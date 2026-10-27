using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ShortcutManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Edit(null, null);
    private void Edit_Click(object sender, RoutedEventArgs e) { if (viewModel.Selected is not null) Edit(viewModel.Selected.Definition, null); }
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (viewModel.Selected is not null) Edit(viewModel.Selected.Definition, null); }
    private void KeyTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KeyTile tile }) Edit(tile.Row?.Definition, tile.Key);
    }

    private void Edit(Core.Models.ShortcutDefinition? definition, string? key)
    {
        var usedKeys = viewModel.Shortcuts.Where(row => row.Definition.Id != definition?.Id && row.Enabled).ToDictionary(row => row.Key, row => row.Name);
        var dialog = new ShortcutEditorWindow(definition, key, usedKeys) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null) viewModel.Upsert(dialog.Result);
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var results = await viewModel.ScanAsync();
        var dialog = new ImportWindow(results) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Selected is not null) viewModel.Import(dialog.Selected);
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Shortcut Manager backup (*.zip)|*.zip", FileName = $"ShortcutManager-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip" };
        if (dialog.ShowDialog(this) == true) viewModel.Backup(dialog.FileName);
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Shortcut Manager backup (*.zip)|*.zip" };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, "現在の状態を自動バックアップしてから復元します。続行しますか？", "Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await viewModel.RestoreAsync(dialog.FileName);
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
    private void Reload_Click(object sender, RoutedEventArgs e) { if (!viewModel.IsDirty || MessageBox.Show(this, "未適用変更を破棄して再読み込みしますか？", "Reload", MessageBoxButton.YesNo) == MessageBoxResult.Yes) viewModel.Load(); }
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

    private bool ConfirmShellReload() => MessageBox.Show(
        this,
        "新規・変更したHotKeyの有効化を試すため、Windows Explorerを再起動します。\n\n公開APIではHotKeyの即時反映は保証されないため、反映されない場合はWindowsの再起動が必要です。デスクトップとタスクバーが一瞬消え、開いているエクスプローラー画面が閉じる可能性があります。ファイルのコピーや移動中でないことを確認して続行してください。",
        "Windows Explorerを再起動",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning) == MessageBoxResult.Yes;
    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Shortcut Manager v0.1.0\n非常駐 / local-only\nWindows .lnk HotKey + Chrome Dispatcher Protocol v1", "About");

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!viewModel.IsDirty) return;
        var result = MessageBox.Show(this, "未適用の変更を破棄して終了しますか？", "Shortcut Manager", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) e.Cancel = true;
    }
}
