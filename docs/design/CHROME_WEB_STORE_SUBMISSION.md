# Chrome Web Store提出準備 / #46

2026-11-23更新。公開済みのTab Bridge v1.0.0を基準にした提出準備。開発者アカウントの登録、規約同意、支払い、アップロード、審査申請、Store公開は未実施。

## 掲載文面

名称: Shortcut Manager Tab Bridge

概要: Windows Shortcut Managerから、現在開いているChromeタブグループへ移動・開閉します。

詳細:

> Shortcut ManagerのWindowsショートカットから、名前を指定したChromeタブグループへ移動・開閉するための拡張です。Popupから現在開いているグループを選んで開くこともできます。閉じた保存済みグループは復元しません。
>
> 最後に使ったタブへ戻るため、グループ名・タブID・記録時刻と、設定がオンの場合のURLを、このChromeプロファイルのローカル領域に最大100グループ分保存します。URL保存の停止と保存済みURLの除去、履歴の消去をPopupから行えます。外部送信・同期・広告・テレメトリはありません。
>
> Windows HotKeyとの連携には別途Shortcut Managerが必要です。拡張がキーを直接受け取ることはなく、Managerの常駐も不要です。導入後、Popupの「IDをコピー」を使ってManagerの設定へ入力し、「導入・接続の確認手順」に従って動作を確認します。

サポートURL: https://github.com/Soki0909/windows-shortcut-manager/issues

プライバシーURL: https://github.com/Soki0909/windows-shortcut-manager/blob/main/PRIVACY.md

2026-11-23に両URLと公式v1.0.0 Releaseへ認証なしで到達できることを確認した。サポートURLから既存の報告を閲覧できる。新規Issueを作成する場合はGitHubログインが必要。

## 権限理由・申告の照合

| 権限 | 現在の目的 |
|---|---|
| tabGroups | 開いているグループの名前/状態の取得、展開/折りたたみ |
| tabs | グループ内のタブ特定・選択、最後のタブへの復帰補助のURL参照、Dispatcherタブの処理 |
| storage | ローカル設定・復帰履歴・起動期限と、セッション中の自動折りたたみ抑止 |
| notifications | 対象グループが見つからない等の失敗通知 |

host permissions、content scripts、Native Messaging、externally_connectable、Chrome Commands、remote codeはない。クリップボードはIDコピーの明示クリック時だけブラウザーAPIを使用し、追加権限や読取りを要求しない。Googleログインや本製品の会員アカウントは不要。

### Privacy practices の入力案

- **単一目的**。Windows Shortcut Managerまたは拡張メニューから、現在開いているChromeタブグループを選択し、表示・開閉する。選択したグループで最後に使ったタブへの復帰を補助する。
- **権限理由**。アップロード後にDashboardが列挙する権限を上の4件と照合し、それぞれの用途を記入する。リモートコードの実行については「使用しない」を選択する。
- **データ利用**。タブURLを扱うので「Web履歴」に相当する項目を確認する。グループ名、タブID、復帰履歴と設定、ブラウザー内だけの保存と削除も説明する。URL保存をオフにすると保存済みURLは除去され、切替機能は使い続けられる。開発者および第三者への送信・共有はない。提出時に実際の分類・文言と [プライバシーポリシー](../../PRIVACY.md) を照合し、Limited Useの宣誓を確認する。[入力項目の公式案内](https://developer.chrome.com/docs/webstore/cws-dashboard-privacy) [ローカル処理の開示要件](https://developer.chrome.com/docs/webstore/program-policies/user-data-faq)

## 審査者の確認手順

1. 審査画面から拡張を利用できる場合は、専用のChromeプロファイルで開く。ZIPから再現する場合は展開し、専用プロファイルの `chrome://extensions` でデベロッパーモードを有効化して「パッケージ化されていない拡張機能を読み込む」から `manifest.json` のあるフォルダを選ぶ。架空名の2つのタブグループと公開サンプルページだけを使う。
2. Popupにグループが並び、「開く」で対象グループへ移動することを確認する。IDコピー・オフライン手順・versionを確認する。
3. URL保存オフ、履歴消去、グループ一覧の更新、折りたたみを確認する。履歴消去でタブが閉じないことを確認する。
4. Windows連携の確認には [公式v1.0.0 Release](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v1.0.0) の `ShortcutManager-Setup-win-x64-v1.0.0.exe` を専用環境へ導入する。Popupで使用中の拡張IDをコピーしてManagerに設定し、架空グループへの割当を作成して競合のないキーを反映する。Managerを閉じ、物理キーでグループを開閉できることを確認する。アカウント/認証情報は提出資料へ含めない。

## 掲載画像

2026-11-23に公開ZIPのv1.0.0を使い、Chrome for Testing 158.0.8127.36の専用プロファイルで掲載画像を撮り直した。架空のグループ一覧とURL保存・履歴管理の2枚を1280×800の24ビットRGB PNGにし、文字と切り出し範囲を目視確認した。提出フォルダに128×128のアイコンと、ルートにmanifestがある公開ZIPを揃えた。ZIPのSHA-256は[公開後記録](../releases/post-release/v1.0.0.md)と一致し、MV3の4権限を確認した。[公式の掲載素材要件](https://developer.chrome.com/docs/webstore/cws-dashboard-listing)

2026-11-18にChrome for Testing 148.0.7778.97の専用プロファイルで、v0.4.0拡張のツールバーからPopupを開いて掲載候補2枚を撮影した。架空のタブグループ一覧と、URL保存・履歴管理の画面。1280×800、24ビットRGB PNG。ブラウザー外周のみを切り出し、個人データや公開前のStore IDを含まないことを確認した。提出対象のバージョンや表示が変わったら同じ条件で撮り直す。

- 掲載画像は1280×800、最低1枚・最大5枚。個人タブ・ユーザー名・本物の履歴を入れず、承認済みや推薦等の未取得の表示も加えない。提出時に実際のDashboardの画像要件を照合する。
- 公開v1.0.0 ZIPのアイコンは不透明な絵柄が128×128の端まであったため、次の提出用のソース `src/ChromeExtension/icons/icon128.png` を96×96の絵柄と四辺16pxの透明余白に修正した。公開v1.0.0のタグとZIPは当時の検証証拠として保全する。提出には修正を含む次の正式タグのZIPをActionsで作り、manifestから参照されるアイコンの余白をZIP内で照合する。表示内容が変わった場合は提出版でスクリーンショットを撮り直す。[アイコンの要件](https://developer.chrome.com/docs/webstore/images)
- 小プロモ画像、マーキー画像、動画は任意。[掲載情報の仕様](https://developer.chrome.com/docs/webstore/cws-dashboard-listing)

## アカウント・提出・ID移行

2026-11-23、所有者から開発者アカウントは未準備との確認を得た。登録後に提出・審査・公開とStore版のID移行を進める。

1. 所有者が[開発者ダッシュボード](https://chrome.google.com/webstore/devconsole)で開発者アカウントの有無と公開者情報を確認する。新規の場合は、使用する開発者メールを選び、規約同意と初回登録料の支払いを行う。パスワードや秘密鍵はIssueやソースへ置かない。[登録手順](https://developer.chrome.com/docs/webstore/register)
2. Actionsで生成した提出版のroot manifest ZIPを、hash/source/権限/アイコン確認後に提出する。初期のPrivate/Trusted testersまたはUnlistedと審査の条件をDashboardで確認する。アップロード時の割当IDと公開URLを記録する。
3. Store IDはproduct codeへ固定しない。試用版のIDを自動的に置き換えない。先にManagerのバックアップを作り、旧ID/版を控え、Store版を同じプロファイルへ導入して新しいIDを明示設定する。Managerの候補差分を確認して反映、テスト起動と実HotKeyを確認してから旧拡張を無効化する。異なるID間でChrome storageが自動移行するとは扱わない。
4. 失敗時は旧IDと旧拡張を再び選び、必要ならManagerのバックアップから差分を確認して復元する。管理外のショートカットは変更しない。
5. 公開後は同一Storeアイテムへ増加したmanifest versionで更新する。Managerと拡張のprotocol互換性・実行中versionを確認し、保存方式や権限変更は公開前に文面とポリシーへ反映する。CWS API自動公開は現時点では行わない。

## 参照した公式資料

- [Listing情報](https://developer.chrome.com/docs/webstore/cws-dashboard-listing)
- [掲載画像・説明](https://developer.chrome.com/docs/webstore/best-listing)
- [プライバシーとLimited Use](https://developer.chrome.com/docs/webstore/program-policies/policies)
- [インストール前の開示](https://developer.chrome.com/docs/webstore/program-policies/disclosure-requirements)

提出時に再確認する。現時点の文面準備は審査通過や公開完了を意味しない。
