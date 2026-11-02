using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using ShortcutManager.Windows.Backup;

namespace ShortcutManager.App;

public sealed class BackupHistoryWindow : Window
{
    public string? SelectedPath {get;private set;}
    public BackupHistoryWindow(IReadOnlyList<BackupHistoryEntry> entries)
    {
        Title="バックアップ履歴";Width=1000;Height=650;MinWidth=520;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        var grid=new DataGrid {ItemsSource=entries,AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single};
        foreach(var (label,property,width) in new[] {("日時",nameof(BackupHistoryEntry.Timestamp),170d),("由来",nameof(BackupHistoryEntry.Origin),140d),("理由",nameof(BackupHistoryEntry.Reason),120d),("版",nameof(BackupHistoryEntry.Version),80d),("件数",nameof(BackupHistoryEntry.Count),60d),("容量 (bytes)",nameof(BackupHistoryEntry.Bytes),100d),("読取りエラー",nameof(BackupHistoryEntry.Error),240d)})grid.Columns.Add(new DataGridTextColumn {Header=label,Binding=new Binding(property),Width=width,ElementStyle=(Style)FindResource("GridCellTextStyle")});
        var details=new TextBox {IsReadOnly=true,Height=120,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var preview=new Button {Content="差分を確認して復元…",IsEnabled=false};var location=new Button {Content="保存場所を表示",IsEnabled=false};
        grid.SelectionChanged+=(_,_)=> {var item=grid.SelectedItem as BackupHistoryEntry;SelectedPath=item?.Path;preview.IsEnabled=item is {Error:null};location.IsEnabled=item is not null;details.Text=item is null?string.Empty:$"{item.Path}\n{item.Timestamp} / {item.Reason} / {item.Version}\n{item.Origin}\n{item.Error}\n表示しただけでは復元しません。内容は次の差分画面で確認します。";};
        preview.Click+=(_,_)=> {if(preview.IsEnabled)DialogResult=true;};location.Click+=(_,_)=> {if(SelectedPath is { } path)InventoryLocation.Show(this,path);};
        var file=new Button {Content="別のZIPファイルを選ぶ…"};file.Click+=(_,_)=> {var dialog=new OpenFileDialog {Filter="Shortcut Manager backup (*.zip)|*.zip"};if(dialog.ShowDialog(this)==true){SelectedPath=dialog.FileName;DialogResult=true;}};
        var close=new Button {Content="戻る",IsCancel=true};close.Click+=(_,_)=>DialogResult=false;
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(file);buttons.Children.Add(location);buttons.Children.Add(close);buttons.Children.Add(preview);
        var bottom=new StackPanel();bottom.Children.Add(details);bottom.Children.Add(buttons);
        var description=new TextBlock {Text=$"この版のローカルバックアップ履歴 {entries.Count} 件。自動生成を検証できた分と手動・外部・旧版を区別しています。壊れたZIPもエラーとして残し、自動削除しません。\n任意の場所へ手動保存したZIPは「別のZIPファイルを選ぶ」から指定してください。",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};
        var panel=new DockPanel {Margin=new(16)};DockPanel.SetDock(description,Dock.Top);panel.Children.Add(description);DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);panel.Children.Add(grid);Content=panel;
    }
}
