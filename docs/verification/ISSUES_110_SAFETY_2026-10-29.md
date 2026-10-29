# 10件の受け入れ条件と実行証拠

2026-10-29 / Windows owner machine / PR #111。製品版は更新していない。

|Issue|条件|証拠|
|---|---|---|
|#110|前面に依存しない2分岐、旧action/Popup互換|Nodeの分岐・protocolテスト、実Chromeのcollapsed反転と旧action/Popupのopen-only|
|#110|実HotKeyでChrome／非Chrome×展開／折りたたみ|所有者の実Ctrl+Alt+Uによる4条件がすべてpassed。実Debug Apply生成lnkの同一Desktopコピーを利用|
|#110|member復帰・他group保全・Dispatcher削除|実Chromeで最後のmemberへの復帰、member件数維持、他group／窓保全、作成Dispatcherの消滅を確認|
|#110|連続・同時要求、実Worker停止復帰、冷間起動|6回連続＋6並列、CDPで実Workerのstoppedを観測して再呼出し、Chrome全終了後のnative起動でnot-found応答とDispatcher消滅|
|#110|不在、同名、複数窓、別group選択|実Chromeのfixtureで確認。保存済みで閉じたgroupを復元しない|
|#2|元失敗と全復旧失敗・復旧先を報告し原本保持|RollbackFailureRetainsEveryOriginalAndReportsAllDestinations：2リンクのアクセス拒否注入、old原本・prior-config・recovery.json保全、再反映停止|
|#4|Backup後の外部競合でconfig/lnk不変|RestoreConflictAfterBackupLeavesConfigLinksAndBackupsUnchanged。GUI/CLIは同じexternal scan → Apply経路|
|#5|Apply/Restore/監査で想定外ファイル保全|AllWriteRoutesRejectUnexpectedFilesWithoutChangingState：txt、任意lnk、生成名に似た管理外lnkを拒否しsentinel/configを保持|
|#6|破損configの保全、空Apply拒否、復旧後のみ再開|CorruptConfigBlocksApplyUntilReloadRestoreOrExplicitInitialization：壊れたJSONと必須項目欠落の`{}`の両方。実Debug UIで反映disabled、初期化導線、修復再読込後enabled。元Debug bytes/hashを復元|
|#7|管理root/marker/reparseを共通検査|OwnershipRejectsDangerousRootInvalidMarkerAndReparsePoint：drive root、相対path、不正marker、symlinkと子、dangling linkと子を拒否しtargetを作成しない|
|#37|null/enum/件数/文字列/総量を位置付き拒否|ConfigSafetyTests。root必須項目欠落を空設定と誤認しない。保存JSONが1 MiBを超える集約設定、既存config/stateの過大スナップショットも書込み前に拒否|
|#38|manifest/version/config hash検証後に書込み|RestoreRejectsTamperingBeforeAnyWriteとBackupSafetyTests。config/manifest変更を拒否し現config/lnk不変|
|#39|ZIP件数・容量・重複・不正構造の上限|BackupSafetyTests：129エントリ、展開合計36 MiB、単一4 MiB超、config1 MiB超、大小文字重複、不正path、破損ZIPを拒否|
|#40|旧ZIPを先に削除しない、途中失敗でも復元可能|FailedReplacementPreservesPreviouslyRestorableBackup：configエントリ書込み途中、ZIP生成後、検証後のIOExceptionで旧bytesとInspect成功を維持。成功時の同名置換も確認|

## 管理フォルダの方針

絶対パスの専用フォルダのみ。drive/share root、OS・Program Files配下、user/profile/desktop/start-menu/programs/LocalAppDataの共有rootとその祖先を拒否する。rootまたは祖先、内部項目のreparse pointとサブフォルダは拒否する。非空ならmarker必須で、内容はShortcut Manager / schemaVersion=1 / 有効なupdatedAtの3行、最大1 KiB。

管理lnkは生成名形式に加え、保存済み設定の名前、または生成元Descriptionを確認する。不明なファイルを自動削除しない。監査、手動Apply、Restoreは同じ事前検査を通る。

## 復旧失敗時

失敗報告のoperationRootにold原本、prior-config/prior-marker/prior-state（存在した場合）、recovery.jsonを残す。元例外と各復旧先のエラーを一覧化し、他の復旧も最後まで試す。pending recoveryがある間は再Apply/Restoreを止める。手動復旧ではこのディレクトリと事前ZIPを先に別の安全な場所へコピーし、報告のdestinationへ原本を戻すか、退避を保全したうえで正常バックアップをRestoreする。原本を確保せずpending recordだけを消さない。

## 実行記録

- `dotnet test ShortcutManager.sln -c Release --no-restore --nologo`
- 最終合格数: Core 27/27、Windows 53/53、Node 12/12（UTF-8/UTF-16 BOM互換回帰を含む）。
- `node --test tests/ChromeExtension.Tests/protocol.test.js`
- `scripts/run-debug.ps1`（通常画面、破損設定画面、修復再読込を実UI確認）
- `dotnet build tests/ShortcutManager.LiveDriver -c Debug`
- `node scripts/test-chrome-live.mjs`（専用CfT、明示環境変数、所有者同意のExplorer再読み込み、所有者実キー入力）
- Live run `chrome-live-1793260987977`: 22 recorded checks passed。生scan/個人パスはgitignored localにのみ保持。

新規Start-menu HotKeyのOS登録がこのExplorer状態で直ちに反映されなかったため、実キーは生成リンクの同一Desktopコピーで検証した。製品のStart-menu出力先は変えておらず、既知のShell再読み込み／Windows再起動による反映条件も変更していない。コピーとテストHotKeyは除去済みで、外部HotKeyの読戻しフィールドは前後一致した。

元の前面条件の失敗証拠と将来調査は#112、最終仕様はAccepted ADR-008。前面判定ランチャーは撤去済み。
