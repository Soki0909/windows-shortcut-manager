# 02. System Architecture

## 1. Components

### ShortcutManager.App
WPF UI。設定編集とユーザー操作だけを担当。

### ShortcutManager.Core
OS 非依存のドメインロジック。

- config model
- validation
- HotKey conflict model
- Dispatcher URI builder
- apply plan model
- filename sanitation
- schema version rules

### ShortcutManager.Windows
Windows 固有機能。

- `.lnk` read/write
- `.url` read
- Start Menu / Desktop inventory scan
- Chrome executable discovery
- managed folder apply
- backup / restore filesystem operation
- process launch

### ChromeExtension
Chrome 内部のみ。

- Dispatcher
- group query
- expand/focus
- last-used tab
- startup collapse
- popup diagnostics

## 2. Dependency direction

```text
ShortcutManager.App
    ↓
ShortcutManager.Core
    ↑
ShortcutManager.Windows
```

実際の composition は `App.xaml.cs` で行う。

Core は WPF / COM / Registry / Chrome API を参照しない。

App は COM を直接呼ばない。

## 3. No resident architecture

Shortcut Manager process lifecycle:

```text
User opens App
→ edit / scan / apply / backup
→ user closes App
→ process exits
```

終了後:
- background process 0
- tray icon 0
- Windows service 0
- global keyboard hook 0

HotKey は Start Menu 配下の `.lnk` を Windows Shell が処理する。

## 4. Runtime flows

### Website

```text
Ctrl+Alt+Key
→ managed .lnk
→ explorer.exe "https://..."
→ default browser
```

### Application — executable

```text
Ctrl+Alt+Key
→ managed .lnk
→ target.exe [arguments]
```

### Application — existing shortcut

```text
Ctrl+Alt+Key
→ managed .lnk
→ explorer.exe "<source .lnk>"
```

元 shortcut の特殊な AppUserModelID 等を Shortcut Manager が再解釈しなくても動かせる fallback とする。

### Application — URI

```text
Ctrl+Alt+Key
→ managed .lnk
→ explorer.exe "<scheme:...>"
```

### BAT

第一候補:

```text
Ctrl+Alt+Key
→ managed .lnk
→ %ComSpec% /d /s /c ""<bat-path>" <args>"
```

BAT 自体は外部ファイルとして維持する。

### Chrome Tab Group

```text
Ctrl+Alt+Key
→ managed .lnk
→ chrome.exe "chrome-extension://<id>/dispatch.html?v=1&action=focus-group&group=<encoded>"
→ Shortcut Manager Tab Bridge
→ tabGroups.query
→ group expand
→ window focus
→ remembered tab activation
→ dispatcher closes
```

## 5. Source of Truth

User intent:
`%LOCALAPPDATA%\ShortcutManager\config.json`

Generated artifacts:
`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Shortcut Manager\*.lnk`

Generated `.lnk` は config から再生成可能。
`.lnk` を設定の正本にしない。

## 6. Managed ownership boundary

管理フォルダには marker:

`.shortcut-manager-owned`

を作成する。

ルール:
- folder が存在しない → 作成 + marker
- folder が空 → marker 作成可
- folder が非空かつ marker なし → destructive Apply を拒否
- marker あり → folder 内を Shortcut Manager の管理対象として扱う

これにより別アプリ/ユーザーの shortcut を誤削除しない。

## 7. State

config 以外の runtime state:

`%LOCALAPPDATA%\ShortcutManager\state.json`

用途:
- last successful apply timestamp
- last applied config hash
- product version
- managed folder path
- last scan metadata

ユーザー設定は置かない。削除されても再構築可能。

## 8. Logs

`%LOCALAPPDATA%\ShortcutManager\logs\shortcut-manager.log`

1 file + rolling retention を簡易実装。
最大 5 files、各 2 MB 程度を上限にする。
秘密情報・ページ本文は記録しない。
