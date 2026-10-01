# Chrome Extension 要件定義書 v1.0

更新日: 2026-09-30  
状態: 要件定義 v1.0 / PoC 実証済み  
対象: Chrome Tab Group Launcher Extension（仮称）

## 1. 文書の位置づけ

本書は、Windows Shortcut Manager から起動され、Chrome 内の既存タブグループを検索・展開・フォーカスする Chrome Extension の要件を定義する。

2026-09-30 に Windows 実機で以下の経路を確認済み:

`Ctrl+Alt+R`
→ Windows `.lnk`
→ `chrome.exe`
→ `chrome-extension://<extension-id>/dispatch.html?...`
→ Chrome Extension
→ 既存「研究」タブグループ

Chrome 前面化、グループ展開、最後に利用したタブへの復帰、Dispatcher タブ自動削除まで成功している。

GitHub リポジトリ作成後は本書と同内容の Markdown を `docs/requirements/chrome-extension.md` に配置し、拡張機能のソフトウェア要件の正本とする。

## 2. 目的

Chrome 上で現在開いているタブグループを、外部の Windows ショートカットからグループ名で呼び出せるようにする。

Chrome Extension は Chrome 内部の操作だけを担当し、Windows のキーバインド管理を行わない。

## 3. 責任境界

### Windows / Shortcut Manager が担当

- `Ctrl+Alt+R` 等の HotKey
- HotKey と起動対象の対応
- Chrome executable の起動
- Extension ID
- 呼び出したい `groupName`
- `.lnk` 生成

### Chrome Extension が担当

- `groupName` に一致する現在開いている tab group の検索
- Chrome window の前面化
- group 展開
- group 内 tab のアクティブ化
- group ごとの最後に使用した tab の記録
- 起動時折りたたみ
- エラー通知
- Popup / 診断 UI

### Chrome Extension が担当しない

- Windows HotKey
- `.lnk` 作成
- config.json
- BAT
- Web サイト起動
- Windows Application 起動
- `groupId` 等の永続管理

## 4. Manifest

- Manifest Version 3
- Service Worker 使用
- Chrome Commands 不使用

必要権限の第一候補:

```json
{
  "permissions": [
    "tabGroups",
    "tabs",
    "storage",
    "notifications"
  ]
}
```

原則:

- `host_permissions` を要求しない。
- Web ページ本文へアクセスしない。
- Remote code を実行しない。
- `commands` でグローバル HotKey を登録しない。
- `externally_connectable` による Web ページからのメッセージ受信を v1 では行わない。

## 5. Dispatcher Contract

### 5.1 v1 URL

```text
chrome-extension://<extension-id>/dispatch.html
  ?v=1
  &action=focus-group
  &group=<URL_ENCODED_GROUP_NAME>
```

実際には一行 URL とする。

例:

```text
chrome-extension://<extension-id>/dispatch.html?v=1&action=focus-group&group=%E7%A0%94%E7%A9%B6
```

### 5.2 方針変更

PoC の:

- `action=research`
- `action=seven`

というユーザー固有 action は正式版では使用しない。

Extension に「研究」「7脱」等をハードコードしない。

任意 `groupName` を Dispatcher query から受け取ることで、全ユーザー共通の Extension とする。

### 5.3 protocol version

`v=1` を必須とする。

未知 version:

- グループ操作を行わない。
- Dispatcher に簡潔な互換性エラーを表示する。
- 必要に応じて通知する。

これにより Shortcut Manager と Extension のバージョンが将来ずれても互換性判定できる。

## 6. groupName 正規化

比較前に:

1. null / undefined を空文字へ
2. trim
3. Unicode NFKC

完全一致で検索する。

部分一致・曖昧一致は v1 では行わない。

## 7. グループ検索

`chrome.tabGroups.query({})` で現在開いているグループを取得する。

一致数:

### 0件

- `not-found`
- 通知を表示
- 「保存済みだが閉じているグループを公開 API で直接再オープンする機能」は v1 では持たない。

### 1件

その group を使用する。

### 複数件

1. 最後にフォーカスされていた normal Chrome window に属する一致 group を優先
2. なければ安定した規則で先頭を使用

複数同名 group がある場合にランダムな選択をしない。

## 8. focus-group 動作

順序:

1. target group を検索
2. group を `collapsed:false` にする
3. target group の Chrome window を focused にする
4. group 内の復帰先 tab を決定
5. tab を active にする
6. tab 情報を last-used として記録
7. 成功レスポンスを Dispatcher へ返す
8. Dispatcher tab を閉じる

## 9. 最後に使用したタブ

### 9.1 記録

`chrome.tabs.onActivated` を使用する。

tab が group に属する場合、その group title に対して記録する。

### 9.2 storage

`chrome.storage.local`

例:

```json
{
  "lastTabByGroupTitle": {
    "研究": {
      "tabId": 123,
      "url": "https://example.com/",
      "savedAt": 0
    }
  }
}
```

### 9.3 復帰優先順位

1. 記録した `tabId` が現在も group 内に存在
2. 記録した URL と一致する tab
3. group 内で現在 active の tab
4. group の先頭 tab

### 9.4 注意

URL は復帰補助のためだけに `storage.local` に保存する。

外部送信しない。

## 10. Dispatcher tab

### 10.1 成功時

対象 group へのフォーカス完了後、自分自身の tab を `chrome.tabs.remove()` で閉じる。

### 10.2 失敗時

ユーザーがエラーを認識できる最短時間だけ状態を表示し、その後閉じることを第一候補とする。

開発モードでは debug 情報を表示可能にする。

### 10.3 通常利用

Dispatcher tab が残留しないことを受け入れ条件とする。

## 11. 起動時折りたたみ

PoC では Chrome 起動後 20 秒の window で新規復元 group を折りたたむ方式が成立している。

正式版では設定可能にする。

設定例:

```json
{
  "collapseOnStartup": true,
  "startupCollapseWindowMs": 20000
}
```

配布版では他ユーザーの既存運用を勝手に変更しないため、初期値は `false` を第一候補とする。

現在の本人環境では `true` を設定する。

### 11.1 Scope

v1:

- `collapse all groups` のみでよい

将来:

- managed group names のみ
- allow list

を追加可能にする。

## 12. Popup

### 12.1 表示

- Extension name
- Version
- Extension ID
- Dispatcher protocol version
- 現在開いている tab group 一覧
- group title
- collapsed / expanded
- window 識別情報（デバッグ用途）

### 12.2 操作

- 任意 group を開く
- 全 group 折りたたみ
- startup collapse ON/OFF
- 診断情報表示

PoC の「研究を開く」「7脱を開く」という固定ボタンは正式版では廃止する。

一覧から対象 group を選ぶ方式にする。

## 13. 通知

対象 group が見つからない場合:

Title:
`タブグループが見つかりません`

Message:
指定された group name と、現在 Chrome 上で開かれていない旨を通知する。

Chrome ネイティブの保存済み group が存在していても、閉じている場合は v1 では開けないことを明示する。

## 14. 設定

`chrome.storage.local` に Extension 固有設定を保存する。

候補:

```json
{
  "settings": {
    "collapseOnStartup": false,
    "startupCollapseWindowMs": 20000,
    "debugMode": false
  }
}
```

Shortcut Manager のキーバインド情報は保存しない。

## 15. エラー処理

最低限区別:

- invalid-protocol
- invalid-action
- empty-group-name
- group-not-found
- group-query-failed
- window-focus-failed
- tab-activate-failed

Service Worker で例外を握り潰すだけにせず、Dispatcher / Popup の診断時に識別可能な reason code を返す。

通常利用時は詳細スタックトレースを通知へ出さない。

## 16. セキュリティ / プライバシー

- 外部サーバーへデータを送信しない。
- Analytics / telemetry を v1 では導入しない。
- Web ページ本文を読み取らない。
- host permissions を要求しない。
- タブ URL は last-used 復帰用に local storage にのみ保存する。
- secret / token を保持しない。
- Remote hosted code を利用しない。
- CSP を緩和しない。
- Chrome Extension private signing key を GitHub にコミットしない。

## 17. Chrome Extension ID

### 17.1 開発

Unpacked Extension は ID が変化し得る。

Shortcut Manager が Extension ID を設定値として保持するため、開発中は可変 ID でも運用できる。

開発用 ID を安定させる必要が出た場合は Manifest の `key` 等、Chrome が提供する開発用 ID 固定方法を利用できる。

### 17.2 公開

Chrome Web Store 公開後は Store Item の Extension ID を正式 ID とする。

Shortcut Manager の配布版では、その ID を初期値として組み込める。

## 18. GitHub 管理

Shortcut Manager と同じ monorepo に置く。

推奨:

```text
src/ChromeExtension/
├─ manifest.json
├─ service-worker.js
├─ dispatch.html
├─ dispatch.js
├─ popup.html
├─ popup.js
├─ popup.css
├─ icons/
└─ README.md
```

Chrome Web Store 提出 ZIP は `manifest.json` が ZIP ルートになるよう packaging workflow で生成する。

## 19. CI

Pull Request / push:

- manifest JSON parse
- Manifest V3 であること
- JS syntax / lint
- 必須ファイル存在確認
- 禁止項目チェック
  - `commands` のグローバル HotKey を再導入していない
  - 不要な host permissions を追加していない
- unit-test 可能な純粋関数
  - normalize title
  - dispatch query parse
  - protocol validation
  - target selection

可能なら Chrome headless / browser integration test を後続で追加する。

## 20. Packaging

### 20.1 GitHub

GitHub Release に:

- source
- developer-mode ZIP
- checksums

を置くことはできる。

ただし Windows / macOS の一般ユーザーへ「GitHub の ZIP/CRXをクリックして通常インストール」させる配布方式は正式ルートとしない。

### 20.2 Chrome Web Store

一般配布は Chrome Web Store を正式ルートとする。

可視性:

- 初期テスト: Private / trusted testers または Unlisted
- 安定後: Public を検討

Store 公開版は Chrome の自動更新に任せる。

## 21. Chrome Web Store 提出要件

- developer account
- Manifest V3
- ZIP root に manifest
- version 増加
- Store listing
- icons
- description
- privacy practices
- 必要に応じて support URL / homepage

Extension は機能上の権限を最小限に保ち、審査説明で以下を明示できるようにする:

- `tabGroups`: group 検索・展開
- `tabs`: group 内 tab の選択と last-used 復帰
- `storage`: local settings / last-used tab
- `notifications`: group not found のユーザー通知

## 22. Web Store 更新

GitHub Actions で提出 ZIP 生成までは自動化する。

CWS API による upload / publish は後から追加可能とする。

認証情報・private key:

- GitHub Secrets 等で管理
- repo にコミットしない

Verified CRX Uploads を利用する場合も signing private key を公開 repository に置かない。

## 23. Versioning

Manifest version は Semantic Versioning 相当で管理する。

Shortcut Manager と当初は同じ product release version を合わせる。

別途:

`dispatchProtocolVersion = 1`

を持つ。

互換性の基準は product version ではなく protocol version とする。

## 24. ローカライズ

v1 は日本語 UI で開始してよい。

公開を想定するため、文字列のハードコードを極力分離し、将来 `chrome.i18n` に移行可能な構造にする。

タブグループ名自体は Unicode を正しく扱う。

## 25. v1 受け入れ条件

1. Shortcut Manager が生成する汎用 Dispatcher URL から任意 group 名を呼べる。
2. `研究` を呼び出せる。
3. `7脱` を呼び出せる。
4. group が折りたたまれていれば展開する。
5. Chrome が前面化する。
6. 最後に利用した group 内 tab へ復帰する。
7. Dispatcher tab が残らない。
8. Extension は Windows HotKey を知らない。
9. manifest に Chrome Commands の global shortcut がない。
10. group が存在しない場合は通知される。
11. Chrome 起動時折りたたみを設定で ON/OFF できる。
12. Popup から現在の group 一覧を確認できる。
13. Popup に Extension ID と version を表示できる。
14. host permissions を要求しない。
15. テレメトリ・外部送信なしで動作する。
16. Chrome Web Store 提出 ZIP を GitHub Actions で生成可能な構成になっている。

## 26. v1 対象外

- Windows HotKey の受付
- `.lnk` 作成
- Native Messaging
- localhost
- Named Pipe
- Web ページからの externally_connectable message
- 保存済みだが閉じている Chrome group の直接復元
- タブ本文・フォーム状態・スクロール位置の保存
- クラウド同期
- Chrome 以外のブラウザ
- Chrome group 自体の作成・編集 UI の高度化

## 27. 将来候補

- URL snapshot から group 再構築
- managed group allow list
- group duplicate 解決 UI
- Edge / Chromium 派生ブラウザ対応
- Shortcut Manager との診断連携
- CWS API release automation
- i18n

## 28. 参考情報

- Chrome extension distribution:
  https://developer.chrome.com/docs/extensions/how-to/distribute
- Chrome Web Store:
  https://developer.chrome.com/docs/webstore
- Chrome Web Store distribution visibility:
  https://developer.chrome.com/docs/webstore/cws-dashboard-distribution
- Chrome manifest:
  https://developer.chrome.com/docs/extensions/reference/manifest
- Manifest key / development ID:
  https://developer.chrome.com/docs/apps/manifest/key/
- Chrome Web Store update:
  https://developer.chrome.com/docs/webstore/update
- Chrome Web Store API:
  https://developer.chrome.com/docs/webstore/using-api
