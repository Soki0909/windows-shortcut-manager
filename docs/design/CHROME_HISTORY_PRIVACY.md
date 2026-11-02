# タブ復帰履歴の管理

対象: #49。既定のlast-used復帰を維持し、URL補助を保存しない選択と明示消去を提供する。

- 保存先はChromeプロファイル内の拡張`storage.local`。正規化したグループ名をキーに、tabId・任意のURL・savedAtを新しい順に最大100グループ保持する。外部送信、storage.sync、ページ本文の収集はしない。
- 既存設定で`rememberTabUrls`がない場合は従来のURL補助を維持する。falseなら既存mapからURLを除去して設定と同じstorage.setで保存する。以降はURLを記録/復帰候補として読まない。trueへ戻した場合、過去のURLを復元せず以降の操作で記録する。
- 消去はmapだけを空にし、他設定・開いたタブ/グループ・Chromeの閲覧履歴を変更しない。以降のタブ操作は再記録されるので、そのこととURL保存の抑制をPopupで説明する。
- activationの発生時に、非同期tabs.getより前に履歴queueへ順番を確保する。remember・設定のpatch・消去・件数取得を同じqueueで実行し、先行する記録が遅れて消去/無効化を取り消さない。Popupは変更したbooleanだけをWorkerへ送り、古い設定全体を上書きしない。
- WorkerのPopup応答はboolean設定と件数だけで、履歴のグループ名/URLを返さない。消去にはPopup内の確認チェックが必要。読み書き失敗を成功扱いせず、設定変更を止めて「更新」から再読込みする。自動再送しない。
- 診断表示のdebugModeは明示選択を維持する。診断へ任意のresponse/例外本文を直列化せず、okと検証したcodeだけを表示する。URL履歴の診断出力は提供しない。

manifestの権限、Dispatcher protocol、開閉と前面アプリの扱いは変更しない。Chrome Web Storeのprivacy説明にも同じ保存範囲・消去/抑制を反映する。ManagerバックアップにChromeのこのstorageは含まれない。
