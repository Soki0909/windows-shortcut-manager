using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.App;

public sealed class ScanOptionsWindow : Window
{
    public IReadOnlyList<string> Roots {get;private set;}=[];
    public ScanOptionsWindow()
    {
        Title="読取り専用スキャンの範囲";Width=720;Height=520;MinWidth=440;MinHeight=300;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        var selected=new List<(CheckBox Check,string Path)>();var list=new StackPanel();
        void Add(string path) {if(selected.Any(item=>item.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))return;var check=new CheckBox {Content=path,IsChecked=true,Margin=new(0,6,0,6)};selected.Add((check,path));list.Children.Add(check);}
        foreach(var path in ShortcutInventoryScanner.DefaultRoots) Add(path);
        var browse=new Button {Content="別のフォルダを追加…"};browse.Click+=(_,_)=> {var dialog=new OpenFolderDialog {Title="追加の読取り範囲"};if(dialog.ShowDialog(this)==true)Add(dialog.FolderName);};
        var go=new Button {Content="選択範囲をスキャン",IsDefault=true};var cancel=new Button {Content="キャンセル",IsCancel=true};cancel.Click+=(_,_)=>DialogResult=false;
        go.Click+=(_,_)=> {Roots=selected.Where(item=>item.Check.IsChecked==true).Select(item=>item.Path).ToArray();if(Roots.Count>0)DialogResult=true;};
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(browse);buttons.Children.Add(cancel);buttons.Children.Add(go);
        var panel=new DockPanel {Margin=new(18)};var description=new TextBlock {Text="既定は共有/個人のスタートメニューとデスクトップです。選んだフォルダの.lnk/.urlを再帰的に読みます。元ファイルの変更・起動は行いません。\n重複範囲は統合し、リンク・ジャンクションは追跡せず、読取り失敗を結果に表示します。全ディスクスキャンは既定にしません。",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};DockPanel.SetDock(description,Dock.Top);panel.Children.Add(description);DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);panel.Children.Add(new ScrollViewer {Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=panel;
    }
}
