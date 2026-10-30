# Shortcut Manager

v0.3.0では、タブグループHotKeyが前面アプリに関係なく展開・折りたたみを切り替えます。旧Dispatcherのfocus-groupと拡張Popupの「開く」は従来どおりです。新Managerの起動時監査／Applyでリンクが再生成されるため、対応拡張を先に更新してください。インストール済み製品は開発ビルドで自動更新しません。

Windows の `Ctrl+Alt+A-Z` を、常駐プロセスなしで Website / Application / BAT・CMD / Chrome タブグループへ割り当てるローカル管理ツールです。Windows Shell の Start Menu `.lnk` HotKey を利用するため、Managerを閉じてもショートカットは動き続けます。

## クイックスタート

1. `ShortcutManager-Setup-win-x64-v0.3.0.exe` を実行してインストールし、スタートメニューの「Shortcut Manager」から起動します。従来のportable ZIPを展開して起動する方法も利用できます。
2. Chromeを使う場合は `ShortcutManager-TabBridge-v0.3.0.zip` を展開し、`chrome://extensions` のデベロッパーモードからフォルダを読み込みます。
3. Extension Popupに表示される32文字のIDをManagerの「設定」に入力します。
4. 「既存をスキャン」から既存 `.lnk/.url` をインポートするか、「追加」で登録します。
5. 「テスト起動」でtargetを確認し、「変更を反映」を実行します。続く確認でExplorerの再起動を選ぶと、Windowsを再起動せず新しいHotKeyが有効になります。
6. Managerを閉じ、登録した `Ctrl+Alt+キー` を使用します。

Applyは、検証 → 外部競合scan → staging生成 → `.lnk` read-back検証 → 自動backup → ownership確認 → commit の順に動作し、失敗時は以前のconfigと管理 `.lnk` を復元します。Markerのない非空フォルダや管理外shortcutは変更しません。

HotKeyの変更にはExplorerの登録更新が必要です。反映を後回しにした場合は「Explorerを再起動」から実行できます。タスクバーとデスクトップが一時的に消え、開いているExplorer画面が閉じる可能性があるため、ファイルのコピーや移動が終わってから実行してください。WindowsがShellを自動復帰するのを待つことで、余分なファイル管理ウィンドウの起動を避けます。

## HotKeyだけが遅い場合

「起動診断」で、Windows Shellの応答待ちに関係し得る候補を確認できます。診断は手動・読み取り専用で、他アプリの自動終了や外部送信を行いません。候補は原因の断定ではありません。作業を保存して通常終了し、同じリンクの直接実行と実キーの反応を比較してください。詳細は [起動遅延の検証](docs/verification/LATENCY_2026-10-30.md) を参照してください。

## 起動時の自動監査・修復

Applyが中断していた場合は、保存したジャーナルから以前の完全な状態へ復旧してから監査します。復旧データの破損や中断後の手動変更が見つかった場合は、退避データを残して停止します。旧版Managerは終了してから更新してください。互換性と更新順序は [v0.3.0リリースノート](docs/releases/v0.3.0.md) を参照してください。

起動時に、保存済みconfig.jsonと管理対象の実際の `.lnk` を照合します。欠落、HotKey・起動先・引数などの改変、設定にない古い管理リンクを検出すると、自動バックアップを作成してから修復します。一致していれば書き込みは行いません。修復後に表示される確認からExplorerを再起動できます。

管理外HotKeyの競合、管理フォルダの所有権異常、無効なconfig、想定外のファイルがある場合は、自動修復を中止して状態欄に理由を表示します。監査はファイル上の設定を対象とし、ほかの常駐アプリが取得するキーやExplorer内部の現在の登録表までは列挙できません。

## インストール・更新・アンインストール

インストール先は `%LOCALAPPDATA%\Programs\Shortcut Manager`。管理者権限や別途.NET Runtimeの導入は不要です。同じインストーラーを再実行するとアプリを更新でき、設定とバックアップを引き継ぎます。デスクトップアイコンは任意です。

Windowsの「インストールされているアプリ」からアンインストールできます。設定・バックアップ・管理HotKeyは保持するため、アンインストール後も登録済みキーは動き、再インストールすれば設定を再利用できます。Chrome拡張機能の導入は別途必要です。配布EXEは現時点で未署名です。

## 開発

普段はスタートメニューの「Shortcut Manager」（インストール済み製品版）を使います。開発確認はリポジトリから次を実行してください。

```powershell
./scripts/run-debug.ps1
```

`ShortcutManager.Debug.exe` が起動し、タイトルに `[DEBUG]`、ロゴにオレンジ色の D が表示されます。設定・バックアップは `%LOCALAPPDATA%\ShortcutManager.Debug`、管理ショートカットはスタートメニュー配下の `Shortcut Manager Debug` に分離します。初回は空の設定で起動し、製品版の設定を自動では読み込みません。コピーした設定に製品版の管理フォルダ指定があっても、Debug版は専用フォルダを使います。

WindowsのHotKey自体は共通なので、製品版で使用中のキーはDebug版では競合として拒否されます。`run-debug.ps1 -NoLaunch` はビルドのみを行います。製品版の更新はリリースのSetupで行い、開発ビルドではインストール先を上書きしません。配布用 `package.ps1` はReleaseのみを受け付けます。

要件: Windows、.NET 10 SDK、Node.js 22。

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/package.ps1 -Version 0.3.0
# インストーラーを生成する場合（Inno Setupはビルド用のみ）
./scripts/prepare-installer-tools.ps1
./scripts/package-installer.ps1 -Version 0.3.0 -SkipPublish
```

成果物は `artifacts/` に生成されます。Chrome Extensionはbuild stepを持たないMV3/Vanilla JavaScriptです。

## 保存先

- config: `%LOCALAPPDATA%\ShortcutManager\config.json`
- logs: `%LOCALAPPDATA%\ShortcutManager\logs\shortcut-manager.log`
- backups: `%LOCALAPPDATA%\ShortcutManager\backups\`
- managed shortcuts: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Shortcut Manager\`

個人固有のbootstrap configやscan結果はgitignoredの `local/` に置いてください。テレメトリ、外部backend、cloud syncはありません。

## 制限

- 実行中プロセスが `RegisterHotKey` で取得した全HotKeyは静的列挙できません。Apply後の実機テストが必要です。
- Chromeで保存済みでも閉じているタブグループはChrome公開APIから再オープンできません。
- v0.3.0のWindows ZIPは未署名です。SmartScreenの警告対象になり得ます。
- Chrome Web Store / Microsoft Storeへの公開とコード署名は、所有者アカウント・credentialが必要な別工程です。

詳細は [要件](docs/requirements/shortcut-manager-requirements-v1.0.md)、[設計](docs/design/02_SYSTEM_ARCHITECTURE.md)、[検証記録](docs/verification/VERIFICATION.md) を参照してください。
