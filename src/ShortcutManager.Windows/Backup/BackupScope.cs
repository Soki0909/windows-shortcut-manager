namespace ShortcutManager.Windows.Backup;

public static class BackupScope
{
    public const string Description = "含むもの: 保存済みの設定、管理ショートカット、反映状態。未適用の編集は含みません。\n\n含まないもの: 外部BAT/CMDの本文、参照元lnk、アプリ本体、Chrome拡張本体・設定、タブグループやタブの内容。外部ファイルは別に保管してください。移動した場合は参照先を修正してから反映してください。";
}
