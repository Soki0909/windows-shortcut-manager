# 開発への参加

最初に `docs/requirements/`、受け入れ済みの `docs/architecture/`、[AGENTS.md](AGENTS.md)を確認してください。要件を黙って変更せず、関連Issueに目的と受け入れ条件を記録します。関連する複数Issueを一つのPRへまとめて構いません。

## 準備と検証

Windows、`global.json`に対応する.NET SDK、拡張テストにはNode.jsを使います。通常の.NET/拡張テストに個人設定やChromeログインは不要です。

```powershell
dotnet restore ShortcutManager.sln
dotnet test tests/ShortcutManager.Windows.Tests/ShortcutManager.Windows.Tests.csproj -c Release --filter FullyQualifiedName~対象クラス
./scripts/run-debug.ps1
# 起動時の自動修復を今回だけ保留する場合
./scripts/run-debug.ps1 -AuditOnly
```

変更で確認が必要な範囲に絞って検証してください。Core変更は該当Coreテスト、Windowsの反映/復元は関連トランザクション、拡張変更は `node --test tests/ChromeExtension.Tests/*.test.js`、配布変更はReleaseのみのパッケージと配布物検査を行います。CIは差分から対象プラットフォームを選びます。インストーラーの実行テストは使い捨てGitHub Windows runnerだけで行い、制限を回避しないでください。

## 実機への影響

- 開発UIは `run-debug.ps1`を使います。Debugの実行名・タイトル・アイコン・taskbar identity・LocalAppData・管理フォルダを製品版と分離し、利用中製品のconfigを自動流用しません。他editionのHotKeyも外部競合として扱います。
- Release/publish出力を普通の開発アプリとして起動しません。ビルドは利用中製品の上書きやSetup実行を許可しません。
- 障害注入は一時フォルダのfixtureだけで行い、管理外ファイルと原本を保全します。Chromeのlive検証は専用プロファイルと専用拡張で行い、通常プロファイルや実キーを勝手に使いません。
- Managerは非常駐、起動はWindows `.lnk` HotKey、BAT/CMDは外部ファイル、Chrome操作はDispatcher方式です。global hook・常駐通信・拡張のhost権限/content scriptsを追加しません。

## PRの書き方

問題と変更後の動作を先に説明し、関連Issue、変更した境界、実行した対象コマンドと結果、未実施のlive条件を記載してください。テスト用データは架空のURL・一時フォルダ・生成したリンクを使用します。個人パス、設定、URL履歴、生ログ、秘密鍵、Store資格情報をcommitしません。秘密の例は `.env.example`等のプレースホルダーだけにします。

新しい製品依存は必要性を先に説明し、標準機能や既存依存を優先します。ビルドツール・Actionの更新は固定元、ハッシュ/署名、ライセンス、影響するテストとパッケージを確認します。実施していない確認を完了にせず、[VERIFICATION](docs/verification/VERIFICATION.md)と現行実装計画へ実行証拠を記録してください。

脆弱性は [Security Policy](SECURITY.md) の非公開窓口へ報告してください。

SDK/Action/依存/installer tool更新、SBOMと来歴は [配布とビルド依存の保守](docs/design/SUPPLY_CHAIN.md)を参照してください。
