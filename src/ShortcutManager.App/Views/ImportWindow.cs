using System.Windows;
using System.Windows.Controls;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.App;

public sealed class ImportWindow : Window
{
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single };
    public ShortcutInventoryItem? Selected => grid.SelectedItem as ShortcutInventoryItem;

    public ImportWindow(IReadOnlyList<ShortcutInventoryItem> items)
    {
        Title = "既存ショートカットをインポート"; Width = 900; Height = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        grid.Columns.Add(new DataGridTextColumn { Header = "HotKey", Binding = new System.Windows.Data.Binding(nameof(ShortcutInventoryItem.Hotkey)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding(nameof(ShortcutInventoryItem.DisplayName)), Width = new(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new System.Windows.Data.Binding(nameof(ShortcutInventoryItem.FileType)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Target", Binding = new System.Windows.Data.Binding(nameof(ShortcutInventoryItem.Target)), Width = new(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Managed", Binding = new System.Windows.Data.Binding(nameof(ShortcutInventoryItem.IsManaged)) });
        grid.ItemsSource = items.Where(item => !item.IsManaged).ToList();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "キャンセル", Margin = new(4), Padding = new(12, 6, 12, 6) }; cancel.Click += (_, _) => DialogResult = false;
        var import = new Button { Content = "選択をインポート", Margin = new(4), Padding = new(14, 6, 14, 6), IsDefault = true }; import.Click += (_, _) => { if (Selected is null) return; DialogResult = true; };
        buttons.Children.Add(cancel); buttons.Children.Add(import);
        var panel = new DockPanel { Margin = new(12) }; DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(grid); Content = panel;
    }
}
