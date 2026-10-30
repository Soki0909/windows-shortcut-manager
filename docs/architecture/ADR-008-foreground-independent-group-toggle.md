# ADR-008: 前面アプリに依存しないChromeグループの開閉

Status: Accepted — owner approved 2026-10-29 (#110)。元の前面条件はP3の[#112](https://github.com/Soki0909/windows-shortcut-manager/issues/112)へ分離。

## Decision

- 新しい管理lnkは、既存の`chrome.exe → Dispatcher`経路でProtocol v1の`action=toggle-group`を呼ぶ。Manager非常駐、hookなし、Native Messaging/localhost/Named Pipeなしを維持する。
- 対象がcollapsedなら展開・前面化・最後のmemberタブへ復帰、expandedなら対象だけを折りたたむ。前面アプリを判定しない。別アプリから展開済み対象を表示したい場合は2回押すことになると説明し、所有者が承認した。
- 旧`focus-group`とPopupの「開く」は常に展開する。未再生成の旧lnkも従来動作を保つ。新Managerの起動時監査またはApplyで新actionへ再生成されるため、対応拡張を先に読み込む。古い拡張は新actionをinvalid-actionで拒否する。
- 対象の選択は従来の正規化名完全一致、最後のnormal Chrome window優先、次にwindowId/groupId順を維持する。Popupの同名実体選択は#48の別課題。
- 畳む前に対象外タブへ移り、Dispatcher削除でmemberが再activeになって展開し直すことを防ぐ。対象外タブがなければabout:blankを1つ作る。対象タブ・他groupは削除しない。
- dispatch操作をWorker内で直列化する。Worker停止後の復帰にも古い前面履歴や時間差の推測を使わない。
- 対象不在はgroup-not-found。Chrome起動で復元された開いているgroupは利用できるが、保存済みで閉じたgroupを直接再オープンしない。

## Rejected prototype and evidence

短命のネイティブランチャーでChrome起動前のGetForegroundWindowを読む案を試作した。しかし実HotKeyではその時点の前面PIDがExplorerだった。専用Chromeのページ内ボタンを押してから入力したケースでも同じだった。一方、同じlnkの直接Shell実行ではChrome前面／非前面の判定が通った。直接起動のテストだけでは押下直前の状態を証明できない。

そのため試作ランチャーを製品から撤去した。Windows標準のlnk HotKey・Chrome Dispatcherという既存境界を保ち、前面条件を将来課題へ分離することを所有者が選んだ。#112で公開APIと因果関係を再調査し、常駐やhook等への変更が必要なら先に所有者承認と別ADRを取る。

参考: [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow)、[Chrome windows API](https://developer.chrome.com/docs/extensions/reference/api/windows)。これらは現在の状態を取得できても、Shell起動前のスナップショットを保証しない。

## Development verification

Debug限定の明示CLI指定`--chrome-test-profile <path>`で専用Chrome for Testingへ配送できる。製品設定にプロファイルを追加せず、Releaseはこの指定をresolverへ渡さない。Live fixture・入力ドライバー・応答観測はテストのみで、配布ZIPに含めない。

実行証拠は[VERIFICATION](../verification/VERIFICATION.md)と[10件の追跡](../implementation/ISSUES_110_AND_SAFETY.md)へ記録する。インストール済み製品はこの開発では更新しない。
