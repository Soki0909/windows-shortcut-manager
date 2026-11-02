using System.Windows;
using System.Windows.Controls;
using ShortcutManager.Windows.Backup;

namespace ShortcutManager.App;

public sealed class RestorePreviewWindow : Window
{
    public PreparedRestore Prepared {get;private set;}
    public RestorePreviewWindow(PreparedRestore initial,int unsavedChanges,Func<string> scanSummary,Func<RestoreMode,Task<PreparedRestore>> prepare)
    {
        Prepared=initial;Title="復元する差分を確認";Width=880;Height=730;MinWidth=500;MinHeight=380;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        var details=new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var mode=new ComboBox {DisplayMemberPath="Label",SelectedValuePath="Mode",ItemsSource=new[] {new ModeChoice(RestoreMode.Entire,"全体を復元（不足・競合があれば停止）"),new ModeChoice(RestoreMode.DisableUnavailable,"起動先に問題・外部競合のある割当を無効にして復元")},SelectedValue=initial.Mode};
        var acknowledge=new CheckBox {Content="差分と無効になる割当を確認しました",Margin=new(0,8,0,8)};
        var restore=new Button {Content="現在をバックアップして復元する",IsEnabled=false};
        var cancel=new Button {Content="復元せず戻る",IsCancel=true,IsDefault=true};cancel.Click+=(_,_)=>DialogResult=false;
        bool validPreview=true;
        void Display() {if(!validPreview){restore.IsEnabled=false;return;}details.Text=Prepared.Preview.Description+"\n\n無効化の理由:\n"+string.Join("\n",Prepared.Preview.DisabledReasons)+"\n\n"+scanSummary();restore.IsEnabled=Prepared.Preview.CanRestore && acknowledge.IsChecked==true;}
        acknowledge.Click+=(_,_)=>Display();restore.Click+=(_,_)=> {if(restore.IsEnabled)DialogResult=true;};
        bool loading=false;Closing+=(_,e)=> {if(loading)e.Cancel=true;};
        mode.SelectionChanged+=async (_,_)=> {
            if(mode.SelectedValue is not RestoreMode selected)return;
            loading=true;validPreview=false;mode.IsEnabled=cancel.IsEnabled=false;restore.IsEnabled=false;acknowledge.IsChecked=false;
            try {Prepared=await prepare(selected);validPreview=true;Display();}
            catch(Exception error) {details.Text="確認できません。元データは変更していません。\n"+error.Message;}
            finally {loading=false;mode.IsEnabled=cancel.IsEnabled=true;}
        };
        var top=new StackPanel();top.Children.Add(new TextBlock {Text="この画面の表示・モード変更・取消では設定やリンクを書き換えません。復元時は保存済み状態を自動バックアップし、競合と起動先を再検証します。\n"+(unsavedChanges>0?$"未保存の候補 {unsavedChanges} 項目は復元成功後に置き換わります。未保存候補はバックアップに含まれません。":"未保存の候補変更はありません。"),TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,10)});top.Children.Add(mode);
        var bottom=new StackPanel();bottom.Children.Add(acknowledge);var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(cancel);buttons.Children.Add(restore);bottom.Children.Add(buttons);
        var panel=new DockPanel {Margin=new(16)};DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);panel.Children.Add(details);Content=panel;Display();
    }
    private sealed record ModeChoice(RestoreMode Mode,string Label);
}
