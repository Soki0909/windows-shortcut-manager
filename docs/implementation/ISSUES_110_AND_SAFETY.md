# Issue #110 と安全性改善10件

2026-10-29開始。対象は #110, #2, #4, #5, #6, #7, #37, #38, #39, #40。

## Progress
- [ ] #110: 同じHotKeyで呼出し・折りたたみ。直前フォーカス／選択グループ／単純開閉を比較し実機検証。
- [ ] #2: rollback失敗時の退避・全エラー保全。
- [ ] #4: Restore外部競合の既存実装と回帰を確認。
- [ ] #5: 全書込み経路の想定外ファイル保護。
- [ ] #6: config破損時の復旧モード。
- [ ] #7: 管理境界・marker・reparse point検証。
- [ ] #37: config構造・値・サイズ上限。
- [ ] #38: バックアップ整合性。
- [ ] #39: ZIPの上限・重複・構造検証。
- [ ] #40: 原子的バックアップ。

## Decisions
- #110をユーザー指定の主目的とし、残りはP0のデータ保全と関連P1を優先。
- インストール済み製品は更新しない。故障注入は隔離fixtureのみ。
- #110について所有者は方法を限定せず、フォーカス条件の必要性を含め多角的検討を指示。

## Discoveries
- #4はADR-007で修正済み。独立した受け入れ条件の確認が必要。
- 現行Applyはmarker以外の全ファイルを退避・削除する。
- 現行Restoreはmanifestのconfigハッシュを照合せず、configを無制限に読む。

## Validation Evidence
- Core Debug 24/24、Windows Debug 43/43、Node 12/12合格。
- #2: 複数復元のアクセス拒否を注入し、全退避・旧config・復旧先を保全。復旧未完了の再反映拒否も合格。
- #4: バックアップ後に追加した外部競合でconfig/link/backup件数が変わらない。GUI/CLIの外部競合引渡しも確認。
- #5/#7: 無関係txt、任意lnk、生成名に似たlnk、marker不正、危険root、相対path、symlinkとその子pathを拒否。
- #6: 実際のMainViewModelで読み込み失敗→Apply拒否、修復再読込、Restore、明示初期化を検証。既存linkと破損configを保全。
- #37: null構造、未定義enum、過大文字列／件数／JSONに位置付きエラー。
- #38/#39: 改変config、不一致manifest、重複名（大小文字含む）、不正path、過大config、破損ZIPを拒否し、現config/linkを保全。
- #40: 書込み中と検証後のIOExceptionでも既存同名バックアップを保持し再検証可能。成功時の置換も合格。
- #110: 起動モード・protocol・Workerモック統合は合格。実HotKeyは未完了。
- run-debug.ps1でウィンドウ生成まで確認したが、画面操作ツールがWindowsブラウザーURLを確定できずターン内の操作停止を要求したため視覚確認は未完了。
- 製品版・個人Chromeは未更新。Debug configも未作成。専用CfTとDebug窓は再開用に残っている。
- 最終Release: Core 24/24、Windows 43/43。Node 12/12再実行合格。Release package生成、SHA256再照合、ZIP内toggle module存在／live harness除外を確認。
- 継続ターン: PR #111 CI run 40830979235はdotnet/extensionとも合格。Debug窓をRaiseした後の画像でDEBUGタイトル・D badge・空のキー枠・反映ボタン・別管理パスを確認（前面化前の誤った画像は証拠から除外）。
- 隔離CfTは依然ブラウザーURL判定で画面操作停止。HotKeyランチャーが同一テストプロファイルへ配送される条件も追加確認が必要（通常Chromeでは既定プロファイルへの配送契約を維持）。
