using System.Windows;
using System.Windows.Controls;
using ShortcutManager.Core.Models;

namespace ShortcutManager.App;

public sealed class LaunchPreviewWindow : Window
{
    public LaunchPreviewWindow(string name, LaunchSpec spec, string notice, Func<(bool Succeeded,string Message)> execute)
    {
        Title="起動内容の確認"; Width=740; Height=590; MinWidth=440; MinHeight=320; WindowStartupLocation=WindowStartupLocation.CenterOwner; DialogLayout.Constrain(this);
        var panel=new StackPanel {Margin=new(18)};
        var explanation=new TextBlock {Text="表示しただけでは起動しません。「対象をテスト」を押すとBATやアプリの処理を実際に実行します。\n"+notice,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};
        panel.Children.Add(explanation);
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,8,0,0)};
        var close=new Button {Content="実行せず戻る",IsCancel=true,IsDefault=true}; close.Click+=(_,_)=>DialogResult=false;
        var run=new Button {Content=UiText.TestTarget}; var feedback=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,0)};
        run.Click+=(_,_)=> { var result=execute(); feedback.Text=result.Message; if(result.Succeeded) DialogResult=true; };
        buttons.Children.Add(close); buttons.Children.Add(run);
        panel.Children.Add(new TextBox {IsReadOnly=true,Height=260,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text=$"名前: {name}\n\n解決済みの実行先:\n{spec.TargetPath}\n\n実際に渡す引数:\n{spec.Arguments}\n\n作業フォルダ:\n{spec.WorkingDirectory}\n\n表示方法: {(spec.WindowStyle==7 ? "最小化" : "通常")}\n\nこの画面は対象起動のテストです。Windowsのショートカットキー登録や実際のキーの動作確認とは別です。表示テキストは選択してコピーできます。"});
        panel.Children.Add(feedback); panel.Children.Add(buttons);
        Content=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
}
