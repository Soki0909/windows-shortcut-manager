using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ShortcutManager.App;

public sealed class HelpWindow : Window
{
    private const string Repository = "https://github.com/Soki0909/windows-shortcut-manager";
    public HelpWindow(Action<Window> openGuide, bool showInformation = false)
    {
        Title = "使い方と製品情報"; Width = 750; Height = 620; MinWidth = 480; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var tabs = new TabControl { Margin = new(16) };
        Add(tabs, "はじめに", "サイトやアプリをキーに割り当てる", "1. 「追加」で名前・起動先・Ctrl+Alt+A–Zのキーを登録します。\n2. 「テスト起動」で実際に対象が開くことを確認します。BATやアプリは本当に実行されるので内容を先に確認してください。\n3. 「変更を反映」で自動バックアップ後に保存します。\n4. 必要な場合に確認のうえExplorerを再起動します。\n5. Managerを閉じて物理キーを押し、目的の対象が開くことを確認します。Managerの常駐は不要です。", ("初回ガイドを開く", () => openGuide(this)));
        Add(tabs, "キーが動かない", "症状から確認する", "未保存の編集がある → 「変更を反映」します。単なる候補の編集はWindowsへ保存されません。\n\n新規・変更したキーだけ反応しない → 反映後の確認でExplorerを再起動するか、「Explorerを再起動」を使います。デスクトップ/タスクバーが一時的に消えるため、コピー・移動中は避けます。既に正しく動く場合に再起動する必要はありません。\n\n別の対象が開く・競合が表示される → 「既存をスキャン」で同じキーの元ファイルを調べます。管理外の割当は自動解除しません。別の空きキーを選ぶと安全です。\n\nテスト起動は動くのに物理キーが動かない → 対象は正しくてもExplorerの反映や別アプリのキー登録の影響が残ります。起動診断で応答しないアプリの候補を確認できます。診断はアプリを終了しません。\n\nExplorerの再起動後も解決しない場合にサインアウト/Windows再起動を検討します。保存や再起動成功だけで実キー確認済みにはなりません。");
        Add(tabs, "Chrome", "タブグループだけに拡張が必要です", "通常のWebサイト起動にChrome拡張は不要です。\n\nタブグループにはTab Bridgeを別途導入し、Popupに表示された拡張IDを「設定」へ入力します。Chromeの正しいプロファイルへ導入したかも確認してください。\n\nグループ名は空白を除き全角・半角を正規化した完全一致です。閉じた保存済みグループは開けません。先にChromeで開きます。\n\n物理キーは、折りたたみ中なら展開して前面へ、展開済みなら折りたたみます。前面アプリによる切替ではありません。Popupの「開く」は常に展開します。\n\n更新後は既存拡張を再読み込みし、Popupの実行versionとIDを確認します。ディスク上のファイル更新だけでは実行版の確認になりません。");
        Add(tabs, "復旧", "設定を失わずに復旧する", "バックアップには保存済み設定と管理リンク等を含みます。外部BATの中身、アプリ本体、Chromeのタブ/拡張storageは含みません。未保存の候補編集も保存済み設定とは別です。\n\n「復元」でZIPの内容・不足する外部ファイルを確認し、現在の状態を自動バックアップしてから戻します。管理外の同じキーと競合する場合は復元を停止します。\n\n破損設定のときは自動反映を停止します。まず復元を試すか、元ファイルを保全して修復します。「復旧用初期化」は空の候補を作る操作で、その後に反映すると既存の管理HotKeyが削除されます。\n\n対応しない新版の設定形式はこの版から上書き/初期化せず、対応する新版で開きます。versionの数値だけを手書きで下げないでください。\n\n中断した反映は次回起動時にjournalから復旧します。復旧が止まった場合、退避データやjournalを削除せず、表示された理由を添えて相談してください。");
        var informational = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? BuildIdentity.Version;
        Add(tabs, "製品情報", $"{BuildIdentity.DisplayName} v{BuildIdentity.Version}", $"実行中のアセンブリ: {informational}\n版: {(BuildIdentity.IsDevelopment ? "Debug（設定・保存先は製品版と分離）" : "製品版（Setup / ZIP共通）")}\n設定schema: 1 / Dispatcher protocol: 1\n\n非常駐・Windowsのlnk HotKey方式。外部サーバーへの設定/履歴の自動送信、テレメトリはありません。以下のリンクは通常のブラウザーで開きます。ユーザー名・設定内容・ログを自動添付しません。\n\nソース: MIT License。runtime等の条件は配布物のLICENSE / THIRD-PARTY-NOTICES.md / licensesを参照してください。", ("リリース一覧", () => Open(Repository + "/releases")), ("オンラインヘルプ", () => Open(Repository + "#readme")), ("不具合を報告", () => Open(Repository + "/issues/new/choose")));
        if (showInformation) tabs.SelectedIndex = tabs.Items.Count - 1;
        Content = tabs;
    }

    private void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) { MessageBox.Show(this, $"ブラウザーを開けませんでした。\n{error.Message}", "リンクを開く", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private static void Add(TabControl tabs, string label, string title, string body, params (string Label, Action Action)[] actions)
    {
        var panel = new StackPanel { Margin = new(18) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Margin = new(0,14,0,16) });
        foreach (var action in actions) { var button = new Button { Content = action.Label, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0,4,0,4) }; button.Click += (_, _) => action.Action(); panel.Children.Add(button); }
        tabs.Items.Add(new TabItem { Header = label, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }
}
