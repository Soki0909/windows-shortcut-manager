# Chrome Web Store提出準備 / #46

2026-11-03。提出文面の原稿。アカウント登録、規約同意、支払い、アップロード、審査申請、公開は未実施。正式導入URLも未確定。所有者の開発者アカウントに関する回答待ちと、ソース上で進められる準備を分ける。

## 掲載文面

名称: Shortcut Manager Tab Bridge

概要: Windows Shortcut Managerから、現在開いているChromeタブグループへ移動・開閉します。

詳細:

> Shortcut ManagerのWindowsショートカットから、名前を指定したChromeタブグループへ移動・開閉するための拡張です。Popupから現在開いているグループを選んで開くこともできます。閉じた保存済みグループは復元しません。
>
> 最後に使ったタブへ戻るため、グループ名・タブID・記録時刻と、設定がオンの場合のURLを、このChromeプロファイルのローカル領域に最大100グループ分保存します。URL保存の停止と保存済みURLの除去、履歴の消去をPopupから行えます。外部送信・同期・広告・テレメトリはありません。
>
> Windows HotKeyとの連携には別途Shortcut Managerが必要です。拡張がキーを直接受け取ることはなく、Managerの常駐も不要です。導入後、Popupの「IDをコピー」を使ってManagerの設定へ入力し、「導入・接続の確認手順」に従って動作を確認します。

サポートURL: https://github.com/Soki0909/windows-shortcut-manager/issues/new/choose

プライバシーURL候補（main統合後の公開到達性を確認する）: https://github.com/Soki0909/windows-shortcut-manager/blob/main/PRIVACY.md

## 権限理由・申告の照合

| 権限 | 現在の目的 |
|---|---|
| tabGroups | 開いているグループの名前/状態の取得、展開/折りたたみ |
| tabs | グループ内のタブ特定・選択、最後のタブへの復帰補助のURL参照、Dispatcherタブの処理 |
| storage | ローカル設定・復帰履歴・起動期限と、セッション中の自動折りたたみ抑止 |
| notifications | 対象グループが見つからない等の失敗通知 |

host permissions、content scripts、Native Messaging、externally_connectable、Chrome Commands、remote codeはない。クリップボードはIDコピーの明示クリック時だけブラウザーAPIを使用し、追加権限や読取りを要求しない。Googleログインや本製品の会員アカウントは不要。

ローカル処理だからデータを扱わない、とは申告しない。タブURL・グループ名・タブ操作とその復帰履歴を説明する。Dashboardの実際の質問/分類を提出時に照合し、`PRIVACY.md`・manifest・製品UIと整合させる。

## 審査者の確認手順

1. 専用のChromeプロファイルへZIPを読み込み、架空名の2つのグループと公開サンプルページだけを開く。個人の履歴・ログイン情報は使わない。
2. Popupにグループが並び、「開く」で対象グループへ移動することを確認する。IDコピー・オフライン手順・versionを確認する。
3. URL保存オフ、履歴消去、グループ一覧の更新、折りたたみを確認する。履歴消去でタブが閉じないことを確認する。
4. Windows連携の確認には同版のManagerを専用環境へ導入し、IDを設定、架空グループへの項目を作成、反映して確認する。日常製品や管理外のキーを上書きしない。アカウント/認証情報は提出資料へ含めない。

## 画像の残作業

2026-11-18にChrome for Testing 148.0.7778.97の専用プロファイルで、v0.4.0拡張のツールバーからPopupを開いて掲載候補2枚を撮影した。架空のタブグループ一覧と、URL保存・履歴管理の画面。1280×800、24ビットRGB PNG。ブラウザー外周のみを切り出し、個人データや公開前のStore IDを含まないことを確認した。提出対象のバージョンや表示が変わったら同じ条件で撮り直す。

- 掲載画像は1280×800、最低1枚・最大5枚。個人タブ・ユーザー名・本物の履歴を入れず、承認済みや推薦等の未取得の表示も加えない。提出時に実際のDashboardの画像要件を照合する。
- 128pxアイコンはmanifestに既存。小プロモ画像とマーキー画像の要否を提出時に確認する。[掲載情報の仕様](https://developer.chrome.com/docs/webstore/cws-dashboard-listing)

## アカウント・提出・ID移行

1. 所有者が[開発者ダッシュボード](https://chrome.google.com/webstore/devconsole)で開発者アカウントの有無と公開者情報を確認する。新規の場合は、使用する開発者メールを選び、規約同意と初回登録料の支払いを行う。パスワードや秘密鍵はIssueやソースへ置かない。[登録手順](https://developer.chrome.com/docs/webstore/register)
2. Actionsで生成した同版のroot manifest ZIPを、hash/source/権限確認後に提出する。初期のPrivate/Trusted testersまたはUnlistedと審査の条件をDashboardで確認する。アップロード時の割当IDと公開URLを記録する。公開・承認済みと先に記載しない。
3. Store IDはproduct codeへ固定しない。試用版のIDを自動的に置き換えない。先にManagerのバックアップを作り、旧ID/版を控え、Store版を同じプロファイルへ導入して新しいIDを明示設定する。Managerの候補差分を確認して反映、テスト起動と実HotKeyを確認してから旧拡張を無効化する。異なるID間でChrome storageが自動移行するとは扱わない。
4. 失敗時は旧IDと旧拡張を再び選び、必要ならManagerのバックアップから差分を確認して復元する。管理外のショートカットは変更しない。
5. 公開後は同一Storeアイテムへ増加したmanifest versionで更新する。Managerと拡張のprotocol互換性・実行中versionを確認し、保存方式や権限変更は公開前に文面とポリシーへ反映する。CWS API自動公開は現時点では行わない。

## 参照した公式資料

- [Listing情報](https://developer.chrome.com/docs/webstore/cws-dashboard-listing)
- [掲載画像・説明](https://developer.chrome.com/docs/webstore/best-listing)
- [プライバシーとLimited Use](https://developer.chrome.com/docs/webstore/program-policies/policies)
- [インストール前の開示](https://developer.chrome.com/docs/webstore/program-policies/disclosure-requirements)

提出時に再確認する。現時点の文面準備は審査通過や公開完了を意味しない。
