# ADR-008: Chromeグループ呼出し時の一時起動モード

Status: Proposed / 実HotKey検証待ち（2026-10-29）

## Intent and alternatives

#110は別アプリから対象を呼び出し、Chrome作業中には同じキーで展開／折りたたみする。所有者は方式を限定せず、フォーカス条件の必要性も含む多角的検討を指示した。

|方式|評価|
|---|---|
|collapsedだけで反転|別アプリから表示したい対象が展開済みだと隠れてしまう|
|選択中groupなら畳む|別groupからの移動は自然だが、Chrome背面でも対象タブが選択済みなら誤って隠す|
|Extensionのfocusイベント履歴|Dispatcher起動自体がfocusを変え、イベントに起動要求との因果関係がなく、Worker復帰を含め確定できない|
|Chrome起動前に前面ウィンドウを取得|Chrome起動によるfocus変更と分離できる。一時プロセスと実HotKey検証が必要|

## Proposed implementation

- Chromeグループ用lnkは同梱Manager EXEの`--dispatch-group <chrome.exe> <extensionId> <groupName>`を起動する。このモードはウィンドウ、設定ロード、監査を起動しない。
- `GetForegroundWindow`のプロセス実行パスを設定のChromeと比較してからDispatcherを起動し終了する。hook、Native Messaging、常駐プロセス、localhostは追加しない。
- 設定されたChromeの全ウィンドウを対象にする。別group・別窓で作業中でも展開済み対象を畳むというIssueの表を維持する。
- 取得不能は非Chrome扱いで表示を優先。判定点は一時プロセス起動時であり、物理キー押下時刻そのものや、その間に利用者が窓を切り替える競合の完全排除は約束しない。
- Protocol v1へ新action `toggle-group`を追加し、`foreground=chrome|other`を必須とする。既存`focus-group`とPopupの「開く」は常に展開する。
- 拡張未更新では新actionは拒否される。対応拡張導入後、ManagerでApplyしてlnkを再生成する。開発中にインストール済み製品は更新しない。
- 対象は正規化名一致、最後のnormal window優先、次にwindowId/groupId順という既存ルール。対象不在はnot-found、閉じた保存済みgroupは復元しない。
- 畳むときは対象外タブへ移る。対象外タブがなければabout:blankを1つ作る。対象memberを再activeにせずDispatcher削除で再展開することを防ぐ。タブ／groupを削除しない。
- dispatch全体を直列化。Worker再生成後も要求内のforeground値を使い、時間差で推測しない。

## Evidence and remaining gates

- [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow)は現在前面のウィンドウを取得し、切替中はNULLになり得る。
- [Chrome windows API](https://developer.chrome.com/docs/extensions/reference/api/windows)はcurrentとfocusedの違いを説明する。focusイベントは起動前状態のスナップショットを提供しない。
- Node: 3分岐、互換性、連続要求、Workerモジュール再生成、対象外タブなしの退避を検証済み。
- Windows: 一時モードの起動仕様、引用符／末尾backslashの引数、旧resolver互換を検証済み。
- 未完了: 実HotKey、Dispatcher削除後の状態、実Worker停止復帰、同名group・複数窓、対象なし・Chrome未起動。専用CfTを準備したが画面操作ツールのURL判定エラーで停止した。未実行項目を合格扱いしない。
