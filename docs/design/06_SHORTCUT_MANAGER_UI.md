# 06. Shortcut Manager UI / UX

## 1. Main Window

初期サイズ:
- Width 1100
- Height 720
- MinWidth 900
- MinHeight 600

Window title:
`Shortcut Manager`

## 2. Header

左:
- Product name
- version

右:
- Scan Existing
- Backup
- Restore
- Settings
- About

## 3. A-Z Key Board

上部に 2 rows x 13 columns の tile を表示。

各 tile:
- Key letter
- assigned name
- type icon/text
- status

Status:
- Empty
- Ready
- Disabled
- Conflict
- Error
- Unapplied Changes

色だけに依存せず文字/アイコンも使用。

tile click:
- assigned → Edit
- empty → Add with preselected key

## 4. Shortcut DataGrid

Columns:
- Enabled
- HotKey
- Name
- Type
- Target
- Status

Buttons:
- Add
- Edit
- Delete
- Test
- Apply

Delete は config candidate から削除するだけ。
実ファイル削除は Apply 時。

## 5. Change model

v1 は **Save ボタンを持たない**。

- UI editing → in-memory candidate config
- Apply → validate + backup + config save + managed links commit
- close with dirty state → discard confirmation
- Reload → disk config へ戻す

「Save したが HotKey は古い」の二重状態を作らない。

## 6. Add / Edit Window

Common:
- Name
- Enabled
- HotKey capture
- Type

Type specific editor:

### Website
- URL

### Application
- launch mode
- Browse executable / Browse shortcut
- URI
- arguments
- working directory

### BAT
- Browse BAT
- arguments
- working directory
- window mode

### Chrome Tab Group
- group name
- current global Extension ID read-only summary
- `Test Dispatcher` button
- `Open extension diagnostics` button

## 7. HotKey capture

WPF `PreviewKeyDown`.

v1:
- Accept only Ctrl + Alt + A-Z
- left/right modifier を同一視
- key repeat は1回として扱う
- system key handling を考慮
- capture box focused 中だけ捕捉

重複時:
- Apply まで待たず即 warning
- existing assignment name を表示

## 8. Settings Window

### Chrome
- Chrome executable: Auto / custom
- detected path display
- Extension ID
- Validate format
- Test Dispatcher

### Storage
- config path read-only
- managed shortcut folder read-only initially
- backup directory
- Open folder buttons

### Advanced
v0.1.0 では必要最小限。
debug logging toggle はあってよい。

## 9. First run

config 無し:

1. empty config 作成
2. Main Window 表示
3. non-blocking First Run panel:
   - Scan Existing Shortcuts
   - Configure Chrome Extension ID
   - Add first shortcut

Wizard 強制はしない。

本人環境への移行では Codex が local bootstrap config を生成して Apply できる。

## 10. Test action

Test は managed HotKey を押すのではなく、candidate definition から直接 launch spec を実行する。

理由:
- Apply 前の設定をテスト可能
- HotKey conflict と target correctness を切り分けられる

Chrome group:
Dispatcher URI を直接 chrome.exe で開く。

## 11. Responsiveness

- scan は async + cancellation
- backup/restore/apply は UI thread をブロックしない
- Apply 中は conflicting controls disable
- operation status / progress text を表示

## 12. Error UX

Error message は最低限:
- What failed
- Which shortcut/file
- What user can do next

stack trace を通常 UI に出さない。
詳細は log / copy diagnostics に出す。
