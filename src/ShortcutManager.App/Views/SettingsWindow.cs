using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ShortcutManager.App;

public sealed class SettingsWindow : Window
{
    private readonly TextBox chromePath = new();
    private readonly TextBox extensionId = new();
    public string? ChromePath => string.IsNullOrWhiteSpace(chromePath.Text) ? null : chromePath.Text;
    public string ExtensionId => extensionId.Text;

    public SettingsWindow(string? currentChromePath, string currentExtensionId, string configPath, string managedPath)
    {
        Title = "設定"; Width = 650; Height = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        chromePath.Text = currentChromePath ?? string.Empty; extensionId.Text = currentExtensionId;
        var grid = new Grid { Margin = new(18) }; grid.ColumnDefinitions.Add(new() { Width = new(160) }); grid.ColumnDefinitions.Add(new());
        for (var index = 0; index < 5; index++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Add(grid, 0, "Chrome executable", ChromePanel()); Add(grid, 1, "Extension ID", extensionId); Add(grid, 2, "config.json", new TextBox { Text = configPath, IsReadOnly = true }); Add(grid, 3, "Managed folder", new TextBox { Text = managedPath, IsReadOnly = true });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "キャンセル", Margin = new(4), Padding = new(12, 6, 12, 6) }; cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "OK", Margin = new(4), Padding = new(18, 6, 18, 6), IsDefault = true }; ok.Click += (_, _) => { if (extensionId.Text.Length != 0 && (extensionId.Text.Length != 32 || extensionId.Text.Any(character => character is < 'a' or > 'p'))) { MessageBox.Show(this, "Extension ID は a-p の32文字です。", "入力エラー"); return; } DialogResult = true; };
        buttons.Children.Add(cancel); buttons.Children.Add(ok); Grid.SetRow(buttons, 4); Grid.SetColumnSpan(buttons, 2); grid.Children.Add(buttons); Content = grid;
    }

    private FrameworkElement ChromePanel() { var panel = new DockPanel(); var browse = new Button { Content = "参照…", Margin = new(5, 0, 0, 0) }; browse.Click += (_, _) => { var dialog = new OpenFileDialog { Filter = "chrome.exe|chrome.exe|実行ファイル|*.exe" }; if (dialog.ShowDialog(this) == true) chromePath.Text = dialog.FileName; }; DockPanel.SetDock(browse, Dock.Right); panel.Children.Add(browse); panel.Children.Add(chromePath); return panel; }
    private static void Add(Grid grid, int row, string label, FrameworkElement field) { var text = new TextBlock { Text = label, Margin = new(0, 8, 8, 8) }; field.Margin = new(0, 5, 0, 5); Grid.SetRow(text, row); Grid.SetColumn(text, 0); Grid.SetRow(field, row); Grid.SetColumn(field, 1); grid.Children.Add(text); grid.Children.Add(field); }
}
