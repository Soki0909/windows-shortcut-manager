# Changelog

## [0.1.0] - 2026-09-30

### Added

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

