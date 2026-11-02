using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ShortcutManager.Core.Models;

namespace ShortcutManager.App;

public sealed class GettingStartedWindow : Window
{
    private readonly MainViewModel model;
    private readonly Button test = new() { Content = "登録したWebサイトを開く" };
    private readonly Button apply = new() { Content = "変更を反映する" };
    private ShortcutRow? Website => model.Selected?.Definition.Target is WebsiteTarget ? model.Selected : model.Shortcuts.FirstOrDefault(row => row.Definition.Target is WebsiteTarget);

    public GettingStartedWindow(MainViewModel model, Func<Window, bool> addWebsite)
    {
        this.model = model;
        Title = "初回ガイド — 最初のショートカット";
        Width = 690; Height = 690; MinWidth = 480; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = new StackPanel { Margin = new(22) };
        content.Children.Add(new TextBlock { Text = "Webサイトを1件登録してみましょう", FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(Text("いつでも閉じて、使い方や空の一覧から再開できます。ガイドを開くだけで設定や既存の割当は変更されません。"));
        var add = new Button { Content = "Webサイトを追加" };
        add.Click += (_, _) => { if (addWebsite(this)) UpdateButtons(); };
        Step(content, "1  名前・URL・キーを登録", "例: 名前を「サンプル」、URLを https://example.com にします。まだWindowsへは保存されません。", add);
        test.Click += (_, _) => { if (Website is { } row) { model.Selected = row; model.Test(row.Definition); } };
        Step(content, "2  登録した対象をテスト", "ボタンを押すと実際にブラウザーが開きます。これは起動先の確認です。WindowsのHotKeyの動作確認とは別です。", test);
        apply.Click += async (_, _) =>
        {
            if (!model.IsDirty || model.IsBusy || model.RecoveryRequired) return;
            if (await model.ApplyAsync() && MessageBox.Show(this, MainWindow.ShellReloadExplanation, "Windows Explorerを再起動", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await model.ReloadShellAsync();
            UpdateButtons();
        };
        Step(content, "3  変更を反映して有効化を試す", "現在の状態を自動バックアップして、Windowsのショートカットへ反映します。新しいキーが反応しない場合にExplorerの再起動を試せます。確認で「いいえ」を選んでも保存した設定は残ります。", apply);
        Step(content, "4  ガイドとアプリを閉じ、設定したキーを押す", "Ctrl + Alt + 選んだ文字キーで目的のサイトが開くか、自分で確認してください。保存やテスト起動だけで確認済みとは判断しません。動かない場合は「使い方」の対処手順を確認します。", null);
        var status = Text(string.Empty); status.SetBinding(TextBlock.TextProperty, new Binding(nameof(MainViewModel.Status)) { Source = model }); content.Children.Add(status);
        var close = new Button { Content = "閉じる・あとで再開", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        content.Children.Add(close);
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.SetBinding(IsEnabledProperty, new Binding(nameof(MainViewModel.CanInteract)) { Source = model }); Content = scroll;
        model.PropertyChanged += ModelChanged;
        Closed += (_, _) => model.PropertyChanged -= ModelChanged;
        Closing += (_, e) => { if (model.IsBusy) e.Cancel = true; };
        UpdateButtons();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e) => UpdateButtons();
    private void UpdateButtons() { test.IsEnabled = Website is not null && !model.RecoveryRequired && !model.IsBusy; apply.IsEnabled = model.IsDirty && !model.RecoveryRequired && !model.IsBusy; }
    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new(0,6,0,8) };
    private static void Step(Panel panel, string title, string body, Button? action)
    {
        panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0,14,0,0), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Text(body)); if (action is not null) { action.HorizontalAlignment = HorizontalAlignment.Left; panel.Children.Add(action); }
    }
}
