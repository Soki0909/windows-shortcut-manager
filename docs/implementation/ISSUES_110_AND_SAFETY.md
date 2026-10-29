# Issue #110 と安全性改善10件

対象: #110, #2, #4, #5, #6, #7, #37, #38, #39, #40。PR #111。

## Progress

以下は実装・検証の状態。GitHubの完了状態はPRマージ後に別途確認する。

- [x] #110: 所有者承認の前面条件なしtoggle。実キー4条件、実Chrome、Worker停止・復帰、冷間起動を検証。
- [x] #2: rollback失敗時に全原本・復旧先・元例外と復旧例外を保全し、未復旧の再反映を停止。
- [x] #4: Restore外部競合の共有経路と、バックアップ後に追加した競合を拒否する回帰。
- [x] #5: Apply/Restore/監査で無関係ファイルと管理外lnkを保護。
- [x] #6: 破損設定で反映停止。修復再読込、Restore、明示初期化、実UIの無効ボタンを検証。
- [x] #7: 管理root、marker内容、通常／dangling reparse pointを全管理書込み経路で事前検査。
- [x] #37: null・enum・文字数・件数・保存JSON総容量を位置付きエラーで拒否。
- [x] #38: manifest/configハッシュを照合してからRestore。
- [x] #39: ZIPの件数、重複名、不正構造、個別・合計容量を制限。
- [x] #40: 一時ZIPの検証・flush後に置換。圧縮途中／置換前の失敗でも旧ZIPを保持。

## Decisions

- 所有者指定の#110を主目的とし、残り9件はP0のデータ保全と関連P1を優先した。
- #110の元の前面条件は実HotKeyで成立せず、所有者が2026-10-29に前面条件なしの開閉へ変更し、P3の#112へ分離するよう指示。要件追補とAccepted ADR-008へ反映した。
- 試作の前面判定ランチャーは撤去。製品は従来のlnk → chrome.exe → Dispatcherのみを使う。
- 旧focus-groupとPopupは開く専用。新Managerの起動監査／Applyでtoggleへ再生成されるため、対応拡張を先に導入する。
- 製品版のインストール、個人Chrome・製品設定の更新は行わない。故障注入は隔離fixture、UI確認はDebug、Chromeは専用CfTプロファイル。

## Discoveries

- #4の外部競合引渡しはADR-007で修正済みだった。独立回帰を追加し、競合前の不要なRestoreバックアップ作成も除いた。
- 着手時のApplyは想定外ファイルを退避後に削除し、rollback自体の失敗でも原本退避先を削除し得た。着手時のRestoreはconfigハッシュを照合せず無制限に読んでいた。
- 実HotKeyでは起動時の前面PIDがExplorerになる。直接Shell起動では再現しないため、起動後の前面取得を押下直前の証拠にできない。#112に証拠を保存した。
- テスト用新規Start-menu HotKeyがOS登録へ即時反映されない環境条件があり、実キー検証は生成lnkのバイト同一Desktopコピーで実施した。ユーザー承認のExplorer再起動と、所有を確認したテストコピーの削除で後片付けした。製品の管理フォルダ方式は変更していない。

## Validation Evidence

- 詳細な要件対応表: [ISSUES_110_SAFETY_2026-10-29.md](../verification/ISSUES_110_SAFETY_2026-10-29.md)。
- 最終Release Core 26/26、Windows 53/53、Node 12/12合格。必須root項目欠落の有効JSONも空設定へ変換せず、復旧モードへ移す。UTF-8/UTF-16 BOM付き設定の復元互換も維持する。
- Live `chrome-live-1793260987977`: 22記録がすべてpassed。実キー4条件、member保全、他group／複数窓、連続・同時要求、実Worker停止・復帰、冷間起動、Popup／旧action互換、生成lnk直接実行、テストキー除去、外部HotKeyフィールド不変。
- Debugの破損設定UIで反映disabledと初期化導線を描画確認。元の設定をバイト単位で戻し、再読込で復旧モード解除・反映enabledを確認し終了した。
- 最終ZIP/Setup生成、3ハッシュ照合、両EXEの7アイコンフレーム、配布EXEのcommand-mode smokeが合格。CIと完了状態はPR #111のChecksとIssue状態を正本とする。未公開ローカル成果物を公開済みv0.1.1と混同しない。
