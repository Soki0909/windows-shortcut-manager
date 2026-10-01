# 01. Product / Release Strategy

## 1. Product name

Windows application: **Shortcut Manager**  
Chrome extension: **Shortcut Manager Tab Bridge**  
GitHub repository 推奨名: `windows-shortcut-manager`

## 2. v0.1.0 のゴール

v0.1.0 は「公開ストアで広く配る」ことではなく、以下を満たす最初の実用版とする。

- 本人の Windows 環境で A-Z のショートカット管理に使える。
- Website / Application / BAT / Chrome Tab Group の4種類が動く。
- `Ctrl+Alt+R → 研究` と `Ctrl+Alt+X → 7脱` が正式実装で動く。
- Shortcut Manager 終了後も HotKey が動く。
- 既存ショートカットを安全にインポートできる。
- Apply 前の自動バックアップと Restore が動く。
- GitHub Actions から配布 ZIP を再現可能に生成できる。
- Chrome Extension の CWS 提出 ZIP を生成できる。

## 3. 配布レーン

### Lane A — Local development / 本人利用

- `dotnet publish` した self-contained folder を ZIP 化。
- インストーラー不要。
- App は任意フォルダへ展開して実行。
- Chrome Extension は initially unpacked。
- Extension ID は config に一度設定。
- 本人が使い始めるための最短レーン。

### Lane B — GitHub Release

Release artifact:

- `ShortcutManager-win-x64-vX.Y.Z.zip`
- `ShortcutManager-TabBridge-vX.Y.Z.zip`
- `SHA256SUMS.txt`
- Release notes

Windows app は portable/self-contained。
Chrome Extension ZIP は developer mode / Web Store submission 共通の構造にする。

### Lane C — Public stable

Windows:
- コード署名済み portable ZIP、または MSIX / Microsoft Store を検討。
- v0.1.0 の完成をこれでブロックしない。

Chrome:
- Chrome Web Store を正式な一般配布ルートとする。
- Store item 作成後の Extension ID を Shortcut Manager の distribution default に設定する。

## 4. リリース容易性のために「やらないこと」

v0.1.0 では以下を導入しない。

- 独自 installer framework
- 常駐 updater
- Native Messaging host
- localhost service
- Named Pipe daemon
- Electron
- WinUI 3 / Windows App SDK
- フロントエンド bundler
- Chrome Extension のビルド必須化
- telemetry backend
- cloud sync

依存関係と配布経路を増やさない。

## 5. 技術スタック

Windows:
- C#
- .NET 10 LTS
- WPF
- System.Text.Json
- Windows Script Host COM (`WScript.Shell`) を `.lnk` 操作アダプタの内部で使用
- xUnit tests
- 原則として追加 UI framework 不使用

Chrome:
- Manifest V3
- Vanilla JavaScript ES Modules
- HTML/CSS
- Node built-in test runner (`node --test`)
- bundler なし
- host permissions なし

## 6. Versioning

- Product version: SemVer `0.x.y` → `1.0.0`
- Shortcut Manager と Extension は v0.x では lockstep version
- Dispatcher protocol は独立した整数 `1`
- config schema version は独立した整数 `1`

Product version の正本:
`Directory.Build.props`

CI で Chrome `manifest.json` version が一致することを検証する。

## 7. Public release 前の非実装ゲート

以下は public release 前にユーザー判断が必要だが、実装を止めない。

- GitHub repository を public にするか
- LICENSE
- Windows code-signing certificate
- Microsoft Store 登録
- Chrome Web Store developer registration / listing 公開範囲
