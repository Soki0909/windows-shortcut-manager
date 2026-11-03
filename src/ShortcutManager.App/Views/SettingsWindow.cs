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
        Title = "設定"; Width = 700; Height = 500; MinWidth = 460; MinHeight = 300; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DialogLayout.Constrain(this);
        chromePath.Text = currentChromePath ?? string.Empty; extensionId.Text = currentExtensionId;
        var grid = new Grid { Margin = new(18) }; grid.ColumnDefinitions.Add(new() { Width = new(160) }); grid.ColumnDefinitions.Add(new());
        for (var index = 0; index < 7; index++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        chromePath.ToolTip = "任意。空欄ならChromeを自動検索します。指定する場合はchrome.exeの絶対パスを入力します。";
        extensionId.ToolTip = "タブグループを使う場合に必要です。Tab BridgeのPopupにある拡張機能ID（a～pの32文字）を入力します。";
        Add(grid, 0, "Chromeの実行ファイル", ChromePanel()); Add(grid, 1, "拡張機能ID", extensionId); Add(grid, 2, "設定ファイル", new TextBox { Text = configPath, IsReadOnly = true }); Add(grid, 3, "管理リンクのフォルダ", new TextBox { Text = managedPath, IsReadOnly = true });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "キャンセル", Margin = new(4), Padding = new(12, 6, 12, 6), IsCancel = true }; cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "候補に保存", Margin = new(4), Padding = new(18, 6, 18, 6), IsDefault = true };
        bool Validate(bool checkFile) {
            error.Text = extensionId.Text.Length != 0 && (extensionId.Text.Length != 32 || extensionId.Text.Any(character => character is < 'a' or > 'p')) ? "拡張機能IDは a～p の32文字です。PopupのIDを確認してください。" : string.Empty;
            if (ChromePath is { } path && (!System.IO.Path.IsPathFullyQualified(path) || checkFile && !System.IO.File.Exists(path))) error.Text += "\nChromeの実行ファイルは存在するファイルの絶対パスで指定するか、空欄で自動検索を使ってください。";
            ok.IsEnabled = string.IsNullOrWhiteSpace(error.Text); return ok.IsEnabled;
        }
        ok.Click += (_, _) => { if (Validate(true)) DialogResult = true; };
        chromePath.TextChanged += (_, _) => Validate(false); extensionId.TextChanged += (_, _) => Validate(false);
        Add(grid, 4, "Chrome連携", new Expander { Header = "導入・接続の確認手順", Content = new TextBlock { Text = ChromeSetupGuide.Text, TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 8) } });
        Grid.SetRow(error, 5); Grid.SetColumnSpan(error, 2); grid.Children.Add(error);
        buttons.Children.Add(cancel); buttons.Children.Add(ok); Grid.SetRow(buttons, 6); Grid.SetColumnSpan(buttons, 2); grid.Children.Add(buttons); Content = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Validate(false);
    }

    private FrameworkElement ChromePanel() { var panel = new DockPanel(); var browse = new Button { Content = "参照…", Margin = new(5, 0, 0, 0) }; System.Windows.Automation.AutomationProperties.SetName(browse,"Chromeの実行ファイルを参照"); browse.Click += (_, _) => { var dialog = new OpenFileDialog { Filter = "chrome.exe|chrome.exe|実行ファイル|*.exe" }; if (dialog.ShowDialog(this) == true) chromePath.Text = dialog.FileName; }; DockPanel.SetDock(browse, Dock.Right); panel.Children.Add(browse); panel.Children.Add(chromePath); return panel; }
    private static void LabelInputs(DependencyObject field, TextBlock label)
    {
        if(field is TextBox) System.Windows.Automation.AutomationProperties.SetLabeledBy(field,label);
        foreach(var child in LogicalTreeHelper.GetChildren(field).OfType<DependencyObject>()) LabelInputs(child,label);
    }
    private static void Add(Grid grid, int row, string label, FrameworkElement field) { var text = new TextBlock { Text = label, TextWrapping=TextWrapping.Wrap, Margin = new(0, 8, 8, 8) }; LabelInputs(field,text); field.Margin = new(0, 5, 0, 5); Grid.SetRow(text, row); Grid.SetColumn(text, 0); Grid.SetRow(field, row); Grid.SetColumn(field, 1); grid.Children.Add(text); grid.Children.Add(field); }
}
