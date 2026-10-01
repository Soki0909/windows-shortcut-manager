# 04. Domain / Config Design

## 1. Config location

`%LOCALAPPDATA%\ShortcutManager\config.json`

理由:
executable path / source shortcut path 等の machine-specific 情報を含むため Roaming AppData より LocalAppData を正本にする。

Start Menu shortcut 自体は Windows 標準の user Start Menu (`%APPDATA%`) に生成する。

## 2. Root config

```json
{
  "schemaVersion": 1,
  "dispatchProtocolVersion": 1,
  "settings": {
    "chrome": {
      "executablePath": null,
      "extensionId": ""
    },
    "managedShortcutDirectory": null
  },
  "shortcuts": []
}
```

`managedShortcutDirectory = null` は標準パスを使用する意味。

## 3. ShortcutDefinition

```csharp
public sealed record ShortcutDefinition(
    Guid Id,
    string Name,
    bool Enabled,
    HotkeyDefinition Hotkey,
    ShortcutTarget Target);
```

`type` を別 field に重複保存せず、Target の discriminator `kind` を種別の正本とする。

## 4. HotkeyDefinition

```json
{
  "modifiers": ["Ctrl", "Alt"],
  "key": "R"
}
```

v1 validation:
- modifiers は正規化後ちょうど Ctrl + Alt
- key は A-Z の1文字
- left/right Ctrl/Alt は区別しない

## 5. Polymorphic target

### Website

```json
{
  "kind": "website",
  "url": "https://chatgpt.com/"
}
```

v1 は `http` / `https` の absolute URL のみ。

### Application — executable

```json
{
  "kind": "application",
  "launchMode": "executable",
  "value": "C:\\Path\\app.exe",
  "arguments": "",
  "workingDirectory": ""
}
```

### Application — shortcut

```json
{
  "kind": "application",
  "launchMode": "shortcut",
  "value": "C:\\...\\Slack.lnk",
  "arguments": "",
  "workingDirectory": ""
}
```

### Application — URI

```json
{
  "kind": "application",
  "launchMode": "uri",
  "value": "someapp:",
  "arguments": "",
  "workingDirectory": ""
}
```

### BAT

```json
{
  "kind": "bat",
  "path": "C:\\...\\work-start.bat",
  "arguments": "",
  "workingDirectory": "",
  "windowMode": "normal"
}
```

`windowMode`: `normal | minimized`

### Chrome Tab Group

```json
{
  "kind": "chromeTabGroup",
  "groupName": "研究"
}
```

Extension ID と Chrome executable は global setting。
shortcut ごとに重複保存しない。

## 6. JSON implementation

.NET:
- `System.Text.Json`
- `JsonPolymorphic`
- `JsonDerivedType`
- enum は strings
- UTF-8
- indented output
- camelCase
- atomic save

## 7. Atomic save

1. 同 directory に `config.json.tmp-<guid>` を write
2. flush / close
3. existing config があれば `File.Replace`
4. なければ `File.Move`
5. temporary cleanup

Config parse failure 時:
- current managed shortcuts を自動削除しない
- `.broken-<timestamp>.json` へ raw copy
- UI で recovery path を提示

## 8. Validation rules

Root:
- schemaVersion == 1
- dispatchProtocolVersion == 1

Shortcut:
- Id unique
- Name trim 後 1..80
- active HotKey unique
- Key A-Z
- Ctrl+Alt only

Website:
- absolute http/https

Application executable:
- file exists at Apply
- extension 制限は設けないが executable launch を test 可

Application shortcut:
- `.lnk` exists

Application URI:
- non-empty scheme-like value

BAT:
- file exists
- `.bat` / `.cmd` を許可

Chrome:
- groupName trim 後 1..200
- Extension ID `^[a-p]{32}$`
- Chrome executable auto-discover または configured path exists

## 9. Config migration

`IConfigMigrator` は v0.1.0 から interface のみ用意してよい。

v1:
- schemaVersion 1 のみ
- unknown newer schema → read-only error
- older schema が将来出たら sequential migration

## 10. Derived filename

Managed `.lnk`:

`<Key> - <SanitizedName>.lnk`

例:
- `R - 研究.lnk`
- `X - 7脱.lnk`

Name change / key change は Apply の全再生成で処理するため old path を config に保存しない。
