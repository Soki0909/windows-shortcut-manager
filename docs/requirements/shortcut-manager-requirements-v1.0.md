# Shortcut Manager 要件定義書 v1.0

## 2026-10-29 所有者承認の追補（#110）

新規生成するChrome Tab Group用lnkはProtocol v1の`toggle-group`を使用する。前面アプリに関係なく、対象が折りたたみなら展開・前面化・最後のタブ復帰、展開済みなら折りたたむ。タブ・グループは削除しない。別アプリからでも展開済みなら畳まれることを説明し、所有者が承認した。

既存`focus-group`とPopupの「開く」は常に展開する互換動作を維持する。新Managerの起動時監査またはApplyでlnkが再生成されるため、対応拡張を先に導入する。未再生成の旧lnkは従来の開く動作を保つ。元の「HotKey押下直前の前面アプリによる分岐」は低優先度の#112へ分離する。この追補は以下の旧仕様と異なる部分に優先する。詳細はADR-008。

更新日: 2026-09-30  
状態: 要件定義 v1.0 / 実装開始前  
対象: Windows Shortcut Manager（仮称）

## 1. 文書の位置づけ

本書は、Windows の `Ctrl + Alt + A-Z` ショートカットを一元管理するデスクトップアプリ「Shortcut Manager」の実装・テスト・配布に関する要件を定義する。

現時点の上位 Source of Truth は Google Drive の `00_Source_of_Truth_現状整理_20260930` とする。本書は、そのうち Shortcut Manager に関する詳細要件を具体化したもの。

GitHub リポジトリ作成後は、本書と同内容の Markdown を `docs/requirements/shortcut-manager.md` に置き、ソフトウェア要件の正本を GitHub 側へ移行する。Google Drive の Source of Truth は、プロジェクト全体の状態・重要判断・参照先を示す索引として残す。

## 2. 目的

よく使う Web サイト、Windows アプリ、BAT、Chrome タブグループを `Ctrl + Alt + 任意キー` で素早く呼び出せる環境を、ユーザーが GUI から安全に設定・変更・バックアップできるようにする。

Shortcut Manager 自体は設定時にだけ起動し、通常利用時には常駐しない。

## 3. 確定済み設計原則

1. Shortcut Manager は常駐しない。
2. グローバル HotKey の常時監視を Shortcut Manager 自身では行わない。
3. HotKey の実行は Windows Shell Shortcut (`.lnk`) に任せる。
4. 管理対象として Website / Application / BAT / Chrome Tab Group を同列に扱う。
5. BAT は既存 `.bat` ファイルのまま維持する。
6. Chrome 内部のタブグループ操作は Chrome Extension に任せる。
7. Windows 側はキーバインドと起動対象を知るが、Chrome の `groupId` / `tabId` / `windowId` は保持しない。
8. Chrome Extension 側は Windows の HotKey を知らない。
9. 管理アプリを終了した後もショートカットは動作し続ける。
10. 管理対象の生成物は原則 `.lnk` に統一する。

## 4. 技術構成

### 4.1 第一候補

- Language: C#
- Runtime: .NET 10 LTS
- Desktop UI: WPF
- Target: Windows x64
- Configuration: JSON
- Shortcut manipulation: Windows Shell / COM 等の Windows 標準機能
- Build / CI: GitHub Actions
- Distribution:
  - 開発・初期公開: GitHub Releases の self-contained portable package
  - 一般公開候補: 署名済み配布、MSIX / Microsoft Store

.NET 10 は 2026-09 時点で LTS の Active support であり、2028-11-14 までサポートされるため、新規 Windows 専用アプリの第一候補とする。

### 4.2 実装上の制約

- Shortcut Manager を利用するためだけに .NET Runtime の手動インストールを要求しない配布形態を用意する。
- portable 版では self-contained publish を第一候補とする。
- 管理者権限を通常動作の前提にしない。
- ユーザー単位の Start Menu 配下を標準生成先とする。

## 5. 管理対象フォルダ

標準生成先:

`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Shortcut Manager\`

Shortcut Manager が自動削除・更新してよいのは、原則としてこの管理対象フォルダ内の生成物だけとする。

既存の Start Menu / Desktop の `.lnk` / `.url` は「参照・インポート対象」であり、ユーザーの明示操作なしに削除・上書きしない。

## 6. config.json

### 6.1 位置

第一候補:

`%APPDATA%\ShortcutManager\config.json`

### 6.2 役割

`config.json` を Shortcut Manager における設定上の Source of Truth とする。

Windows 上に生成された `.lnk` は config から再生成可能な成果物として扱う。

### 6.3 スキーマ例

```json
{
  "schemaVersion": 1,
  "dispatchProtocolVersion": 1,
  "chrome": {
    "extensionId": "",
    "chromePath": "auto"
  },
  "shortcuts": [
    {
      "id": "uuid",
      "name": "研究",
      "type": "chromeTabGroup",
      "enabled": true,
      "hotkey": {
        "modifiers": ["CTRL", "ALT"],
        "key": "R"
      },
      "target": {
        "groupName": "研究"
      },
      "generatedShortcutPath": "",
      "source": "managed"
    }
  ]
}
```

### 6.4 保存してはならない値

- Chrome `groupId`
- Chrome `tabId`
- Chrome `windowId`
- その他ブラウザセッションに依存する ID

## 7. 対応 Action Type

### 7.1 Website

必須項目:

- name
- URL
- hotkey
- enabled

動作:

- `.lnk` から URL を開く。
- v1 の既定は「OS の既定ブラウザで開く」とする。
- 将来 Chrome 固定等を追加できる構造にする。

### 7.2 Application

対応対象:

- 実行ファイルパス
- 既存 `.lnk`
- URI scheme
- Windows Apps / `shell:AppsFolder` 系の起動方法

最低限、現在利用中の LINE / Slack / ChatGPT App / Outlook を登録できること。

Application の自動検出は便利機能であり、v1 の必須条件は「手動指定または既存ショートカットからのインポートで起動できること」。

### 7.3 BAT

- 既存 `.bat` を外部ファイルとして指定する。
- BAT 本文を Shortcut Manager 独自形式へ変換しない。
- 実行テスト機能を持つ。
- BAT の内容編集機能は v1 の対象外。
- BAT ファイルが消失している場合は設定画面でエラー表示する。

### 7.4 Chrome Tab Group

必須項目:

- groupName
- Chrome Extension ID
- hotkey

生成する起動形式:

`chrome.exe "chrome-extension://<extension-id>/dispatch.html?v=1&action=focus-group&group=<URL_ENCODED_GROUP_NAME>"`

例:

`研究` を固定 action 名 `research` へ変換する方式は廃止する。

これにより Shortcut Manager 側の `groupName` が任意のタブグループ名を指定でき、Chrome Extension をユーザー固有設定なしで配布可能にする。

## 8. HotKey 要件

### 8.1 v1 の範囲

- 修飾キー: `Ctrl + Alt` 固定
- メインキー: `A-Z`
- 大文字小文字は区別しない

将来的に数字、Function Key、別 modifier を拡張可能な内部構造にするが、v1 UI では不要。

### 8.2 キー入力登録

編集画面で実際に `Ctrl + Alt + キー` を押して登録できる。

入力時:

- Ctrl + Alt が不足していれば受理しない。
- A-Z 以外は v1 では受理しない。
- 現在使用済みなら即時警告する。

### 8.3 重複検出

必須:

1. config 内重複
2. Shortcut Manager 管理フォルダ内重複
3. 既知の Start Menu / Desktop の既存 `.lnk` / `.url` の HotKey

探索候補:

- `%ProgramData%\Microsoft\Windows\Start Menu`
- `%APPDATA%\Microsoft\Windows\Start Menu`
- User Desktop
- Public Desktop

注意:

Windows アプリが `RegisterHotKey` 等で独自に取得しているすべてのグローバルキーを静的に完全列挙できるとは限らない。

そのため「ファイル上の競合なし」は保証できても「OS 上の全アプリとの競合なし」は保証しない。Apply 後に実行テストできることを必須とする。

## 9. メイン UI

### 9.1 一覧画面

最低限の列:

- 有効/無効
- Key
- Name
- Type
- Target summary
- Status

操作:

- 追加
- 編集
- 削除
- 有効/無効
- テスト実行
- Apply
- Reload / Rescan
- Backup
- Restore

### 9.2 表示

A-Z の各キーについて、

- 使用中
- 空き
- 競合
- 無効
- エラー

が一目で分かること。

空きキー一覧を明示する。

### 9.3 編集画面

Action Type に応じて入力欄を切り替える。

共通:

- Name
- HotKey
- Enabled

Type 別:

- Website: URL
- Application: target / arguments / working directory 等
- BAT: bat path
- Chrome Tab Group: group name

## 10. Apply

### 10.1 基本動作

Apply を実行すると:

1. config の構文・必須項目を検証
2. HotKey 重複を検証
3. ターゲット存在確認
4. 現在の生成状態との差分を計算
5. 自動バックアップ
6. Shortcut Manager 管理フォルダへ `.lnk` を生成・更新
7. 不要になった「管理下の `.lnk`」のみ削除
8. 結果を再読み込み
9. 成否を UI に表示

### 10.2 安全性

- 入力エラーまたは競合がある場合、一括 Apply を開始しない。
- 途中失敗時は可能な限り Apply 前の状態へロールバックする。
- 管理対象外のショートカットは削除しない。
- Apply 前後の差分をログに残す。

## 11. インポート

### 11.1 対象

- `.lnk`
- `.url`

### 11.2 読み取る情報

可能な範囲で:

- Name
- HotKey
- Target
- Arguments
- WorkingDirectory
- Icon
- URL

### 11.3 ルール

インポートは「読み取って Shortcut Manager 管理設定として複製する」ことを基本とする。

元ファイルを自動削除しない。

元を削除する機能を将来設ける場合も、別操作 + 明示確認とする。

## 12. バックアップ / 復元

### 12.1 バックアップ内容

- `config.json`
- Shortcut Manager が生成した `.lnk`
- バージョン情報
- schemaVersion
- バックアップ日時

### 12.2 自動バックアップ

以下の直前に自動バックアップ:

- Apply
- Restore
- config schema migration

### 12.3 手動エクスポート

ユーザーが任意の場所へバックアップパッケージを保存できる。

## 13. Chrome Extension 連携

### 13.1 Extension ID

Shortcut Manager は Chrome Extension ID を設定として保持する。

Unpacked Extension は配置・キーにより ID が変わる可能性があるため、ID をアプリにハードコードしない。

Chrome Web Store 配布版では固定された Store Extension ID を初期値として設定できる設計にする。

### 13.2 Chrome Path

自動探索候補:

- `%ProgramFiles%\Google\Chrome\Application\chrome.exe`
- `%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe`
- `%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe`

自動検出できない場合は手動指定可能とする。

### 13.3 v1 の通信

一方向:

Shortcut Manager / `.lnk`
→ Chrome
→ Extension Dispatcher

Native Messaging、localhost、Named Pipe は使用しない。

Shortcut Manager から Extension 内部状態を直接取得する双方向通信は v1 では行わない。

### 13.4 接続テスト

「Chrome Tab Group」編集画面に Test を設ける。

Test は実際に Dispatcher URL を起動し、ユーザーが Chrome 側の動作を確認できる方式とする。

## 14. ログ

ローカルログを用意する。

記録対象:

- app version
- Apply
- generated / updated / deleted shortcut
- import
- backup / restore
- validation error
- exception

記録しない:

- 閲覧ページ本文
- Chrome タブのページ内容
- パスワード
- 認証トークン

## 15. プライバシー / セキュリティ

- テレメトリはデフォルトで送信しない。
- config はローカル保存。
- クラウド同期を v1 では実装しない。
- 外部サーバーを起動しない。
- 管理者権限を常用しない。
- Extension の秘密情報を保存しない。
- Chrome Web Store / コード署名用 private key をリポジトリへコミットしない。

## 16. GitHub 管理

### 16.1 1リポジトリ構成

Shortcut Manager と Chrome Extension は同じ製品を構成し、Dispatcher 契約も共有するため、v1 は monorepo を採用する。

推奨:

```text
/
├─ src/
│  ├─ ShortcutManager/
│  ├─ ShortcutManager.Core/
│  └─ ChromeExtension/
├─ tests/
│  └─ ShortcutManager.Tests/
├─ docs/
│  ├─ requirements/
│  │  ├─ shortcut-manager.md
│  │  └─ chrome-extension.md
│  └─ architecture/
├─ scripts/
├─ .github/
│  └─ workflows/
│     ├─ ci.yml
│     └─ release.yml
├─ README.md
├─ CHANGELOG.md
├─ SECURITY.md
└─ LICENSE
```

ライセンスは公開方針を決めるまで TBD とする。

### 16.2 Branch

- `main`: 常にリリース可能
- feature branch
- Pull Request 経由を基本

個人開発であっても CI を通してから main へ入れる。

## 17. CI

Pull Request / push で最低限:

- `dotnet restore`
- build
- unit test
- Release build
- Extension manifest / JS 構文チェック
- packaging test

Windows 固有機能を含むため Shortcut Manager の主要 CI は `windows-latest` runner を使う。

## 18. Release / 配布

### 18.1 初期

GitHub Releases:

- `ShortcutManager-win-x64.zip`
- Chrome Extension 開発用 ZIP
- SHA-256 checksums
- Release notes

Shortcut Manager は self-contained portable package を第一候補とする。

### 18.2 一般公開

未署名 EXE は Windows SmartScreen / Smart App Control により警告・ブロックされる可能性がある。

安定版の配布候補:

1. コード署名した GitHub Release
2. MSIX
3. Microsoft Store

Microsoft Store 経由の MSIX は Microsoft により署名され、SmartScreen のダウンロード警告を避けやすいため、利用者が増えた段階で有力候補とする。

### 18.3 自動アップデート

v1 では常駐 updater を持たない。

初期案:

- About 画面に現在 version と Release ページへの導線
- ユーザー操作による更新確認

Microsoft Store / MSIX 採用時は、その更新機構へ委ねる。

## 19. Versioning

Semantic Versioning を使用する。

v1 開発中:

- `0.x.y`
- 安定仕様完成後 `1.0.0`

Shortcut Manager と Chrome Extension は当初同一リリース番号を付ける。

内部通信互換性は別途 `dispatchProtocolVersion` を持ち、アプリ version と切り離して管理する。

## 20. v1 非機能要件

- Shortcut Manager 終了後、バックグラウンドプロセスが残らない。
- 起動にネットワーク接続を必要としない。
- 通常操作に管理者権限を要求しない。
- config が破損しても既存ショートカットを即時破壊しない。
- エラーはユーザーに原因と対象を示す。
- Apply は再実行しても同じ config なら同じ状態になる（冪等）。
- 生成 `.lnk` は config から再構築できる。

## 21. v1 受け入れ条件

1. Website を GUI から登録し HotKey で起動できる。
2. Application を登録し HotKey で起動できる。
3. BAT を登録し HotKey で実行できる。
4. Chrome Tab Group を登録し HotKey で対象グループへ移動できる。
5. `Ctrl+Alt+R → 研究` が PoC と同等に動作する。
6. `Ctrl+Alt+X → 7脱` が動作する。
7. Shortcut Manager を終了してもすべての HotKey が動作する。
8. Shortcut Manager の常駐プロセスがない。
9. HotKey 重複を Apply 前に検出できる。
10. 空き A-Z を表示できる。
11. `.lnk` / `.url` をインポートできる。
12. Apply 前バックアップから復元できる。
13. config から管理 `.lnk` を全再生成できる。
14. Chrome Extension ID を変更して再 Apply できる。
15. A-Z 最終配置を config として一括反映できる。

## 22. v1 対象外

- グローバルキーを Shortcut Manager 自身が常時フックすること
- Native Messaging
- localhost server
- Named Pipe
- クラウド同期
- BAT エディタ
- Chrome の保存済みだが閉じたタブグループの完全復元
- Chrome タブ内容の収集
- macOS / Linux 対応
- 自動 AI 配置提案
- 複数端末同期

## 23. 参考情報

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- WPF for .NET 10: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- .NET single-file deployment: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- GitHub Actions .NET: https://docs.github.com/en/actions/tutorials/build-and-test-code/net
- Windows SmartScreen reputation: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
