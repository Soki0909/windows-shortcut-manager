using System.Windows;
using System.Windows.Controls;

namespace ShortcutManager.App;

public sealed class ShortcutDetailsWindow : Window
{
    public ShortcutDetailsWindow(ShortcutRow row, Func<Window,bool> edit)
    {
        Title="選択した割当の詳細"; Width=720; Height=520; MinWidth=440; MinHeight=300; WindowStartupLocation=WindowStartupLocation.CenterOwner; DialogLayout.Constrain(this);
        var panel=new DockPanel {Margin=new(18)};
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,8,0,0)};
        var close=new Button {Content="閉じる",IsCancel=true}; var change=new Button {Content="この割当を編集"};
        close.Click+=(_,_)=>DialogResult=false; change.Click+=(_,_)=> {if(edit(this)) DialogResult=true;};
        buttons.Children.Add(close); buttons.Children.Add(change); DockPanel.SetDock(buttons,Dock.Bottom); panel.Children.Add(buttons);
        var help=new TextBlock {Text="省略されていない名前・起動先・検証結果です。テキストを選択しCtrl+Cでコピーできます。",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,8)}; DockPanel.SetDock(help,Dock.Top); panel.Children.Add(help);
        panel.Children.Add(new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text=row.FullDescription}); Content=panel;
    }
}
