using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.App;

public sealed class ConflictWindow : Window
{
    public ShortcutInventoryItem? Selected {get;private set;}
    public ConflictWindow(IReadOnlyList<ShortcutInventoryItem> items,InventoryScanReport? report)
    {
        Title="外部キー競合の確認";Width=920;Height=600;MinWidth=500;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        var grid=new DataGrid {ItemsSource=items,AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single};
        foreach(var (label,path) in new[] {("キー",nameof(ShortcutInventoryItem.Hotkey)),("形式",nameof(ShortcutInventoryItem.FileType)),("名前",nameof(ShortcutInventoryItem.DisplayName)),("元ファイル",nameof(ShortcutInventoryItem.SourcePath)),("起動先",nameof(ShortcutInventoryItem.Target))}) grid.Columns.Add(new DataGridTextColumn {Header=label,Binding=new Binding(path),Width=DataGridLength.Auto});
        var details=new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Height=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var edit=new Button {Content="同じキーの割当を編集",IsEnabled=false};
        grid.SelectionChanged+=(_,_)=> {Selected=grid.SelectedItem as ShortcutInventoryItem;edit.IsEnabled=Selected is not null;details.Text=Selected is { } item ? $"{item.Hotkey}\n元ファイル: {item.SourcePath}\n起動先: {item.Target}\n形式: {item.FileType}\n引数: {item.Arguments}\n{item.ParseError}" : string.Empty;};
        edit.Click+=(_,_)=>DialogResult=true;
        var location=new Button {Content="元ファイルの場所を表示"};location.Click+=(_,_)=> {if(Selected is { } item)InventoryLocation.Show(this,item.SourcePath);};
        var close=new Button {Content="戻る",IsCancel=true};close.Click+=(_,_)=>DialogResult=false;
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(location);buttons.Children.Add(close);buttons.Children.Add(edit);
        var bottom=new StackPanel();bottom.Children.Add(details);bottom.Children.Add(buttons);
        var top=new TextBlock {Text="外部の同じキーが残っているため反映を停止しました。対象を選び、場所を確認するか、候補に別のキーを指定してください。元ファイルのキーは自動解除しません。\n"+(report?.Summary ?? string.Empty)+"\nアプリが動的に取得したキーは検出できません。読取り不能や未スキャンがある場合、競合なしとは断定できません。",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};
        var panel=new DockPanel {Margin=new(16)};DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);panel.Children.Add(grid);Content=panel;
    }
}
