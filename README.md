# Shortcut Manager

Windows の `Ctrl+Alt+A-Z` を、常駐プロセスなしで Website / Application / BAT・CMD / Chrome タブグループへ割り当てるローカル管理ツールです。Windows Shell の Start Menu `.lnk` HotKey を利用するため、Managerを閉じてもショートカットは動き続けます。

## クイックスタート

1. GitHub Release の `ShortcutManager-win-x64-v0.1.0.zip` を展開し、`ShortcutManager.exe` を起動します。
2. Chromeを使う場合は `ShortcutManager-TabBridge-v0.1.0.zip` を展開し、`chrome://extensions` のデベロッパーモードからフォルダを読み込みます。
3. Extension Popupに表示される32文字のIDをManagerの「設定」に入力します。
4. 「既存をスキャン」から既存 `.lnk/.url` をインポートするか、「追加」で登録します。
5. 「テスト起動」でtargetを確認し、「変更を反映」を実行します。続く確認でExplorerの再起動を選ぶと、Windowsを再起動せず新しいHotKeyが有効になります。
6. Managerを閉じ、登録した `Ctrl+Alt+キー` を使用します。

Applyは、検証 → 外部競合scan → staging生成 → `.lnk` read-back検証 → 自動backup → ownership確認 → commit の順に動作し、失敗時は以前のconfigと管理 `.lnk` を復元します。Markerのない非空フォルダや管理外shortcutは変更しません。

HotKeyの変更にはExplorerの登録更新が必要です。反映を後回しにした場合は「Explorerを再起動」から実行できます。タスクバーとデスクトップが一時的に消え、開いているExplorer画面が閉じる可能性があるため、ファイルのコピーや移動が終わってから実行してください。WindowsがShellを自動復帰するのを待つことで、余分なファイル管理ウィンドウの起動を避けます。

## 開発

要件: Windows、.NET 10 SDK、Node.js 22。

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/package.ps1 -Version 0.1.0
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
- v0.1.0のWindows ZIPは未署名です。SmartScreenの警告対象になり得ます。
- Chrome Web Store / Microsoft Storeへの公開とコード署名は、所有者アカウント・credentialが必要な別工程です。

詳細は [要件](docs/requirements/shortcut-manager-requirements-v1.0.md)、[設計](docs/design/02_SYSTEM_ARCHITECTURE.md)、[検証記録](docs/verification/VERIFICATION.md) を参照してください。
