# #52 ローカルデータ削除候補と削除前アーカイブ

2026-11-18。対象は [任意削除の設計](../design/OPTIONAL_REMOVAL.md) の候補分類と削除前アーカイブ。アプリ画面への接続、退避ジャーナル、移動・消去・復元は後続作業。

## 判定と保全

- 現エディションの保存先と、設定・前回の反映状態が記録する管理先を確認する。開発版は設定の管理先上書きを無視する。別エディションと重なるパス、マーカーが不正な管理先、未解除の管理リンク、有効な割当、未保存の変更、回復が必要な反映・復元記録があれば後続の確認を止める。
- 設定、反映状態、監査の記録、ログは既知の保存先と形式を照合する。監査履歴は画面の表示上限にかかわらず、フォルダの直下を列挙する。ログの全行を記録形式と照合できない場合は保持する。分類は保存先と形式に基づくため、同一Windowsユーザーがファイルを作り替えた場合の認証に使わない。
- 自動バックアップは検証済みのZIPと自動作成記録を一組で表示する。読める手動・旧形式バックアップには個別選択が要る。壊れたZIP、自動作成記録だけのファイル、不明な項目は保持する。
- Win32でパスの実体とファイルのリンク数を読み取る。reparse pointの配下や複数のハードリンクを持つファイルは候補にしない。管理フォルダとマーカー、処理用フォルダの既存データは保持する。 [Windowsの最終パス照会](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew) [ファイル実体情報](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle)

## 検証

- Windows向けの隔離テストでは、一時フォルダ内の設定、管理先、バックアップ、監査記録、ログを使用する。未知ファイル、履歴上限を超えた監査記録、旧管理先の残留リンク、他エディション、マーカー、ハードリンクとreparse point、状態の不一致と中断記録を検証する。候補に含む既存ファイルと保持対象のハッシュがプレビュー前後で一致することも確認する。
- SDK 10.0.404、Release で `LocalDataRemovalPreviewTests`、`AuditControlTests`、`BackupRetentionTests` を実行し、28件成功・失敗0・スキップ0。初回は既存のNuGetキャッシュから復元し、最終差分の再検証は `--no-restore` を指定した。インストール済み製品や個人の設定はテストに使っていない。

## 削除前アーカイブ

- 同じWindowsユーザー、エディション、管理先、候補、ファイルの実体・ハッシュを再確認する。個別指定のバックアップを検査し、保存先とその親がデータ保存先や管理先へ重ならないことを確かめる。
- 新規の一時ファイルに書き、確定前にZIPの一覧と各項目のサイズ・ハッシュを読み戻す。書込みに使用したファイルハンドルで検証済みの一時ファイルを改名する。別の処理が保存先を作った場合は既存ファイルを保護する。
- SDK 10.0.404、Releaseで `LocalDataRemovalArchiveTests`、`LocalDataRemovalPreviewTests`、`AuditControlTests`、`BackupRetentionTests` の対象40件が成功した。保存先競合、同じ長さの内容変更、確認後の置換、意図的な書込み失敗を隔離フィクスチャで検証した。本体から利用する際にはインスタンスリースと、退避・復元のジャーナルの検証が必要。

## 退避・取り消し・確定消去・復元・GUI（2026-11-21）

- [PR #181](https://github.com/Soki0909/windows-shortcut-manager/pull/181)（[CI 44881410187](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/44881410187) 成功後、main `ef5a0d4` へ統合）で、`LocalDataRemovalStaging`、`RemovalJournal`、`LocalDataRemovalRestore`、起動時未完了削除ゲート（`App.xaml.cs`）、解除前バックアップ検証付き全キー解除（`MainViewModel.DisableAllForRemovalAsync`）、および「削除の準備・データ削除と復元…」GUI（`RemovalPreparationWindow.cs`）を実装した。
- 親ディレクトリロック＋同一ハンドル移動（`FileRenameInfo`）＋同一ハンドル削除（`FileDispositionInfo`）により、ハッシュ検証後の差替え（TOCTOU）を防止した。退避・取り消し・確定消去の各チェックポイントおよび `ShortcutManager.CrashDriver` による実プロセスキル（`file-staged` / `file-restored` / `file-deleted`）からの再開、アーカイブ改変・欠損時の停止、削除前アーカイブからの復元（エディション一致・既存競合停止・旧 `state.json` 除外・中断時ロールバック）を自動テストで検証した。
- ローカル `dotnet build ShortcutManager.sln -c Debug` 0警告・0エラー、`LocalDataRemoval` テスト 62/62（関連 `EditorStateTests` 含む 84/84）成功、PR CI `44881410187`・main 統合後CI [44881815123](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/44881815123) 成功。
- 正式候補での独立検証機における代表の物理キー操作と削除後に残る項目の実機確認（#54と連動）が残るため、Issue #52 は open を維持する。
