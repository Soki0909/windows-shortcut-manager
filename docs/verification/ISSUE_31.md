# 既存ショートカットのキーを手動で移行する（#31）

「既存をスキャン」で移行元を選び、「元のキーを手動で解除…」を開く。元のパスとキーを確認して復旧用コピーを保存すると、Windowsのプロパティを開ける。キー欄を「なし」にして保存し、再スキャンする。元キーと外部競合が解消すると移行画面に戻り、既存の「選んだキーで候補へ追加」から通常の反映へ進める。

復旧用ZIPは受入環境のローカルデータ内 `migration-source-backups` に新規保存する。`original/` 内の元ファイルデータと `migration-source.json` の元パス・キー・SHA-256でコピーを識別する。Windowsのファイル属性やACLはZIPの復元対象に含まない。元へ戻す場合は、移行先で同じキーを反映済みならそちらを先に変更して反映し、利用者が元のパスを照合してファイルを戻す。

## 2026-11-18 の確認結果

- Windowsの SDK 10.0.404、Debugで、`dotnet test tests/ShortcutManager.Windows.Tests/ShortcutManager.Windows.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~ManualHotkeyMigrationTests|FullyQualifiedName~InventoryMigrationTests|FullyQualifiedName~SkippedInventoryScanDoesNotReturnPreviousReport' --nologo --disable-build-servers` は14件成功、失敗・スキップ0件。
- `.lnk` と `.url` の原本バイトとキーの保持、復旧ZIPのハッシュ照合、利用者の操作を模したキー解除後の再スキャン、復旧ファイルからの手動復元を隔離フォルダーで確認した。
- 元キーの残存、別ファイルの同キー、不明な `.url` のキー値、一部未走査、スキャン後の原本変更、保存失敗、別版と所有マーカー付きフォルダーで継続を拒否することを確認した。
- 再スキャン処理にはその呼び出しが返したレポートだけを使用する。前回レポートを保持している状態で別の走査が処理中の場合、新しい呼び出しがレポートを返さないことを確認した。
- 日本語Windowsで隔離した `.lnk` と `.url` を使い、WPFの案内で復旧用コピーを保存してから `SHObjectProperties` でファイルのプロパティを開いた。両方のキー欄を「なし」にして保存し、WPFの再スキャンで解除を確認した。復旧ZIPから手動で戻した両ファイルのSHA-256は解除前と一致した。物理キーは#54で確認する。プロパティを呼び出せない場合は同じファイルの場所を表示できる。[Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shobjectproperties)
- PR #177をmain `204b25a` に統合し、[統合後CI 44289337197](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/44289337197) の成功を確認した。
