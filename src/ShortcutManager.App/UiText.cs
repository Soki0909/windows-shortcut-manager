namespace ShortcutManager.App;

// Display text only. Config enum values, status codes and diagnostics stay stable.
public static class UiText
{
    public const string ShortcutKey = "ショートカットキー";
    public const string SaveCandidate = "候補に保存";
    public const string Apply = "変更を反映";
    public const string ApplyWithAccessKey = Apply + "(_S)";
    public const string TestTarget = "対象をテスト";
    public const string TestTargetWithAccessKey = TestTarget + "(_T)";
    public const string Website = "Webサイト";
    public const string Application = "アプリ";
    public const string Bat = "BAT / CMD";
    public const string ChromeTabGroup = "Chromeタブグループ";
    public const string Unsaved = "未保存";

    public const string Glossary = "割当: 名前・起動先・ショートカットキーをまとめた1件です。\n\n"
        + "ショートカットキー: Ctrl + Alt + A–Zで対象を呼び出すキーです。Windowsのショートカットファイル（.lnk）が実行を担当します。Managerを閉じても使えます。\n\n"
        + "候補に保存: 編集画面や設定画面の内容を、編集中の候補へ戻します。設定ファイルやWindowsのリンクはまだ変更しません。\n\n"
        + "変更を反映: 編集中の候補を検証し、現在をバックアップしてから設定ファイルと管理リンクへ保存します。Windows側のキー反映や実際のキー動作は別に確認します。\n\n"
        + "未保存: 候補に変更があり、まだ「変更を反映」を行っていない状態です。「保存済み・反映未確認」は保存後の別の状態です。\n\n"
        + "対象をテスト: 登録した対象を実際に起動します。BATやアプリの処理も実行されます。設定の保存やショートカットキーの動作確認とは別です。\n\n"
        + "管理リンク: Managerが管理フォルダに作るWindowsショートカット（.lnk）です。元のファイルや管理外のショートカットとは区別します。\n\n"
        + "監査: 保存済み設定と管理リンクを照合することです。編集中の候補は保持します。修復は管理リンクを保存済み設定に合わせる別の操作です。\n\n"
        + "エクスプローラー: Windowsのデスクトップ・タスクバー・ショートカットを扱う仕組みです。再起動してもキー動作を確認済みにはしません。";
}
