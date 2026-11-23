# Shortcut Manager

ソースコード: [MIT License](LICENSE)。再配布するruntime・素材の表記は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。
v1.0.0の進捗・判断・実行済み検証は [実装計画](docs/implementation/PLAN_v1.0.0.md) で追跡します。
対応環境と未検証条件は [環境マトリクス](docs/verification/ENVIRONMENT_MATRIX.md) にまとめています。v1.0.0の全環境確認は継続中です。
既存のunpacked拡張の更新は [中断復旧付き更新手順](docs/verification/EXTENSION_UPDATE.md) を参照してください。
各版の公開・所有者PC更新の実績は [公開後記録](docs/releases/POST_RELEASE.md) にまとめています。
保存情報と削除方法は [プライバシーポリシー](PRIVACY.md)、Store提出の準備と未実施工程は [提出計画](docs/design/CHROME_WEB_STORE_SUBMISSION.md) を参照してください。Chrome Web Storeでの正式配布は準備中です。

[v1.0.0をGitHubで公開しています](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v1.0.0)。更新前のバックアップと旧版に戻す手順は [リリースノート](docs/releases/v1.0.0.md) を参照してください。

Windows の `Ctrl+Alt+A-Z` を、常駐プロセスなしで Website / Application / BAT・CMD / Chrome タブグループへ割り当てるローカル管理ツールです。Windows Shell の Start Menu `.lnk` HotKey を利用するため、Managerを閉じてもショートカットは動き続けます。

## クイックスタート

初回はアプリのガイドでWebサイト1件の登録から案内します。閉じた後も「使い方」から再開できます。キーは一覧から選べるので、登録時に実際のCtrl+Altを押す必要はありません。オフラインの症状別ヘルプと「情報」からの版確認も利用できます。

登録、初回ガイド、反映後の状態は [画面付きクイックスタート](docs/quickstart/README.md) で確認できます。

1. [v1.0.0の公開ページ](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v1.0.0) の「Assets」から `ShortcutManager-Setup-win-x64-v1.0.0.exe` を取得して実行します。インストール後、スタートメニューの「Shortcut Manager」から起動します。管理者権限や別途.NET Runtimeの導入は不要です。
2. 初回ガイドの「Webサイトを追加」、または一覧の「追加」から、例として `https://example.com/` を登録します。空いているキーを選んで「候補に保存」を押します。既存のファイルは「既存をスキャン」から取り込めます。
3. 一覧から登録した割当を選び、「対象をテスト」でURLが開くことを確認します。「変更を反映」を実行します。初回ガイド内でも同じ操作ができます。キーが反応しない場合はエクスプローラーの再起動を試せます。
4. Managerを閉じ、割り当てた `Ctrl+Alt+キー` を押して同じURLが開くことを確認します。

Webサイトの登録にはChrome拡張は不要です。Chromeタブグループを登録する場合は、同じ公開ページの `ShortcutManager-TabBridge-v1.0.0.zip` を展開します。`chrome://extensions` でデベロッパーモードを有効にし、「パッケージ化されていない拡張機能を読み込む」から `manifest.json` があるフォルダを選びます。拡張メニューの「IDをコピー」で取得した32文字のIDを、Managerの「設定」に入力します。

Applyは、検証 → 外部競合scan → staging生成 → `.lnk` read-back検証 → 自動backup → ownership確認 → commit の順に動作し、失敗時は以前のconfigと管理 `.lnk` を復元します。Markerのない非空フォルダや管理外shortcutは変更しません。

バックアップには保存済み設定・管理リンク・反映状態が含まれます。外部BAT/CMDや参照元lnk、アプリ本体、Chrome拡張の設定やタブ、未適用の編集は含みません。外部ファイルは別途保管してください。復元前には不足するファイルのパスを確認でき、不足があれば現在の状態を変更せず停止します。移動した外部ファイルは元の場所へ戻すか、参照先を修正してバックアップを作り直してください。復元後も、Explorer再起動を辞退した場合は反映待ちの案内が残ります。

HotKeyの変更にはExplorerの登録更新が必要です。反映を後回しにした場合は「Explorerを再起動」から実行できます。タスクバーとデスクトップが一時的に消え、開いているExplorer画面が閉じる可能性があるため、ファイルのコピーや移動が終わってから実行してください。WindowsがShellを自動復帰するのを待つことで、余分なファイル管理ウィンドウの起動を避けます。

## HotKeyだけが遅い場合

「起動診断」で、Windows Shellの応答待ちに関係し得る候補を確認できます。診断は手動・読み取り専用で、他アプリの自動終了や外部送信を行いません。候補は原因の断定ではありません。作業を保存して通常終了し、同じリンクの直接実行と実キーの反応を比較してください。詳細は [起動遅延の検証](docs/verification/LATENCY_2026-10-30.md) を参照してください。

## 起動時の自動監査・修復

Applyが中断していた場合は、保存したジャーナルから以前の完全な状態へ復旧してから監査します。復旧データの破損や中断後の手動変更が見つかった場合は、退避データを残して停止します。旧版Managerは終了してから更新してください。互換性と更新順序は [v1.0.0リリースノート](docs/releases/v1.0.0.md) を参照してください。

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
$version = ([xml](Get-Content -Raw -LiteralPath 'Directory.Build.props')).Project.PropertyGroup.Version
./scripts/package.ps1 -Version $version
# インストーラーを生成する場合（Inno Setupはビルド用のみ）
./scripts/prepare-installer-tools.ps1
./scripts/package-installer.ps1 -Version $version -SkipPublish
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
- v1.0.0のWindows ZIP内の本体とSetupは未署名です。SmartScreenの警告対象になり得ます。
- Chrome Web Store / Microsoft Storeへの公開とコード署名は、所有者アカウント・credentialが必要な別工程です。

詳細は [要件](docs/requirements/shortcut-manager-requirements-v1.0.md)、[設計](docs/design/02_SYSTEM_ARCHITECTURE.md)、[検証記録](docs/verification/VERIFICATION.md) を参照してください。
