using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ShortcutManager.Core.Models;

namespace ShortcutManager.App;

public sealed class ShortcutEditorWindow : Window
{
    private readonly Guid id;
    private readonly TextBox name = new();
    private readonly CheckBox enabled = new() { Content = "有効", IsChecked = true };
    private readonly TextBox hotkey = new() { IsReadOnly = true };
    private readonly ComboBox type = new() { ItemsSource = new[] { "Website", "Application", "BAT/CMD", "Chrome Tab Group" }, SelectedIndex = 0 };
    private readonly ComboBox launchMode = new() { ItemsSource = Enum.GetValues<ApplicationLaunchMode>() };
    private readonly TextBox target = new();
    private readonly TextBox arguments = new();
    private readonly TextBox workingDirectory = new();
    private readonly ComboBox windowMode = new() { ItemsSource = Enum.GetValues<BatWindowMode>() };
    private readonly TextBlock hint = new() { Opacity = .65, TextWrapping = TextWrapping.Wrap };
    private readonly Grid form = new();
    private readonly IReadOnlyDictionary<string, string> usedKeys;

    public ShortcutDefinition? Result { get; private set; }

    public ShortcutEditorWindow(ShortcutDefinition? definition, string? preselectedKey, IReadOnlyDictionary<string, string>? usedKeys = null)
    {
        this.usedKeys = usedKeys ?? new Dictionary<string, string>();
        id = definition?.Id ?? Guid.NewGuid();
        Title = definition is null ? "ショートカットを追加" : "ショートカットを編集";
        Width = 620; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        for (var index = 0; index < 9; index++) form.RowDefinitions.Add(new() { Height = GridLength.Auto });
        form.ColumnDefinitions.Add(new() { Width = new(155) }); form.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        AddRow(0, "Name", name); AddRow(1, "", enabled); AddRow(2, "HotKey", hotkey); AddRow(3, "Type", type); AddRow(4, "Launch mode", launchMode); AddRow(5, "URL / Path / Group", TargetPanel()); AddRow(6, "Arguments", arguments); AddRow(7, "Working directory", workingDirectory); AddRow(8, "BAT window", windowMode);
        hotkey.PreviewKeyDown += Hotkey_PreviewKeyDown; type.SelectionChanged += (_, _) => UpdateVisibility();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "キャンセル", Padding = new(14, 7, 14, 7), Margin = new(4) }; cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "OK", Padding = new(18, 7, 18, 7), Margin = new(4), IsDefault = true }; ok.Click += Save_Click;
        buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var panel = new DockPanel { Margin = new(18) }; DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); DockPanel.SetDock(hint, Dock.Bottom); panel.Children.Add(hint); panel.Children.Add(form); Content = panel;
        Load(definition, preselectedKey); UpdateVisibility();
    }

    private FrameworkElement TargetPanel()
    {
        var panel = new DockPanel();
        var browse = new Button { Content = "参照…", Margin = new(5, 0, 0, 0), Padding = new(8, 3, 8, 3) };
        browse.Click += (_, _) => { var dialog = new OpenFileDialog { Filter = "実行/shortcut/BAT|*.exe;*.lnk;*.bat;*.cmd|すべて|*.*" }; if (dialog.ShowDialog(this) == true) target.Text = dialog.FileName; };
        DockPanel.SetDock(browse, Dock.Right); panel.Children.Add(browse); panel.Children.Add(target); return panel;
    }

    private void AddRow(int row, string label, FrameworkElement control)
    {
        var text = new TextBlock { Text = label, Margin = new(0, 8, 10, 8), VerticalAlignment = VerticalAlignment.Center };
        control.Margin = new(0, 5, 0, 5); Grid.SetRow(text, row); Grid.SetColumn(text, 0); Grid.SetRow(control, row); Grid.SetColumn(control, 1); form.Children.Add(text); form.Children.Add(control);
    }

    private void Load(ShortcutDefinition? definition, string? key)
    {
        if (definition is null) { hotkey.Text = key is null ? "クリックして Ctrl+Alt+A-Z を入力" : $"CTRL+ALT+{key}"; launchMode.SelectedItem = ApplicationLaunchMode.Executable; windowMode.SelectedItem = BatWindowMode.Normal; return; }
        name.Text = definition.Name; enabled.IsChecked = definition.Enabled; hotkey.Text = definition.Hotkey.Canonical;
        switch (definition.Target)
        {
            case WebsiteTarget item: type.SelectedIndex = 0; target.Text = item.Url; break;
            case ApplicationTarget item: type.SelectedIndex = 1; target.Text = item.Value; arguments.Text = item.Arguments; workingDirectory.Text = item.WorkingDirectory; launchMode.SelectedItem = item.LaunchMode; break;
            case BatTarget item: type.SelectedIndex = 2; target.Text = item.Path; arguments.Text = item.Arguments; workingDirectory.Text = item.WorkingDirectory; windowMode.SelectedItem = item.WindowMode; break;
            case ChromeTabGroupTarget item: type.SelectedIndex = 3; target.Text = item.GroupName; break;
        }
    }

    private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != (ModifierKeys.Control | ModifierKeys.Alt) || key is < Key.A or > Key.Z) { hint.Text = "Ctrl + Alt + A-Z のみ登録できます。"; return; }
        hotkey.Text = $"CTRL+ALT+{key}";
        hint.Text = usedKeys.TryGetValue(key.ToString(), out var assignedName) ? $"警告: {key} は「{assignedName}」で使用中です。" : string.Empty;
    }

    private void UpdateVisibility()
    {
        var application = type.SelectedIndex == 1; var bat = type.SelectedIndex == 2;
        launchMode.Visibility = application ? Visibility.Visible : Visibility.Collapsed;
        arguments.Visibility = application || bat ? Visibility.Visible : Visibility.Collapsed;
        workingDirectory.Visibility = application || bat ? Visibility.Visible : Visibility.Collapsed;
        windowMode.Visibility = bat ? Visibility.Visible : Visibility.Collapsed;
        hint.Text = type.SelectedIndex == 3 ? "Chrome Extension ID はメイン画面の設定で指定します。任意の完全一致グループ名を入力してください。" : string.Empty;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var key = hotkey.Text.Split('+').LastOrDefault();
        if (string.IsNullOrWhiteSpace(name.Text) || key is not { Length: 1 } || key[0] is < 'A' or > 'Z' || string.IsNullOrWhiteSpace(target.Text)) { MessageBox.Show(this, "Name、Ctrl+Alt+A-Z、Target を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        ShortcutTarget shortcutTarget = type.SelectedIndex switch
        {
            0 => new WebsiteTarget { Url = target.Text.Trim() },
            1 => new ApplicationTarget { LaunchMode = launchMode.SelectedItem is ApplicationLaunchMode mode ? mode : ApplicationLaunchMode.Executable, Value = target.Text.Trim(), Arguments = arguments.Text, WorkingDirectory = workingDirectory.Text },
            2 => new BatTarget { Path = target.Text.Trim(), Arguments = arguments.Text, WorkingDirectory = workingDirectory.Text, WindowMode = windowMode.SelectedItem is BatWindowMode mode ? mode : BatWindowMode.Normal },
            3 => new ChromeTabGroupTarget { GroupName = target.Text.Trim() },
            _ => throw new InvalidOperationException(),
        };
        Result = new() { Id = id, Name = name.Text.Trim(), Enabled = enabled.IsChecked == true, Hotkey = new() { Key = key }, Target = shortcutTarget };
        DialogResult = true;
    }
}
