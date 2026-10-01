# Shortcut Manager

Windows の `Ctrl+Alt+A-Z` を、常駐プロセスなしで Website / Application / BAT・CMD / Chrome タブグループへ割り当てるローカル管理ツールです。Windows Shell の Start Menu `.lnk` HotKey を利用するため、Managerを閉じてもショートカットは動き続けます。

## クイックスタート

1. GitHub Release の `ShortcutManager-win-x64-v0.1.0.zip` を展開し、`ShortcutManager.exe` を起動します。
2. Chromeを使う場合は `ShortcutManager-TabBridge-v0.1.0.zip` を展開し、`chrome://extensions` のデベロッパーモードからフォルダを読み込みます。
3. Extension Popupに表示される32文字のIDをManagerの「設定」に入力します。
4. 「既存をスキャン」から既存 `.lnk/.url` をインポートするか、「追加」で登録します。
5. 「テスト起動」でtargetを確認し、「Apply」を実行します。
6. Managerを閉じ、登録した `Ctrl+Alt+キー` を使用します。

Applyは、検証 → 外部競合scan → staging生成 → `.lnk` read-back検証 → 自動backup → ownership確認 → commit の順に動作し、失敗時は以前のconfigと管理 `.lnk` を復元します。Markerのない非空フォルダや管理外shortcutは変更しません。

既存shortcutからHotKeyを移行した直後に旧targetが起動し続ける場合は、Explorerがセッション内に以前の登録を保持しています。Windowsへサインアウトするか再起動してから新しい割当を確認してください。

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
