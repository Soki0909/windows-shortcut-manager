# 既存ショートカットのキーを手動で移行する（#31）

「既存をスキャン」で移行元を選び、「元のキーを手動で解除…」を開く。元のパスとキーを確認して復旧用コピーを保存すると、Windowsのプロパティを開ける。キー欄を「なし」にして保存し、再スキャンする。元キーと外部競合が解消すると移行画面に戻り、既存の「選んだキーで候補へ追加」から通常の反映へ進める。

復旧用ZIPは受入環境のローカルデータ内 `migration-source-backups` に新規保存する。`original/` 内の元ファイルデータと `migration-source.json` の元パス・キー・SHA-256でコピーを識別する。Windowsのファイル属性やACLはZIPの復元対象に含まない。元へ戻す場合は、移行先で同じキーを反映済みならそちらを先に変更して反映し、利用者が元のパスを照合してファイルを戻す。

## 2026-11-18 の確認結果

- Windowsの SDK 10.0.404、Debugで、`dotnet test tests/ShortcutManager.Windows.Tests/ShortcutManager.Windows.Tests.csproj -c Debug --filter 'FullyQualifiedName~ManualHotkeyMigrationTests|FullyQualifiedName~InventoryMigrationTests' --nologo --disable-build-servers` は13件成功、失敗・スキップ0件。
- `.lnk` と `.url` の原本バイトとキーの保持、復旧ZIPのハッシュ照合、利用者の操作を模したキー解除後の再スキャン、復旧ファイルからの手動復元を隔離フォルダーで確認した。
- 元キーの残存、別ファイルの同キー、不明な `.url` のキー値、一部未走査、スキャン後の原本変更、保存失敗、別版と所有マーカー付きフォルダーで継続を拒否することを確認した。
- Windowsプロパティの入口には Unicode パス対応の `SHObjectProperties` を使う。呼び出せない場合は同じファイルの場所を表示する導線を使える。OSのプロパティ画面での実操作と物理キー確認は未実施。[Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shobjectproperties)
