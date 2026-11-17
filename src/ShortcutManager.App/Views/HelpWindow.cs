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
        DialogLayout.Constrain(this);
        var tabs = new TabControl { Margin = new(16) };
        Add(tabs, "はじめに", "サイトやアプリをキーに割り当てる", "1. 「追加」で名前・起動先・Ctrl+Alt+A–Zのキーを登録します。\n2. 「対象をテスト」で実際に対象が開くことを確認します。BATやアプリは本当に実行されるので内容を先に確認してください。\n3. 「変更を反映」で自動バックアップ後に保存します。\n4. 必要な場合に確認のうえエクスプローラーを再起動します。\n5. Managerを閉じて実際のキーを押し、目的の対象が開くことを確認します。Managerの常駐は不要です。", ("初回ガイドを開く", () => openGuide(this)));
        Add(tabs, "キーが動かない", "症状から確認する", "未保存の編集がある → 「変更を反映」します。単なる候補の編集はWindowsへ保存されません。\n\n新規・変更したキーだけ反応しない → 反映後の確認でエクスプローラーを再起動するか、「エクスプローラーを再起動」を使います。デスクトップ/タスクバーが一時的に消えるため、コピー・移動中は避けます。既に正しく動く場合に再起動する必要はありません。\n\n別の対象が開く・競合が表示される → 「既存をスキャン」で同じキーの元ファイルを調べます。管理外の割当は自動解除しません。別の空きキーを選ぶと安全です。\n\n「対象をテスト」では動くのに実際のキーが動かない → 対象は正しくてもエクスプローラーの反映や別アプリのキー登録の影響が残ります。起動診断で応答しないアプリの候補を確認できます。診断はアプリを終了しません。\n\nエクスプローラーの再起動後も解決しない場合にサインアウト/Windows再起動を検討します。保存や再起動成功だけで実キー確認済みにはなりません。");
        Add(tabs, "Chrome", "タブグループの導入と確認", ChromeSetupGuide.Text);
        Add(tabs, "復旧", "設定を失わずに復旧する", "バックアップには保存済み設定と管理リンク等を含みます。外部BATの中身、アプリ本体、Chromeのタブ/拡張機能の保存データは含みません。未保存の候補編集も保存済み設定とは別です。\n\n「復元」でZIPの内容・不足する外部ファイルを確認し、現在の状態を自動バックアップしてから戻します。管理外の同じキーと競合する場合は復元を停止します。\n\n破損設定のときは自動反映を停止します。まず復元を試すか、元ファイルを保全して修復します。「復旧用初期化」は空の候補を作る操作で、その後に反映すると既存の管理ショートカットキーが削除されます。\n\n対応しない新版の設定形式はこの版から上書き/初期化せず、対応する新版で開きます。バージョンの数値だけを手書きで下げないでください。\n\n中断した反映は次回起動時に復旧記録から復旧します。復旧が止まった場合、退避データや復旧記録を削除せず、表示された理由を添えて相談してください。");
        Add(tabs,"監査と履歴","自動修復を保留して比較する","「確認と復旧」から、保存済み設定の監査、監査・修復履歴、保存済みの管理リンクの明示修復へ進めます。履歴では実際の値と期待する値、修復前後の差分とバックアップを確認できます。編集中の候補は監査や明示修復で失われません。\n\n次回以降も保留する場合は「次回から監査のみ」を選びます。今回だけ保留して起動するにはスタートメニューの「Shortcut Manager (監査のみ)」を使います。通常のManagerを開いている場合は先に閉じてください。\n\n監査のみの起動は中断した反映の自動復旧も保留します。退避データを確認し、「中断した反映を復旧」で明示操作します。保留した外部変更を保存設定へ逆取り込みしません。\n\n起動先不足・外部競合・スキャンの未確認、同じ差分での修復試行後は自動修復を繰り返しません。詳細を確認してから明示修復します。修復成功やエクスプローラー再起動は実際のキーの確認とは別です。\n\nバックアップ履歴から差分を確認して復元できます。既定は全体復元で、不足・競合があれば停止します。明示的に選んだ場合は問題の割当を残して無効化できます。「今回直前の反映を戻す」は、この起動中の成功した「変更を反映」と対応するバックアップだけが対象です。");
        Add(tabs,"キーボード","マウスを使わずに操作する","Tab / Shift+Tabで操作項目を移動し、EnterまたはSpaceでボタンを実行します。一覧に入ったら上下矢印で割当を選びます。Tabで一覧のセルをすべて巡回せず次の操作へ進めます。\n\n主画面のAlt+A: 追加 / Alt+E: 選択を編集 / Alt+D: 選択を削除 / Alt+T: 対象をテスト / Alt+S: 変更を反映。Alt+Rで「確認と復旧」、Alt+Oで設定、Alt+Hで使い方、Alt+Vで情報を開きます。Altキーを押すと対応文字が表示されます。\n\nメニューは矢印で選び、Enterで実行し、Escapeで閉じます。編集画面ではF1で入力欄の説明、F5でファイル・作業フォルダ・URI登録を再確認します。候補の編集とWindowsへの反映は別です。\n\n「対象をテスト」は実際に起動します。BATやアプリは起動内容を先に確認してください。「変更を反映」はバックアップ後に設定と管理リンクを更新します。無効なボタンの操作は実行されません。\n\nこれらはManagerを開いている間の画面操作です。登録したCtrl+AltのWindows ショートカットキーとは別で、常駐やグローバルキーボードフックは使いません。");
        Add(tabs, "用語", "画面の操作と状態の意味", UiText.Glossary);
        var informational = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? BuildIdentity.Version;
        Add(tabs, "製品情報", $"{BuildIdentity.DisplayName} v{BuildIdentity.Version}", $"実行中のアセンブリ: {informational}\n版: {(BuildIdentity.IsDevelopment ? "開発版（設定・保存先は製品版と分離）" : "製品版（インストーラー / ZIP共通）")}\n設定形式（schema）: 1 / 拡張機能との通信形式（protocol）: 1\n\n非常駐・Windowsショートカット（.lnk）のキー方式。外部サーバーへの設定/履歴の自動送信、テレメトリはありません。以下のリンクは通常のブラウザーで開きます。ユーザー名・設定内容・ログを自動添付しません。\n\nソース: MITライセンス。同梱ランタイム等の条件は配布物のLICENSE / THIRD-PARTY-NOTICES.md / licensesを参照してください。", ("リリース一覧", () => Open(Repository + "/releases")), ("オンラインヘルプ", () => Open(Repository + "#readme")), ("不具合を報告", () => Open(Repository + "/issues/new/choose")));
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
