# 09. Import / Backup / Restore

## 1. Import philosophy

Import は「既存 shortcut を Shortcut Manager 設定へ読み込む」操作。
元 shortcut を自動削除・変更しない。

## 2. Scan result model

- sourcePath
- fileType `.lnk | .url`
- displayName
- hotkey
- target
- arguments
- workingDirectory
- icon
- parseStatus
- isManaged

## 3. Deduplication

scan root を canonical full path で dedupe。
同一 file path を1回だけ返す。

以前の PoC 調査で発生した「Start Menu 親 + Programs 子を両方 recursive」の重複を再発させない。

## 4. `.url`

INI parser を最小実装。

Read:
- URL
- HotKey
- IconFile
- IconIndex

HotKey decoder は unit test する。

## 5. `.lnk`

`IShellLinkService.Read()` を利用。

Imported application は優先順位:

1. normal URL target と判断可能 → Website proposal
2. `.bat/.cmd` → BAT proposal
3. executable → Application/executable
4. 解釈が不安定 → Application/shortcut (source lnk reference)

Import preview でユーザーが type を変更可能。

## 6. Backup ZIP

Auto backup path:

`%LOCALAPPDATA%\ShortcutManager\backups\yyyyMMdd-HHmmssfff_<id>.zip`

contents:

```text
backup.json
config.json
managed-shortcuts/
  ...
state.json          (存在すれば)
```

`backup.json`:
- formatVersion
- createdAt
- appVersion
- reason (`apply|restore|migration|manual`)
- configSha256

## 7. Retention

Auto backups:
最新20個を保持。
manual export は retention 対象外。

## 8. Restore

1. ZIP structure validate
2. backup format validate
3. config validate
4. current state auto backup
5. staged restore
6. Apply transaction と同じ commit path を使用
7. success verify

Restore 専用の別 destructive implementation を作らない。

## 9. Export

Manual Backup button:
SaveFileDialog → ZIP。

Import/export path に個人情報を外部送信しない。

## 10. Current-user migration

Codex implementation run の最終 phase で:

1. existing shortcut inventory scan
2. final desired A-Z layout を `examples/target-layout-20260930.md` から読み取る
3. Website URLs を確定
4. Application は existing Start Menu shortcut / exe を現機で解決
5. BAT「作業開始」を新内容へ更新または新規作成
6. Chrome Extension ID を現機の value に設定
7. `local/bootstrap-config.json` を生成
8. `local/` は `.gitignore`
9. Backup 後 Apply
10. representative HotKey を実機確認

public repository へ本人固有 absolute path を commit しない。
