using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ShortcutManager.Windows.Apply;

namespace ShortcutManager.App;

public sealed class AuditHistoryWindow : Window
{
    public string? SelectedBackupPath {get;private set;}
    public AuditHistoryWindow(IReadOnlyList<AuditHistoryItem> items,Action<Window> help)
    {
        Title="監査と修復の履歴";Width=980;Height=700;MinWidth=500;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        var grid=new DataGrid {ItemsSource=items,AutoGenerateColumns=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single,MaxHeight=230};
        foreach(var (label,path,width) in new[] {("日時","Record.At",190d),("操作","Record.Mode",140d),("結果","Record.Outcome",200d),("読取りエラー","Error",250d)}) {
            var binding=new Binding(path);if(path=="Record.At")binding.StringFormat="{0:yyyy-MM-dd HH:mm:ss zzz}";
            grid.Columns.Add(new DataGridTextColumn {Header=label,Binding=binding,Width=width,ElementStyle=(Style)FindResource("GridCellTextStyle")});
        }
        var details=new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var restore=new Button {Content="修復前の差分を確認して復元…",IsEnabled=false};
        grid.SelectionChanged+=(_,_)=> {var item=grid.SelectedItem as AuditHistoryItem;SelectedBackupPath=item?.Record?.BackupPath;restore.IsEnabled=SelectedBackupPath is not null;details.Text=item?.Record is { } record ? $"{record.At:yyyy-MM-dd HH:mm:ss zzz}\n{record.Mode} / {record.Outcome}\n{record.Message}\n復旧用バックアップ: {record.BackupPath ?? "なし"}\n\n修復前 / 監査時:\n{Describe(record.Before)}\n\n修復後 / 保留した状態:\n{Describe(record.After)}" : item?.Error ?? string.Empty;};
        restore.Click+=(_,_)=> {if(restore.IsEnabled)DialogResult=true;};
        var guide=new Button {Content="復旧の使い方"};guide.Click+=(_,_)=>help(this);
        var close=new Button {Content="戻る",IsCancel=true};close.Click+=(_,_)=>DialogResult=false;
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(guide);buttons.Children.Add(close);buttons.Children.Add(restore);
        var description=new TextBlock {Text="この版のローカル監査・修復履歴を最新50件まで表示します。実際の値と保存設定で期待する値、修復結果とバックアップを確認できます。履歴は外部送信しません。",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,10)};
        var panel=new Grid {Margin=new(16)};panel.RowDefinitions.Add(new(){Height=GridLength.Auto});panel.RowDefinitions.Add(new(){Height=new(2,GridUnitType.Star)});panel.RowDefinitions.Add(new(){Height=new(3,GridUnitType.Star)});panel.RowDefinitions.Add(new(){Height=GridLength.Auto});
        panel.Children.Add(description);Grid.SetRow(grid,1);panel.Children.Add(grid);Grid.SetRow(details,2);panel.Children.Add(details);Grid.SetRow(buttons,3);panel.Children.Add(buttons);Content=panel;
    }
    public static string Describe(IReadOnlyList<AuditDifference> details)=>details.Count==0 ? "リンク差分なし（確認不能の場合は上の結果を確認してください）。" : string.Join("\n\n",details.Select(item=>$"{item.File}\n項目: {item.Field}\n実際: {item.Actual}\n設定が期待する値: {item.Expected}"));
}

public sealed class AuditRepairWindow : Window
{
    public AuditRepairWindow(IReadOnlyList<AuditDifference> details,string notice)
    {
        Title="保存済みの管理リンクを修復";Width=820;Height=620;MinWidth=480;MinHeight=340;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        var acknowledge=new CheckBox {Content="差分とスキャンの未確認範囲を確認しました",Margin=new(0,8,0,8)};
        var repair=new Button {Content="バックアップして明示修復",IsEnabled=false};acknowledge.Click+=(_,_)=>repair.IsEnabled=acknowledge.IsChecked==true;repair.Click+=(_,_)=>DialogResult=true;
        var cancel=new Button {Content="今回保留",IsCancel=true,IsDefault=true};cancel.Click+=(_,_)=>DialogResult=false;
        var bottom=new StackPanel();bottom.Children.Add(acknowledge);var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(cancel);buttons.Children.Add(repair);bottom.Children.Add(buttons);
        var panel=new DockPanel {Margin=new(16)};DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);panel.Children.Add(new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text="保存済み設定に合わせて、管理リンクだけを修復します。編集中の候補は保持します。外部リンクの変更を設定へ逆取り込みしません。現在をバックアップし、外部キー競合と所有マーカーを再確認します。\nスキャンできなかった外部キーや動的取得キーには未確認の範囲があります。修復後のエクスプローラー反映と実キー動作は別に確認してください。\n\n"+notice+"\n\n"+AuditHistoryWindow.Describe(details)});Content=panel;
    }
}
