# Changelog

## [Unreleased]

## [0.2.1] - 2026-10-30

### Fixed

- 公開前にジャーナル後片付け中の再中断を修正。v0.2.0タグの内容を含む正式更新はv0.2.1として公開。

## [0.2.0] - 未公開タグ

### Changed

- Chromeグループの開閉HotKey、旧action互換と最低拡張版の案内（#110・#92）。
- 原本保全、破損設定復旧、管理外ファイル保護、ZIP/config検証と原子的保存（#2・#4・#5・#6・#7・#37〜#40）。
- Apply中断からの永続ジャーナル復旧、abandoned mutex処理、多重起動・処理中更新防止（#3・#63・#34・#35）。
- 専用Chrome回帰と、ドラフト添付物のダウンロード・Setup検証後の公開（#57・#61）。

### Added

- インストール済み製品版とDebug版のEXE名、表示、タスクバー識別、設定・管理ショートカット保存先を分離。
- 専用の `scripts/run-debug.ps1` と、Releaseのみを受け付ける配布パッケージ生成。
- Restoreでも他方の版が使うHotKeyとの競合を検証。

## [0.1.1] - 2026-10-28

### Added

- 提供された製品ロゴをWindows EXE、ウィンドウ、ヘッダー、Setupへ適用（#8）。

## [0.1.0] - 2026-09-30

### Added

- 起動時の管理ショートカット監査と、バックアップ付き自動修復
- ユーザー単位のSetup EXE、スタートメニュー登録、更新・アンインストール
- 非常駐の.NET 10/WPF Shortcut Manager
- Website / Application / BAT・CMD / Chrome Tab Group target
- A-Z overview、key capture、直接テスト起動、既存 `.lnk/.url` scan/import
- 原子的config保存、ownership marker、transactional Apply、backup/restore/rollback
- 汎用Dispatcher Protocol v1対応のManifest V3 Chrome Extension
- remembered tab、startup collapse option、popup診断
- Windows self-contained ZIP / CWS-ready ZIP / SHA-256 package workflow
- モダンなWPFカードUIと物理キー配列 `QWERTYUIOP / ASDFGHJKL / ZXCVBNM`
- Apply後の同意付きWindows Explorer再起動による、新規・変更HotKeyのWindows再起動なし反映

### Known limitations

- Windows配布物は未署名です。
- Store公開・自動更新は含みません。
- 閉じたChrome保存グループの復元は対象外です。
- `.lnk` HotKeyの即時再登録用公開APIがないため、Apply後はWindows Explorerの再起動が必要です。

