# 05. Windows Shortcut Engine

## 1. Goal

Windows Shell の `.lnk` HotKey 機能を正式実装へ昇格する。

PoC v1.1.2 で以下は Windows 実機確認済み:

- Start Menu 配下 `.lnk`
- `Ctrl+Alt+R`
- chrome.exe
- `chrome-extension://...`
- Chrome 外からの起動

参考実装:
`reference-implementation/windows-shortcut-poc-v1.1.2/`

## 2. Shell link adapter

Interface:

```csharp
public interface IShellLinkService
{
    void Write(string path, ShellLinkSpec spec);
    ShellLinkInfo Read(string path);
}
```

`ShortcutManager.Windows` 内で WScript.Shell COM を encapsulate する。

Core / App は COM object を見ない。

## 3. ShellLinkSpec

最低限:

- TargetPath
- Arguments
- WorkingDirectory
- Hotkey
- IconLocation
- Description
- WindowStyle

Hotkey string:
`CTRL+ALT+R` の canonical uppercase 形式。

## 4. Launch spec mapping

### Website

TargetPath:
`%WINDIR%\explorer.exe`

Arguments:
URL

### Application executable

TargetPath:
configured exe path

Arguments:
configured args

WorkingDirectory:
configured value。空なら exe parent。

### Application shortcut

TargetPath:
`%WINDIR%\explorer.exe`

Arguments:
quoted source `.lnk` path

### Application URI

TargetPath:
`%WINDIR%\explorer.exe`

Arguments:
URI

### BAT

TargetPath:
`%ComSpec%`

Arguments を Windows command-line quoting helper で構築:

`/d /s /c ""<bat-path>" <arguments>"`

WorkingDirectory:
configured or BAT parent。

### Chrome group

TargetPath:
Chrome executable

Arguments:
quoted Dispatcher URI

IconLocation:
Chrome executable

## 5. Chrome executable discovery

順序:

1. configured explicit path
2. `%ProgramFiles%\Google\Chrome\Application\chrome.exe`
3. `%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe`
4. `%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe`

PoC の scalar/array bug を再導入しない。

C# では候補を `IEnumerable<string>` として `FirstOrDefault(File.Exists)`。

## 6. Managed directory

Default:

`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Shortcut Manager`

marker:
`.shortcut-manager-owned`

marker body:
- product name
- schema version
- creation timestamp

## 7. Transactional Apply

### Phase A — Plan

UI candidate config を受け取る。

- Validate
- External conflicts scan
- Resolve all LaunchSpecs
- Resolve all output filenames
- Ensure no duplicate output paths
- Compute candidate config SHA-256

ここでは filesystem を変更しない。

### Phase B — Stage

Staging:
`%LOCALAPPDATA%\ShortcutManager\staging\<guid>\`

enabled shortcut を全件 `.lnk` 生成。

各 `.lnk` を `Read()` して:
- TargetPath
- Arguments
- Hotkey

が expected と一致することを確認。

1件でも failure → staging 削除、現状維持。

### Phase C — Backup

Current:
- config
- managed folder

を auto backup ZIP へ保存。

### Phase D — Commit

1. config atomic save
2. managed folder marker check
3. existing managed `.lnk` を temporary old folder へ move
4. staged `.lnk` を managed folder へ move
5. marker を保持/更新
6. state.json 更新
7. old folder を delete

### Phase E — Rollback

Commit 中 failure:
- candidate files 除去
- old managed links を戻す
- previous config を restore
- failure log
- UI に partial failure を隠さない

## 8. Concurrency

Named mutex:
`Local\ShortcutManager.Apply`

Apply / Restore が並行起動したら後発を拒否し、UI に「別処理が実行中」と表示。

## 9. External HotKey inventory

### `.lnk`

WScript.Shell で Hotkey を read。

### `.url`

INI:
`[InternetShortcut]`

`HotKey=<decimal>`

数値デコード:

- low byte = virtual key
- high byte bit flags:
  - `0x02` Ctrl
  - `0x04` Alt
  - `0x01` Shift

例:
`1602 = 0x0642 = Ctrl+Alt+B`

v1 conflict detection 対象は `Ctrl+Alt+A-Z` のみ。

## 10. Scan roots

重複しない root 単位で一度だけ recursive scan:

- `%ProgramData%\Microsoft\Windows\Start Menu`
- `%APPDATA%\Microsoft\Windows\Start Menu`
- User Desktop
- Public Desktop

親と子を重複して recursive scan しない。

Managed Shortcut Manager folder は inventory 上「managed」とマークし external conflict として二重計上しない。

## 11. Limitation

Windows API で他プロセスが runtime 登録した全 `RegisterHotKey` を安全に列挙する仕組みを v1 で実装しない。

したがって:
- file-based conflict detection
- Apply 後 Test launch
- 実機 verification

の3段階とする。
