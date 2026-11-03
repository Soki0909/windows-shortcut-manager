# Shortcut Manager Verification

Updated: 2026-11-03 (Asia/Tokyo)

## v0.4.0確定・製品環境更新・作業終了（2026-11-03）

- [公開後記録](../releases/post-release/v0.4.0.md): PR #156/mainのCIとRelease attempt 2 success、Core39/Windows200/Node29、公開4assetのdigest/attestation・版/source照合。初回UNC fixtureの後片付け失敗は#157として未解決。
- 公式Setupで本体v0.4.0へ更新し公開ZIPの413ファイル一致、設定/データ28/管理23ファイル保持、installed CLI設定検証とinstalled DLL読取り監査差分0。旧製品/データ/リンク/拡張backupも再照合した。ローカルinstaller smokeは行っていない。
- 拡張ファイル22件が公開ZIPと一致し、同じ配置先のmanifest0.4.0・保存ID不変。Computer Useのbrowser URL判定停止により再読み込み/実行版確認はpending。物理HotKey/実Chrome切替/更新後GUI一連操作はnot-tested。
- 所有者の終了依頼に従い、[正式版の残作業42件](../releases/v1.0.0-remaining-after-v0.4.0.md)を残して作業を停止する。v1.0.0完了とは扱わない。

## 配色追従とhover可読性 / #21/#155（2026-11-03）

- main3df86a1のXAMLに対し、IsMouseOver条件だけをTag=hoverへ置き換えたfixtureで、主操作の実効background=#FFF1F3F9/foreground=#FFFFFFFFを再現し#155を作成。修正後は#FF4338CA/#FFFFFFFF。同じsource style/template/resourceの優先順位を評価し、実マウス操作の成功証拠とは分ける。
- ThemePaletteTests/ThemeResourceTestsの5件passed。通常/明暗の組の比率、既存controlのhover/disabled/エラー/派生Window更新、現在OS配色へのQueueRefreshとDispose後の停止を確認。編集状態/診断/manifest等を含む関連35件もpassed。Windowテストを実App参照へ変更し、WPF SDKで省かれたSystem.IO usingを明示してcompileを修正した。
- 暗色描画で派生Windowの文字が黒のままになることを確認。回帰テストでexpected=#FF172033/actual=#FF000000を再現し、共通Window styleの明示後passed。通常行/セルの背景・文字も明示。共通制限外だった起動診断画面も同じ経路へ統一した。
- 非表示の同じ主画面で通常→明暗配色を描画し、状態文字とエラー/未保存の区別を確認。描画ホストが途中で候補を再読込した失敗はfixtureの順序を修正し、製品側の行名判定を弱めていない。OSのテーマ、個人設定/HotKey/Chrome/clipboard、日常製品は未変更。native popup等と実Narrator/実OSコントラストテーマの一連操作は未実施。

## 小画面・キーボード・読み上げの基礎 / #19/#20/#21（2026-11-03）

- PR #153の最終head7986c8d、CI 41561167611 success後main1512093へ統合。Core39/39・Windows195/195・skip0、旧Setup更新/再導入/アンインストールと構成差payloadの保持もpassed。無関係な拡張jobはskip。主画面の最小値480x360と非表示の実HWNDで800x600へ変更可能なことを追加確認。実機の全条件は未完了でIssueはcloseしない。

- 配置計算9件と実EXE resourceのmanifest検査1件、合計10/10 passed（PerMonitorV2/PerMonitor、asInvoker、uiAccess=false）。Debug build警告0/エラー0。manifest検査が単独projectテストでもAppを先にbuildするよう非参照のproject依存を明示。
- 非表示WPFは架空26件だけの隔離設定を使用。1180x800/800x600/右下スクロール後を描画。行のキー/割当名、TabNavigation=Once、Button AutomationPeerのaccess key A、結果Peerが実メッセージを読むこと、設定TextBoxのLabeledByを確認。非表示Settings HWNDでSourceInitializedのサイズ制限もpassed。個人config/HotKey/Chrome・日常製品・clipboardを使用していない。
- 描画ホストのDebugアイコンresource不足はfixtureへ同じ素材を追加して修正。Application.ResourceAssemblyの設定変更はホスト側で不可だったため撤回し、製品コードの回避変更はしない。初回の一覧列縮小はDispatcherのbinding/layout後に正常化したため、製品不具合としてIssue化しない。
- 物理DPI/複数モニター、実際のAlt/Tab/JIS/US、Narrator/高コントラストの全操作は未実施。計算・peer・描画の成功から推定せず、各Issueと環境マトリクスへ残す。

## アプリ起動方法・パスの区別 / #26/#27（2026-11-03、main統合済み）

- PR #151の最終head5c1f95c、CI 41557876433全4job success後main958a29dへ統合。Release配置のプローブを確認し、Core39/39・Windows185/185・skip0、Node29/29。独自URI登録/日本語・空白を含むSMB UNC起動もskipされずpassed。旧Setup更新/再導入/uninstallと構成差payloadの保護検証もpassed。#26/#27 close、日常製品の更新は未実施。

- 初回CI 41557401531はテストプローブがsln未登録でDebugへビルドされ、Release配置を探した4ケースが開始前に失敗。slnのDebug/Release構成へ登録し、製品の起動条件を変えずに修正。Releaseのsln build警告0/エラー0、Release実起動2passed/CI限定2skipも確認。URI/UNCの起動失敗の証拠とは扱わない。

- `LaunchContractTests`の直接起動・元lnk経由2件passed。無画面WinExeが受け取った日本語/空白/引用符/末尾バックスラッシュ/空引数と実際の作業フォルダを確認。元lnkのbyte/HotKey不変もpassed。再検査でも2passed、独自URI登録/SMB共有の2件はlocalで明示skip。個人のHotKey/Chrome/設定や製品版を変更していない。
- `ApplicationLaunchAdviceTests|UriRegistrationTests|ShortcutEditorStateTests`の絞込み14件、Core `TargetPathValidationTests`4件passed。入力中は登録照会しない、F5は未登録を別に案内、非URIモードは登録照会しない、旧引数/種類別候補を保持することを確認。
- `scripts/run-debug.ps1 -NoLaunch`警告0/エラー0。非表示WPFで未登録URIと不足作業フォルダが別の説明として表示され、旧引数はread-onlyであることを確認。編集720x720/480x400とプレビュー740x590/480x320の最下部を描画確認。長い注意文でも実値と実行/取消へスクロール到達可能。レンダー中の起動callbackは実行していない。
- ツール環境更新後に既定shell起動がos error3となったため、利用可能なWindows PowerShellを明示して同じ描画を完了。製品コードへの回避変更なし。
- URIのnonce keyとSMB共有はhosted Windows runner限定として追加。CIの実行結果を確認するまでは成功扱いにしない。署名/Store公開/任意の外部アプリやネットワーク全体の証拠とも区別する。

## 報告用診断のプレビューと新規保存 / #58（2026-11-03、main統合済み）

- PR #149のhead a1f5d48、CI 41508869729対象job成功後main e7d6447へ統合。Release Core39/39・Windows178/178、CLI7、署名境界11、payload7、metadata8、署名tamper4、旧Setup→更新/再導入/uninstallと構成差payload更新の保持検証がpassed。拡張jobは無関係でskip。製品版へのSetup実行はしていない。

- `dotnet test tests/ShortcutManager.Windows.Tests/ShortcutManager.Windows.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~DiagnosticReportTests|FullyQualifiedName~LoggingSafetyTests|FullyQualifiedName~DiagnosticCapturePreserves'`: 10/10 passed。canaryの名前/URL/ID/パス/未知コードを出さず、既知コードと件数を保持。補足の明示追加/除去/上限、未走査と未確認の区別、正確なUnicode保存、既存保持・同時保存・失敗時の非公開を検証。ViewModelの診断作成前後で候補と隔離root内全fileのbyte不変を確認。
- 最初の補足テストはprivacy説明の`userNotes`名に文字列検索が一致した。root JSON項目の有無を直接確認するよう修正。追加した同時保存テストはxUnit解析ルールに合わせ`Assert.Single`のpredicate overloadへ修正後passed。
- `scripts/run-debug.ps1 -NoLaunch`警告0/エラー0。非表示WPFで確認前は保存不可、補足追加/変更/除去で確認解除、オフ時はroot userNotesが消えることを確認。790x700/480x360と小画面の最下部描画を目視確認。短い保存ボタン名へ調整して切れを避ける。個人のconfig/clipboard/Chrome/日常製品を使わず、ファイル選択ダイアログや送信は実行していない。
- #45/#46準備の前区切りmain `443c800`および`79d5014`のCI 41507003680 / 41507291467 successを確認。公開PRIVACY URLはHTTP200、正式Store公開/審査は未実施。


## Store提出資料 / #46（2026-11-03、準備段階）

- `PRIVACY.md`と提出原稿を、拡張のlocal設定/最大100グループ履歴、起動期限、session保護ID、通知、診断、URL除去/履歴消去と照合。Managerのログ/backup/journal保持、アンインストール時保持、Chrome storageとの分離も記載。README/SECURITYから到達可能。
- 公式の掲載/プライバシー/開示資料を参照し、提出文面・権限理由・架空データによる審査手順・公開ID変更時のバックアップ/明示変更/rollbackを用意。文書のみの変更のため製品テストを反復しない。
- 掲載用の実画面、所有者アカウント確認、規約同意/支払い、ZIP提出、審査、正式公開URLは未完了。#46はopenを維持する。公開後にmain上のポリシーURLを到達確認する。資格情報や個人履歴は資料へ含めていない。

## Chrome導入・IDコピー / #45（2026-11-03、main統合済み）

- PR #147のhead cda5231、CI 41505807728全4job success後main 443c800へ統合。拡張テスト/更新保護、Windows build/test/package/Setup更新経路がpassed。

- `node --test tests/ChromeExtension.Tests/worker-regressions.test.js`: 16/16 passed。clipboardはfixtureだけを使用し、Worker設定の読込待ち/失敗でもID欄とコピーが有効、コピー拒否は欄の選択とCtrl+C案内になることを確認。実際の個人clipboardを読書きしていない。
- `scripts/run-debug.ps1 -NoLaunch`: Debug build警告0/エラー0。架空パスだけでWPFを非表示描画し、設定700x500/説明展開480x360/Chromeヘルプ750x620を目視確認。小画面の本文はスクロール可能。ウィンドウ表示や個人config読込/保存はしていない。
- headless shellで実Popup/help HTML/CSSを描画して確認。静的fixtureのためChrome API/実グループ/OS clipboardの実動作証拠ではなく、それらはNode fixtureと別の手動条件。新権限・製品依存・常駐通信なし。Setup/日常製品/個人Chromeデータは未変更。


## インストール/更新の保持と旧payload整理 / #56/#91（2026-11-03、main統合済み）

- PR #145の最終head `34158cb1180bd7ddeea9b0d2570756e1b5e33a65`で、統合CI 41500960914全4jobとnative 41500960938 success。公開v0.3.1→新版→同版再導入→uninstall→reinstallがpassed。旧CLIのApplyが生成したconfig/state/backup/managed lnkと管理外lnkの全byte hash、Q/V HotKey属性を保持。新しいexe/commit/HKCU/audit-only link/CLI検査もpassed。
- 実file構成が異なる旧fixture Setup→新Setupでは、所有が一致する不要物の退避/削除と改変/ユーザー追加物の保持を確認。未対応項目を持つconfigはreinstallがbyte保持し、CLIが非ゼロ・ok:falseで上書きせず拒否した。uninstallでもバックアップ/管理内外lnk/追加物/退避物を保持する判定がpassed。main `450a52006ec2c9fbcb6c374c884f1f77c1ef8ea8`へ統合し#56/#91 close。対話uninstallの実UIと物理キーは別の未実施条件。

- 専用`Installer payload safeguards` job 41499714890 success。native12ケース・保持対象27 hash・generator7件passed。初期CI 41495659650/41496966503/41498442981/41498978251ではUnicode名の判定で安全側へ保留し、データを削除していない。fixtureのUnicode名自体は正しく読め、禁止文字検出で存在しない`?`へ一致することをログで確認。標準PosをUnicode native StringChangeEx、分割をnativeパス関数へ置換してpassed。Trim equalityは0であり、Trimが文字を失ったという仮説を確定原因にはしていない。
- CI 41499714927では、保持検査用の未対応root項目を正常系configへ混ぜたfixtureがCLIで拒否された。旧CLI生成の正常configの更新成功と、別ケースの未対応config保全/拒否へ期待値を修正。UnmappedMemberHandling等の製品側の安全基準は変更しない。旧Setup→新版の統合検査を継続する。
- installer固有の軽量jobを追加し、統合Windows jobとのnative unit重複を外した。Releaseは引き続きnative/統合を両方実行する。古いPR runだけをcancelし、mainの既存runはcancelしない。最新headの全統合CI成功を待ってからmergeする。

- 実publishのUnicode相対パス/SHA-256から配置receiptを生成。旧receiptが現在保存先に結び付く場合だけ、新版にない一致ファイルを新規の退避フォルダへコピー・hash再検証してから削除する。旧receiptを退避し、新receiptは同じvolume内で原子的に切り替える。改変/不明/receiptなし/破損/過大/重複/途中変更は保持する。新しい製品依存なし。
- 配置先/新版file先のreparseや照会不能は書込み前に拒否。旧不要file側は追跡しない。config/state/バックアップ/管理HotKey/Chrome storageは整理対象外。uninstallには保持説明と既定キャンセルを追加し、silent時も従来どおり保持する。完全削除の一括UIは#52の未完了として維持。
- `test-installer-payload.ps1` 7件passed（Unicode BOM/相対パス/hash、予約名、空/不正版、reparse/祖先、拒否時の元manifest・外部file保持）。scripts parser、CI scope20件、7 YAML parse、diff check passed。
- 製品Setupと同じnative処理のハーネスをコンパイル確認し、12保護fixtureを使い捨てrunnerへ追加。ローカルでは実行していない。公開v0.3.1 Setupの既知SHAから、旧CLI Applyで作ったfixtureと管理外lnkを保持したupgrade/reinstall/uninstall/reinstall、実構成差を持つ旧fixture Setup→新SetupもCIへ追加。
- 各runner限定scriptをローカルで呼び、実行前に停止することを確認。guard回避なし。Release Setupのコンパイルと配布物構造/hash検査passed（ローカルpayloadは以前のビルドを使った構造確認であり、正式release/sourceの一致証拠にはしない）。日常製品のSetup実行・個人HotKeyやChromeデータ変更はしていない。
- 初回記録時はCI実行前だったが、上記の最終headでnative12件/27保全hashと実Setup lifecycleの成功を確認してからmainへ統合した。日常製品へのインストールや正式版公開は行っていない。

## テスト依存更新 / PR #144（2026-11-03）

- Microsoft.NET.Test.Sdk17.14.1→18.11.0（MIT）、xunit.runner.visualstudio3.1.4→4.0.1（Apache-2.0）。xUnit2.9.3と.NET SDK10.0.403は維持。実restore済みnuspecとhashを確認し、公式NuGet archiveのSHA-512と実体が一致。両方の`dotnet nuget verify --all` passed。
- CI 41493712002の実出力でCore39/39、Windows170/170を確認。別worktreeでsolution6projectの推移依存をNuGet vulnerability auditし、既知脆弱性検出0。テスト専用で製品のcsproj/manifest/runtime依存は増えない。main `31bbc0055913f6346e6707d8aaae0bb20598f478`へ手動統合。
- flat-containerの`.sha512` URL照会はBlobNotFoundだったため、それを検証成功とは扱わず、公式`.nupkg`そのものを取得してSHA-512と署名を照合した。raw cache/証拠はgit管理外。

## Chromeの復帰履歴管理 / #49（2026-11-03、未公開）

- Popupで保存目的・localのみ・100グループ上限・URL補助のオフ/既存URL除去・確認付き全履歴消去を案内。消去がタブ/グループ/他設定を変更せず、以降は再記録されることも明示。manifest権限とprotocolは変更なし。
- remember/設定patch/消去/件数応答を同じqueueへ直列化。追加検証で非同期tabs.getの完了後にqueueへ入れると先行activationが消去の後から書ける問題を検出し、イベント発生時に順番を確保して修正した。
- 対象Worker/Popup 15件passed（新規6件）。既定互換、遅い先行書込みとURL無効化、Worker再読込み、他設定保持、消去後の切替、保存失敗/不正patch、特殊名、診断へのURL/例外本文非出力、確認前消去停止、URLだけのpatch、失敗後の変更無効/再読込みを確認。protocol/選択等13件も関連する履歴読みへ通るため実行しpassed。
- 静的Popupの実HTML/CSSを既存Chromium headless shellで400×850へ描画し、展開した説明・折返し・無効な消去ボタン・操作導線をPNG目視。fixtureの件数だけを差し込んだレイアウト検査で、実Chrome extension runtimeの操作とは区別する。
- インアプリブラウザーはlocalhostへ接続timeout。通常Chrome for Testingのheadless描画も出力不能だったため検証用プロセスだけ終了し、専用headless shellで描画を完了した。localhost server/fixture以外の個人Chrome、URL履歴、実タブ/拡張、製品版には触れていない。実ChromeでのPopup操作・OS clipboard・高DPIの証拠にはしない。

## PR #141 統合確認

- PR CI 41487695479の全jobと、非公開のRelease workflow_dispatch 41487722103がsuccess。Inno7.1.1の使い捨てinstall/audit-only link/CLI/uninstall smoke passed。
- 最終4assetのGitHub署名来歴を生成し、repository/workflow/commit/ref/hosted runner付きのgh attestation verifyが全件成功。tagでないため公開stepはskipped。main `dd5201ac2df3b139ab234c5c318f25508b66df46`へ統合し#60/#139 close。既存の公開版/日常製品はv0.3.1のまま。

## ビルド依存の保守・SBOM/来歴 / #60/#139（2026-11-03、未公開）

- SDK 10.0.403（従来latestFeatureで実際に使用されていた版）をglobal.jsonで厳密固定し、CI/Releaseも同じ入力を使う。既存majorの公式Action tagをAPIでcommitへ解決しSHA固定。DependabotでAction/NuGet/SDKを毎週確認し、Innoはread-onlyの定期workflowで公式latestと比較する。自動merge/download更新なし。
- 初回Inno照会で6.7.3より新しい7.1.1を検出し#139へ記録。公式変更履歴・対応Windows・利用条件と公開digestを確認。公式x64 EXEのSHA-256 `8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0`とAuthenticode Validを検証してworkspaceのビルドツールを更新。製品Setupの実行ではない。
- 実deps.json由来の自作3projectと.NET runtime pack 2個（10.0.14）、nuspecのlicense、cacheのNuGet SHA-512、依存辺からSPDX 2.3を生成。ビルド元commit/dirty、SDK、deps/SBOM hashも同梱し、個人パス・設定・環境変数・tokenを出力しない。runtimeの原文noticeは維持。
- メタデータ境界8件passed（正常、deps hash、版、package欠落、依存辺欠落、package hash、dirty、commit不一致）。原本hash不変。公式SPDX v2.3 schemaをローカルだけに取得し、PowerShell標準Test-Json成功。schema SHA-256は`239208b7ac287b3cf5d9a9af23f9d69863971102a5e1587a27a398b43490b89b`。追加の製品/テストライブラリは導入しない。
- 新しいmetadata付きRelease ZIPとInno 7.1.1 Setupを生成。配布検査と7サイズのexe/Setup embedded icon、3hashの照合passed。App/SetupはNotSigned。ローカル成果物のSourceDirtyはtrueと正しく表示し、正式成果物の証拠には使わない。
- Release workflowへ公式actions/attestをSHA固定で追加。最終4assetの来歴をrepository/workflow/commit/ref/hosted runner付きで検証し、公開時にはdraft再downloadにも検証を適用する。実GitHub attestation生成/確認はCI後のworkflow_dispatchで追跡する。tag/public release/日常製品の更新はしていない。
- #138 CI 41485546790成功後main `abcd9c6b2bed614779e9c8c9d9f9aa60bbe19fb8`へ統合。#59/#93 close。#50/追加#140は実署名経路の未完了項目を明示してopen。

## 配布の署名準備・報告/貢献導線（2026-11-02、未公開）

- #59/#93: 公開フォームに版/導入形態/再現/期待実結果/個人情報除去、機能の受け入れ条件、非公開報告先を設定。CONTRIBUTING/PR templateは要件・ADR・実機分離・fixture・対象検証・秘密情報・依存基準を案内。YAMLは既存環境のPyYAMLで5ファイルparse成功。
- GitHub private-vulnerability-reporting設定をAPIでfalseからtrueへ変更し、GETのenabled:trueで有効化を確認。非公開reportの送信は行っていない。サインインが必要な新規報告フォームの実送信/UI確認は未実施で、公開ページ取得のcache missをフォームの失敗とは判定していない。
- #50: 所有者は証明書/契約未用意と回答。[CODE_SIGNING](../design/CODE_SIGNING.md)に適格性と申請情報を整理。SignPathの審査/承認、CAの本人確認/契約、Microsoftの地域/主体条件を区別し、未採択の提供表記や新契約を行っていない。
- 証明書ストア経路は対象root/EXE/DLL/reparse保護、正確なthumbprint/秘密鍵/有効期間/EKU、SHA-256/RFC3161、署名後の信頼/発行元/timestampを要求。自作4バイナリとSetupのみ署名対象、runtimeの再署名なし。ZIP記録の署名後hashと実体を照合し、署名必須検査はpublish時の再downloadにも引き継げる。
- `test-signing.ps1` 11件passed。sandboxではreparse fixture作成が権限制約で失敗したため通常ユーザー権限で再実行し、11件の実行結果を確認。symlink権限を必須にしないjunction fixtureを使用。証明書の生成/取込み、秘密鍵の送付、timestamp通信、署名は行っていない。
- Release ZIP/Setupを再生成、`verify-release-artifacts.ps1 -Version 0.3.1` passed。両方のAuthenticodeはNotSignedと明示。`test-release-signing.ps1`は記録hash改変・署名状態偽装・重複・署名必須時の未署名拒否の4件passed。fixtureのみ変更し元3成果物のSHA不変。全scripts parserとdiff check passed。ローカルSetup実行/製品更新はしていない。
- 実証明書による成功系、署名サービスとGitHub連携、署名済み公開物の実ダウンロード/発行元確認は未実施。#50は未完了。runtime変更がないためローカルCore/Windows/拡張の全回帰を繰り返していない。CIは配布の統合検査を追跡する。

## PR #136 統合確認

- CI 41456172398 success（Unicodeリンクをそのまま読む英語Windows installer smokeを含む）。main `34be17feeca7a6c29b6d98efa424480e65470ef5`へ統合。#43/#44/#137をclose。製品版はv0.3.1のままで未更新。

## UnicodeのShell Link読取り / 追加#137（2026-11-02、PR #136へ統合）

- CI 41450882258/41453135967/41454138947の使い捨て英語Windowsで、存在する日本語名のSetup専用lnkからWScriptが空の実行先/引数を返した。1243 byte・正しいShell Linkヘッダーを持ち、同じバイトをASCII名へコピーすると正しいTarget/`--audit-only`/HotKeyなしを返すことを確認。検証の比較を緩めず、原本の読取り経路を修正した。
- 日本語PCでも、正常なASCII名lnkを同じ一時フォルダで`リンク🧪.lnk`へ変更すると、926 byteの原本に対しWScriptは空の実行先/引数を返すことを再現。利用中リンクは変更していない。
- ShellLinkServiceをUnicode版IShellLinkW/IPersistFileへ置換。全項目をUnicodeで保存/読戻しし、HotKeyのWORDとicon indexを保持する。読取りではRAWPATHを使い、Resolve・対象起動・元ファイル変更・別フォルダへの回避移動をしない。Managerで割当可能なキーは従来どおりCtrl+Alt+A–Zのみ。
- Unicodeのファイル名/対象/引数/作業フォルダ/説明/icon/HotKey/表示方法、原本SHAと更新日時不変、旧WScript生成物と逆方向互換、未設定キーと壊れたlnkの対象7ケースpassed。関連する監査・復元・所有確認・元lnk保持の10ケースpassed。
- PowerShellから同じ製品ソースを単独Add-Typeする経路もUnicode fixtureでpassed。installer smokeはこの読取りを使い、原本の名前を変えずtarget/args/HotKeyを検証する。smoke自体は使い捨てrunner限定のまま。
- worktreeのDebug build 0 warnings/errors。read-only CLI scan exit0 / 248件 / 読取り失敗3 / 未走査・除外2、元Debug configのSHA不変。日常製品の更新・キー入力・Explorer再起動・Setup実行はしていない。
- 新しいworktreeで最初の`--no-restore`は資産未復元のためテスト実行表示なしで終了し、合格扱いしていない。対象プロジェクトをrestoreしてから実際の7件/10件の実行出力を確認した。

## 監査の権限・詳細履歴・保留 / #43/#44（2026-11-02、未公開）

- 監査の各実行先/引数/作業フォルダ/HotKey/アイコン/説明/表示方法とファイル欠落等について、実際/期待する値を記録。ローカル履歴に操作・結果・修復前後・バックアップを保存し、壊れた1件を隔離して最新50件を表示。履歴から復旧ヘルプ/既存の確認付き復元へ進める。外部送信や自動履歴削除は追加しない。
- editionごとの別ファイルで永続的な監査のみ方針を保持。`--audit-only`とSetupのHotKeyなし専用リンクで今回だけ自動修復を保留できる。監査のみGUI起動は中断transactionの自動復旧も保留し、別の明示復旧操作を提供する。普通の起動は既定の安全な自動修復を維持する。
- 起動先不足・既知の外部キー競合・外部走査の一部未確認で自動修復を保留。同じconfigと実際の差分値について試行intentを先に永続化し、失敗/中断後の同一試行を自動反復しない。方針の破損は監査のみ、試行記録の破損/保存不能は自動修復保留とする。
- 明示修復は保存済み設定だけを使い候補を保持。走査後の設定外部変更を検出して停止する。修復前に差分と未確認範囲を見て明示確認する。修復後の独立監査や履歴書込みが失敗しても、既に完了したtransactionを未反映と誤表示しない。一致/再監査必要/Explorer反映/実キー確認は別に扱う。
- 監査Control/既存Startup/保存済み監査の対象10件passed。修復後監査が例外ではなく差分結果になるケースを検出し、transaction完了＋再監査必要の表示へ修正してpassed。保存設定の走査中変更・未保存候補保持の明示修復・設定とは別の方針保持等を含む対象10件passed。外付け先不足での反復停止とフィールド正規化の追加2件passed。
- Debug build 0 warnings/errors。GUI専用フラグと未知引数のエラー境界を変更したためCLI7プロセスを確認しpassed（未知optionやGUI/コマンド混在で対話ダイアログを開かず非ゼロ終了）。`run-debug.ps1 -AuditOnly`も提供。
- 新しい履歴/明示修復Viewsをgit対象外の非表示WPF rendererで通常/520×400に描画し、PNGを目視。確認前の修復無効、確認後の有効、既定の「今回保留」を確認。Win32入力やスタートメニューからの実GUI起動そのものは今回未実施。日常環境のプロセス・実キー・Explorerへ入力していない。
- Release ZIP/Setupをローカルで再生成し配布物検査passed（App/Setup NotSigned、署名導入は#50が未完了）。公開済み配布物を変更せず、利用中環境へSetupを実行していない。専用リンクのtarget/arguments/HotKey読戻し検証は、使い捨てGitHub runnerだけのinstaller smokeへ追加。ローカルでsmokeの実行制限を回避していない。

## 復元差分・バックアップ履歴・直前反映の復旧（2026-11-02、未公開）

- #42: 読取り専用プレビューで項目の追加/削除/変更、変更前後のキー/対象/設定、不足する外部パス、外部競合、元と今回の管理先を表示。既定の全体復元は問題があれば停止し、明示選択した場合だけ、起動先に問題または外部競合のある定義を保持して無効化する。名前/キー/構造等の問題は隠さない。無効化の各理由と未保存候補が置き換わる件数を示す。
- 確認をZIPのSHA・現在configのバイトSHA・最終的な復元configへ対応付け、実行前に再検証する。ZIP差替え、外部設定変更、追加のファイル不足/競合によって部分復元の判断が変われば停止する。プレビュー取消はApply/backupを呼ばない。
- 追加#134: 復元先の現editionの管理フォルダを復元後設定にも保存し、再読込み/次回起動が元PC等の管理先へ戻らないようにした。元の管理先は作成/変更せず、所有マーカーと既存のトランザクション境界を維持。ADR-013に記録。
- #81: 履歴に日時/件数/理由/版/容量/由来とエラーを表示。1件の壊れたZIPは一覧全体から隔離し、検証済み自動receiptと手動/未確認を区別。履歴閲覧は保持管理や削除を実行しない。任意保存場所のZIPは既存ファイル選択で指定できる。
- #83: 今回の起動中に成功した直前Applyのバックアップと保存snapshot/SHAだけを関連付ける。未保存候補/外部変更/改変ZIPは直前復旧を停止。復旧は同じ差分プレビューとRestoreService経由で行い、復元前にもバックアップ・競合確認を実施する。過去session/別PCの「最新ZIP」を自動推測しない。
- 対象Windows11件（復元preview/partial/確認後変更/旧管理先不変/履歴/既存復元/Debug分離/BOM）passed。履歴のInvalidDataException隔離不足をテストで検出して修正後passed。ViewModelの候補保持・確認後外部変更・直前Apply対応/改変拒否/Busy/破損復旧の関連6件passed。履歴表示調整後は該当1件だけ再確認してpassed。最初のテスト記述はxUnit analyzerに合わせ修正した。
- Debug build 0 warnings/errors。git管理外の `local/recovery-renderer` が実App/ViewsをDebug参照し、Window.ShowやApp.Runを呼ばず標準WPFで描画。通常860×690/960×600と狭い520×400のPNGを目視確認。全体復元の問題ありはボタン無効、明示的な無効化モードは確認チェック後のみ有効も検証。履歴列幅/日本語理由を調整して再描画した。
- この描画fixtureはWin32入力・ネイティブファイル選択・キーボードフォーカス・混在DPI/複数モニターの実機検証ではない。実環境のRestore/Explorer再起動/管理外データ変更/製品更新は行っていない。raw PNG/fixtureはlocal配下でgit対象外。既存の関連トランザクション/原本保全テストを実行証拠とする。

## 設定JSON・既存割当の移行/スキャン統合バッチ（2026-11-02、未公開）

- #131: target.kindの先頭/途中/末尾を受理する一方、重複プロパティ・重複kind・未知kind・壊れたJSONを拒否。未対応versionは既存の上書き停止を維持する。Coreの専用8件passed。既存repositoryの読込みで原文を書き換えないことも検証。
- `.lnk`はexeへの変換を止め、元lnkを参照してWindowsが全起動条件を解釈する。通常/最小化fixtureの引数・作業フォルダ・元HotKey・元バイト不変と、解決されたExplorerへの元lnk参照を確認。特殊Storeアプリ等の実起動は未実施であり、#32の残る実例確認を完了扱いしない。
- 移行先キーは明示選択が必須。QWERTY順に候補/外部の使用状況と無効定義件数を表示。外部使用中キーは選択不可。反映時は追加走査範囲を含めて再確認し、検出した競合の元パス・形式・対象を示し、場所表示/同じキーの候補編集へ進める。管理外ファイルを変更しない。
- スキャン範囲の選択/追加、重複統合、名前/パス/キー/エラー検索、キーあり/移行可能フィルターを追加。ディレクトリ単位の走査失敗とリンクの読取り失敗を分離し、reparse pointとその配下ルートを除外して詳細表示する。全ディスク走査を既定にしない。
- Windows対象20件（移行/既存inventory/設定互換性）passed。fixtureの空IconLocationによるCOM拒否と、親ルートに統合される不足ルートという2つのテスト側誤りを修正後passed。追加のreparse・明示キー・カスタム範囲競合・Busyを含む対象10件passed。Debug build 0 warnings/errors。
- Debug GUI: 範囲選択、正常245/読取り失敗3/未走査・除外2という区分、検索で0件への更新、外部使用中キーの無効化とQWERTY順、未選択の追加不可、キャンセルで候補不変を確認。実キー/外部アプリ実行/元キー解除/製品更新は行っていない。競合詳細画面の実機操作、追加フォルダのネイティブ選択は未実施。
- CLIスキャンは exit0 / ok true / complete false / 248件 / 読取り失敗3 / 除外2。GUIと一致し、Debug configのSHA不変。生の元パスや診断データはgit管理外へ保持する。通常終了UIは利用者入力との競合で2回停止し、未保存候補がないことを観測済みの当方起動Debugだけをプロセス終了してCLIを確認。製品プロセスは対象外。
- ロードマップ#1の15 checkboxを統合済みIssueと同期した。一般的な動的HotKey取得や指定範囲外を検出したとはしない。

## 編集・状態表示・実行前プレビューの統合バッチ（2026-11-02、未公開）

- 保存済みと候補の意味的差分を項目ごとに集計。差分なしの反映、未選択の編集/削除/テスト、処理中の変更を停止。外部の設定変更を検出すると候補を保持して上書きを停止する。
- 保存済みの手動監査は候補を保持し、config/link/state/backupを変更しない。保存、リンク監査、Explorer再起動、今回の利用者による実キー確認申告を別に表示。同一キーに複数定義がある場合は有効定義を優先し、件数と明示選択を表示する。
- 編集フォームは種別とアプリ起動方式ごとに必要なラベル/入力/参照だけ表示し、別々の下書きを保持。ドメイン検証による項目別エラー、保存不可、F1説明、F5存在再確認、エラー項目へのフォーカスを追加。通常入力中はファイルシステム/ネットワークを照会しない。
- 絶対パス、フォルダとファイル、作業フォルダ、BAT拡張子を区別。無効エントリは起動先の存在確認を省略し、対象テスト前は有効な対象として再検証する。URIの登録は読取り専用で確認し、未登録候補/判定不能と実起動成功を区別する。
- .lnk/URIの追加引数は既存契約どおり使わず、読み取り専用と理由を表示して既存値を保持。アプリ/BAT等は解決済み起動先・実引数・作業フォルダを先に表示し、明示実行だけで起動。表示後の候補/起動内容の変更は再確認して停止する。通常Webテストは既存の直接起動導線を維持。
- 対象テスト: `TargetPathValidationTests` 4/4、`ShortcutEditorStateTests|EditorStateTests|UriRegistrationTests` 20/20 passed。先行する状態/Busy/Restoreの関連9ケースpassed。全体テストや無関係な拡張テストはローカルで繰り返していない。
- `scripts/run-debug.ps1` build 0 warnings/errors。Debug GUIで空欄の項目別エラー/保存不可、Webへの切替時の不要欄の非表示、アプリへ戻した入力保持、候補保存、選択キー強調、未保存件数、解決済みプレビューとEscapeキャンセルを確認。スクロール導入によるDataGrid列の縮小を発見し、viewport幅を拘束して修正。長い名前の無効fixtureで一覧列の正常表示を再確認。
- 製品configの読取り専用CLI検証は exit 0 / ok true / issues 0、検証前後のSHA一致。Debug fixtureは原本退避後に使用し、終了後にバイト復元・SHA一致。Apply/実キー/Explorer再起動/外部アプリ実行/製品インストール更新は今回行っていない。
- 実ネットワーク共有の接続・起動、複数モニター/混在DPI、小画面全条件は未実施。UNCは絶対パス構文のみ検証。ダイアログのサイズ制限は主画面の作業領域基準であり、これだけで #19/#54/#71 の全条件を完了扱いしない。
- 順不同JSON fixtureでkind metadataの位置による読込み失敗も発見。順序を正したfixtureでは正常表示。互換性課題は別Issue化し、現在の安全停止/原本保全は維持する。

## 初回導入・オフライン案内 / #9/#10/#13/#24/#69（2026-11-02、未公開）

- 空状態からWebサイト/アプリ追加と既存移行を説明付きで案内。新規configなしの場合のみガイドを自動表示し、既存設定では繰り返さない。ガイドは閉じる/ヘルプから再開でき、単に開いても設定を書き換えない。
- ガイドは登録→実対象テスト→反映と明示確認付きExplorer再起動→Manager終了後の実キー確認を案内。保存/テスト/再起動を物理確認済みとはしない。実行処理は既存サービスへ委譲。
- キー一覧が主導線になりCtrl+Altの実入力は不要。既存設定内の使用状況を表示。従来の実入力は既存対象が起動し得ると明示した折りたたみ欄へ残す。hookは追加しない。
- 「使い方」に症状別オフラインヘルプ・復旧案内、製品情報に実行中assembly版、Debug/製品区別とリリース/ヘルプ/報告リンクを表示。ユーザー名・設定・ログをリンクへ自動添付しない。
- Debug build 0 warnings/errors。GUIで空状態/登録後の非表示、一覧でUを選び実HotKeyなしに未保存候補を作成、ガイドの登録前後の操作可否・スキップ/再開・初回自動表示、ヘルプと製品情報を確認。表示 `0.3.1+f005c6b7e7fde66397f72db7c2b5fd88ae664c82` が同じDebug EXEのProductVersionと一致した。
- UI fixtureの候補は保存せず破棄、Debug configの初回probeは原本復元とSHA一致を確認。個人Chrome/製品データ/実HotKey/Explorerを変更せず。ガイドから実行される既存Apply/Shell/ブラウザー起動そのものは今回再試験していない。
- Debugの終了UI操作が一度失敗しビルドがEXEロックで停止したが、正常終了を確認して再ビルドpassed。モーダル操作のUIA cache/geometry制約は再観測とscreenshot-backed操作で解決。未保存の利用者データを強制破棄していない。

公開後記録 [PR #127](https://github.com/Soki0909/windows-shortcut-manager/pull/127) / [CI 41406522663](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41406522663) をmain `40d3dd0a9091c2c61f36b475ab2bfb0bb81837c3`へ、CI範囲判定 [PR #129](https://github.com/Soki0909/windows-shortcut-manager/pull/129) / [CI 41407105770](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41407105770) をmain `d989c39777534a10514b9d7c5dd4d1ecfd99b208`へ統合済み。

## 変更範囲に応じたCI / #128（2026-11-02）

- 所有者の検証絞込み指示をCIにも適用。native git差分とPowerShellだけでWindows/拡張/配置helperを振り分ける。文書/管理記録はruntime/Setup検証を省く。比較元不明・取得不能・未知のbuild入力・workflow/共有設定変更は保守的に全チェック。
- 判定19ケースpassed。文書/Windows/拡張テスト/実拡張ソース/Setup/配置/module/ライセンス/未知ファイル・Windows区切り・比較不能を検証。実際のdocs-onlyコミット差分はWindows/Extension/DeploymentすべてFalse。全PowerShell scriptの構文検査もpassed。
- 範囲判定jobでmaintenance scriptの構文を確認し、Issue操作はCIから実行しない。配置復旧テストは独立job。正式リリースの包括的な検証workflowは変更しない。PR CIではworkflow自身の変更として全jobを確認する。

## 公開後記録の同期 / #120（2026-11-02）

- [公開後索引と毎回のチェックリスト](../releases/POST_RELEASE.md)を追加。準備記録と実際の公開/所有者PC更新を区別し、公開後にmainへ記録する担当・タイミングを定義。
- [v0.3.0](../releases/post-release/v0.3.0.md)、[v0.3.1](../releases/post-release/v0.3.1.md)について、実在するtag/main SHA、成功CI/Release、公開日時/URL、3ペイロードSHA、既存ローカル証拠の所有者PC更新/拡張再読み込み確認を個人パスなしで記録。
- 全6ペイロードを現在のGitHub asset digestと保管済み公式checksumで再照合して全一致。過去のPC更新証拠の追記であり、この作業でPCを再更新していない。公開済みtag/配布物を変更していない。
- `scripts/sync-roadmap.ps1` は子Issueの実open/closedに合わせ#1のcheckboxだけを更新し、前後を再照合する。本文の製品スコープは変更しない。履歴上の初期105件と後発Issueの現在総数を混同しない。

## version header数値境界 / #125（2026-11-02、未公開）

- 継続レビューで `TryGetInt32=false` の数値が専用の未対応形式エラーにならず、明示復旧で上書き可能になる境界を発見してIssue化。
- int32範囲外、小数、指数表現を含む未対応versionを全書込み経路で拒否し、極端な数値の表示を32文字に制限。
- `dotnet test tests/ShortcutManager.Windows.Tests/ShortcutManager.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ConfigCompatibilityTests --nologo`: 11/11 passed。旧設定/marker/stateのバイト不変、4096桁数値の診断長、通常schema 1と破損JSON復旧の維持を確認。その他テストは反復せず。

## unpacked拡張の安全な更新 / #119（2026-11-02、未公開）

- [ADR-012](../architecture/ADR-012-unpacked-extension-update.md)、[更新/復旧手順](EXTENSION_UPDATE.md)。直接source上書きをやめ、新旧公式ZIP+公開SHA確認、完全stage、before/after journal、旧フォルダ退避、同じパスへの切替、再実行可能な復旧を実装。未知/改変ファイルは保護し、旧不要ファイルは旧ZIPとの一致でのみ整理。既存keyは維持。
- `scripts/test-extension-deployment.ps1`: 28/28 passed。copy/write例外6地点、プロセス強制終了7地点と復旧中2地点、同一版再実行、旧版バイト照合、unknown/空フォルダ保持、改変/ZIP/hash/key/junction拒否、中断後の編集・旧退避改変・metadata破損の書込み停止を隔離fixtureで確認。後二者は復旧不能を成功扱いせず証拠を保持する。
- PowerShell parser / `git diff --check` passed。CIへ専用テストを追加。ディスク更新完了とChrome実行版確認を明確に区別。実際の個人Chrome・製品環境への更新は正式リリース時に実施する。
- 検証は変更したscriptだけに限定。コード・UI・manifestの動作変更がないため、ローカル.NET全体/Chrome runtime回帰を重ねていない。物理電源断の保証ではなく、アプリケーションプロセス中断を実証した。

設定互換保護 [PR #123](https://github.com/Soki0909/windows-shortcut-manager/pull/123)は[CI 41403674393](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41403674393)成功後にmain `c2d57748a865434445f8f6c6ae3bfcc379a548b0`へ統合。[main CI 41404154559](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41404154559)もpassed。

## 設定互換性 / #53（2026-11-02、未公開）

- [ADR-011](../architecture/ADR-011-config-compatibility-and-downgrade.md)でschema 1/protocol 1の継続対応、将来の隣接段階移行・移行前バックアップ・ジャーナル復旧・downgrade拒否を定義。実在しない旧schemaを作らず、全既存公開版は無変換で読み込む。
- Configのversion headerを未知メンバー/targetの読込みより先に判定。未対応schema 0/2・protocol 2に対するApply/Restore/SaveAtomic/初期化/監査は書込み停止、config/marker/state不変。破損JSONの既存明示復旧は維持。
- `dotnet test ShortcutManager.sln -c Release --no-restore --nologo`: Core 27/27、Windows 109/109 passed。Debug build 0 warnings/errorsとCLI 6プロセスpassed。
- Debug限定で元設定を退避してschema 2を一時投入。computer-useで対応新版の案内、初期化と変更反映ボタンの無効化を目視・accessibility両方で確認して正常終了。元Debug configをバイト単位で復元しSHA一致。製品版設定/管理リンク/Chromeは変更せず。

第1単位は [PR #122](https://github.com/Soki0909/windows-shortcut-manager/pull/122)を[CI 41401906336](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41401906336)成功後にmain `14236a8cc29b14d1c6c66661f0912e7a22131b9c`へ統合。[main CI 41402992250](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41402992250)もpassed。公開版の更新はまだ行っていない。

## v1.0.0へ向けた第1単位（未公開、2026-11-02）

- #41: 自動生成receiptとZIP名・SHA-256・manifestを照合できるapply/restoreだけを整理する。手動、外部コピー、旧版/不明ZIP、改変/破損receiptは保存。時刻が同一でも順序が安定し、最低1世代を保持する。
- #36: ログ保存不能を操作結果から分離して警告表示。設定読込不能+ログ不能+初期化バックアップ不能のfixtureで元設定を保全し反映停止を確認。Startup/Dispatcher例外は保存せず通知・終了し、再起動時ジャーナル復旧へ委ねる。
- 追加発見 #121: コマンド失敗は非0。成功/不正設定/引数欠落/結果指定なし/未知引数/結果出力不能のDebugプロセス6件が終了し、入力不変。CIに `scripts/test-cli.ps1` を追加。
- #51: 所有者が推奨を採用しMITを追加。Windows ZIP/SetupはMIT、依存物表、ロゴ出所、実際の.NET runtime LICENSE/NOTICESを含み、拡張ZIPにもMITを同梱。欠落を配布物検査で拒否。
- #90: Setupに製品/サポート/更新URLと日英の設定保持・別途Chrome拡張導入説明を追加。per-user/PrivilegesRequired=lowestを維持。
- 実行: Release `dotnet test ShortcutManager.sln --no-restore` Core 27/27、Windows 104/104。Debug build 0 warnings/errors。Node 22/22（拡張コード変更なし）。ローカルpackage/Setup、7サイズのEXE/Setupアイコン、全3SHA、ZIP/manifest/ライセンス検査passed。署名はNotSigned。インストール試験は使い捨てCI runner限定で、main統合前PR CIが実行する。
- Debug通常画面・分離識別表示をcomputer-useで目視確認し正常終了。Apply mutex保持中の起動停止、多重起動停止も観測。ログ警告は隔離ViewModel回帰で確認し、GUI故障注入は未実施。製品設定・管理リンク・Chromeには変更せず。
- 追跡: [v1.0.0計画](../implementation/PLAN_v1.0.0.md)。この時点では正式版の公開・製品更新は未実施。

## v0.3.1 Apply/Restore safety release (2026-11-02)

- Core 27/27, Windows 98/98, extension 22/22 passed. All launch properties are checked before and after commit; the new post-verification kill checkpoint recovers the previous complete state. Transaction test classes share a nonparallel collection because production deliberately uses a single Apply mutex.
- Fake Shell host covers exit timeout, explicit cancellation before/after termination, return timeout, termination error, failed recovery and another-session guard. No live desktop fault injection.
- Missing external BAT and referenced lnk prevent Restore without changing current config. Restored-result and Shell-pending notices survive reload and failed restart; successful restart clears only the pending flag.
- Native Debug GUI covered backup scope, actual ZIP save, restore preview, empty-config Restore, restart offer and decline with persistent notice. No production config was copied to Debug; no owner keys or live Shell restart changed by this check. Duplicate notice/status text was simplified afterward and focused tests rerun.
- Release ZIP/Setup, version/manifest contract, seven-frame icons and three artifact SHA checks passed locally. App/Setup remain intentionally NotSigned. Final CI/publication/installed update is tracked through [RELEASE_0.3.1.md](../implementation/RELEASE_0.3.1.md) and GitHub workflow results.

## v0.3.0 responsiveness release (2026-10-30)

- Core 27/27, Windows 70/70 and extension 22/22 passed. Local ZIP/Setup, all three digests, seven-frame branding and unsigned-signature policy checks passed.
- Owner physical-key measurements (ten per phase) reduced median target-code entry from 3086.43 ms to 139.92 ms after explicitly approved removal of two unresponsive application processes. Direct-launch baselines and limitations: [latency evidence](LATENCY_2026-10-30.md). No claim that product code alone removes every OS delay.
- Dedicated Chrome run chrome-live-1793343256430 passed all checks, including exact Popup instance selection, injected transport/storage failure and state rollback, Worker/cold start and cleanup. No personal Chrome profile used.
- Debug diagnostic dialog rendered and completed a real read-only probe; candidate labels, retry and normal close were checked. The initial background capture did not show the target; activation and a fresh target capture resolved it before any input.
- Scope and publication/update tracking: [RELEASE_0.3.0.md](../implementation/RELEASE_0.3.0.md).

## v0.2.0 / v0.2.1 release preparation (2026-10-30)

- Public target is v0.2.1. v0.2.0 publication was cancelled before draft creation to fix cleanup ordering. Windows 69/69 passed including the eleventh kill checkpoint after invalidating journal intent but before deleting snapshots. The v0.2.0 tag is preserved; published v0.1.1 assets were not modified.

- Clean-runner [CI 41004714567](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41004714567) passed Release tests, Debug/driver builds, packaging, branding, SHA/signature policy and Setup install/CLI/uninstall. Final dedicated Chrome run chrome-live-1793335333677 passed 17/17 including unhandled-exception monitoring and cleanup.

- Core 27/27, Windows 68/68 and Node 13/13 passed. Child processes are killed at ten Apply checkpoints; a retained mutex observer ensures real abandoned ownership. Recovery is killed and repeated, first-Apply interruption is recovered, and corrupt snapshots/post-crash edits stop all recovery writes. Instance leases are tested across processes and independently for Debug; delayed Shell work rejects parallel ViewModel changes.
- Dedicated CfT live suite covers real API/group/window operations, Worker stop/restart, cold process startup and generated links. Prior owner physical-key evidence remains in the #110 matrix. Normal personal Chrome profiles are excluded.
- Debug Settings compatibility guidance rendered successfully. Native close action returned `coordinate input geometry is unavailable`; only the test Debug process was stopped for cleanup, without saving settings.
- Local ZIP/Setup generation, seven-frame branding and all three SHA-256 checks passed. Both signatures are explicitly `NotSigned`; a tampered checksum is rejected. Clean-runner installer and draft-download/publication gates are executed by CI/Release, with workflow state as the authoritative evidence.
- Release scope, remaining publication gates and compatibility: [release tracker](../implementation/RELEASE_0.2.0.md), [release notes](../releases/v0.2.0.md). Installed product and personal Chrome remain unchanged by development tests.

## Issue #110 and nine safety improvements (unreleased)

Branch: `codex/ten-issues-110-safety`. Details: [implementation tracker](../implementation/ISSUES_110_AND_SAFETY.md), [ADR-008](../architecture/ADR-008-foreground-independent-group-toggle.md).

- Owner approved foreground-independent toggling; the original foreground condition is P3 issue #112. The native foreground prototype was withdrawn. Final product route is still lnk → chrome.exe → Dispatcher, with new toggle-group and compatible legacy focus-group/Popup.
- Final Release tests: Core 27/27 and Windows 53/53. Node 12/12. Regression fixtures cover all nine safety issues, including incomplete-but-valid JSON (`{}`), oversized prior snapshots, aggregate saved config size, dangling links, excessive ZIP entries/expanded data, exceptions during compression, and UTF-8/UTF-16 BOM restore compatibility.
- Live `chrome-live-1793260987977`: 22 recorded checks passed, including owner physical Ctrl+Alt+U in all four source/state combinations with Manager closed, Dispatcher removal, member preservation, last-tab return, sequential/overlapping requests, real Worker stop/restart, missing/duplicate groups, multiple windows, another group selected, Popup/legacy compatibility, and a cold Chrome process start.
- The real HotKey used a byte-identical Desktop copy of the actual Debug Apply-generated lnk because this Shell state did not register the new Start-menu key immediately. The copy and test HotKey were removed, the empty prior Debug config restored, and external HotKey readback fields matched before/after. This does not change the product's Start-menu output or its known Shell/Windows restart registration conditions.
- Debug UI: normal screen and recovery-mode layout rendered through run-debug.ps1. Corrupt config disabled Apply and exposed recovery actions; restoring original config bytes and clicking Reload re-enabled Apply and hid initialization. Debug exited. Unit tests also verify Restore and explicit initialization recovery paths.
- All Chrome work used an isolated CfT 148 profile and disposable extension copy. No installed product/config/managed link or personal Chrome extension was updated. Raw scans/paths remain gitignored. Test-only response observation and driver are excluded from product artifacts.
- Final ZIP/Setup generation passed; verify-branding passed all seven original ICO frames for app and Setup and matched all three SHA-256 values. Product ZIP excluded the withdrawn dispatcher, Debug binaries and live fixture. Packaged EXE command-mode validation passed. These are unreleased local artifacts, not replacements for published v0.1.1. Current CI/merge/Issue state: [PR #111](https://github.com/Soki0909/windows-shortcut-manager/pull/111).
- Local final hashes: Windows ZIP `2eeecab850a1a1cd01b424bb84de9a8d7e30f111a64c185ddf71d585628bf98a`; extension ZIP `8f2001e962a680c1cecd646d1c87446d3b0ea43ee2431228118c1fc5a56a411a`; Setup `4ffea772085e8d5ea6137c728a30fdc4bdcf23d3dcd65b90afeaae8ff94d1974`.
- Detailed condition mapping: [ISSUES_110_SAFETY_2026-10-29.md](ISSUES_110_SAFETY_2026-10-29.md).

## Development/product isolation (2026-10-28, unreleased)

- `scripts/run-debug.ps1` builds and launches the exact `ShortcutManager.Debug.exe`; observed process title `Shortcut Manager [DEBUG]`, responsive process. Release solution build succeeds with the original `ShortcutManager.exe` name. Both builds report zero warnings/errors.
- Debug uses `ShortcutManager.Debug` LocalAppData and `Shortcut Manager Debug` managed links. A copied config's managed-folder override cannot redirect Debug writes. Unit/real-COM regression coverage verifies independent paths, unchanged Release path overrides, Apply/Restore preserving product sentinels, and conflict rejection preserving prior debug config/link bytes.
- Windows tests: 24/24 passed in Release and Debug. Debug Core 15/15 and Chrome 10/10 passed. `package.ps1 -Configuration Debug` rejected its parameter before touching publish output.
- Real Debug launch preserved all 22 SHA-256 values covering the installed EXE, product config and managed files. It did not load the installed config automatically or update the installation. The owner-requested workflow is persisted in AGENTS.md and ADR-007.
- GUI screenshot/interaction verification was interrupted by the owner's physical Escape. No further Computer Use input was issued. D-badge rendering and simultaneous taskbar-group appearance remain visual checks; process identity/title and storage isolation are verified automatically. The previously owner-confirmed v0.1.1 product logo taskbar check remains passed.

## Issue #8 — product logo (2026-10-28)

- Owner confirmed the newly packaged v0.1.1 displays the new logo in the taskbar on 2026-10-28. This passes the requested taskbar check; the installed old EXE remains unchanged until the owner runs the update installer.
- Follow-up for v0.1.1: the installed v0.1.0 EXE has no RT_GROUP_ICON resources (0 matching logo frames), and the Start-menu launcher points to that installed EXE with the default icon reference. This explains the old taskbar appearance before installing an update; the packaged build is a separate executable. No cache reset or Explorer restart was needed to establish the cause.
- v0.1.1 candidate: Core 15/15, Windows 20/20, Chrome 10/10 passed; Release solution build passed with zero warnings/errors; package and installer generation passed. All seven new EXE/Setup frames and all three artifact checksums passed `scripts/verify-branding.ps1 -Version 0.1.1`. Application, backup metadata, About, extension and packaging versions are aligned to 0.1.1; protocol/schema remain 1.
- Owner supplied the SVG, transparent PNG exports and multi-resolution ICO and requested integration. Originals are retained under `assets/branding/` and application `Assets/`; provenance is documented without asserting a separate third-party license or independent rights clearance.
- Release solution build passed with zero warnings/errors. Initial sandbox build was interrupted; the normal-host retry passed.
- `scripts/package.ps1 -Version 0.1.0` and `scripts/package-installer.ps1 -Version 0.1.0 -SkipPublish` passed with Inno Setup 6.7.3.
- `scripts/verify-branding.ps1 -Version 0.1.0` reads native RT_GROUP_ICON/RT_ICON resources as data: EXE and Setup each contain byte-identical copies of all supplied 16/24/32/48/64/128/256px ICO frames. All three generated artifact SHA-256 values match `SHA256SUMS.txt`. CI and Release now run this verification.
- Computer Use inspection of the executable under `artifacts/publish/win-x64/` confirmed the logo in the title bar and header, an intact layout and a matching startup audit. The installed older executable was distinguished from the new build by its full returned process path.
- Existing installer Start-menu/desktop launchers and `UninstallDisplayIcon` use `ShortcutManager.exe`, which now embeds the logo. The initial local implementation did not install or publish an update. No Explorer icon-cache reset was performed.
- Remaining manual checks: Alt+Tab and the visual matrix across multiple DPI settings, and installed Start-menu/uninstall-list rendering after an upgrade. The owner limited the follow-up visual check to the taskbar, which passed. Do not treat native-resource validation as a passed full visual matrix.

## Environment

- Windows x64 target machine
- .NET SDK `10.0.403` (2026-10-27 follow-up)
- Node.js `v22.15.0`
- Chrome: `C:\Program Files\Google\Chrome\Application\chrome.exe`
- Product version / config schema / Dispatcher protocol: `0.1.0 / 1 / 1`

## Automated verification

| Area | Command | Result |
|---|---|---|
| Core + Windows | `dotnet test ShortcutManager.sln -c Release --no-restore` | Core 15/15, Windows 20/20 passed |
| Extension | `node --test tests/ChromeExtension.Tests/protocol.test.js` | 10/10 passed |
| Version/policy | `scripts/verify-version.ps1 -ExpectedVersion 0.1.0` | Passed; MV3, no commands/host_permissions/content_scripts/externally_connectable |
| Release build | `dotnet build ShortcutManager.sln -c Release --no-restore` | Passed with 0 warnings / 0 errors |
| Packaging | `scripts/package.ps1 -Version 0.1.0` | Passed; self-contained EXE and root manifest verified |
| Packaged GUI smoke | Start self-contained EXE, wait for window, request normal close | `Shortcut Manager` window appeared within 1 second; responding; clean exit; 0 remaining processes |

Tests cover config roundtrip/polymorphism, Unicode, schema/URL/Extension ID validation, disabled invalid target behavior, GUID/HotKey conflicts, filename sanitation, Dispatcher percent encoding, real WScript `.lnk` roundtrip, `.url` numeric HotKey decoding (`1602 → CTRL+ALT+B`), scan root dedupe, managed ownership rejection, staging/read-back/backup/commit, idempotence, external conflict blocking, forced rollback, Restore through the Apply transaction, and Chrome protocol/group/tab selection.

## 2026-10-27 startup audit and installation

- GUI startup now compares persisted config with the actual owned links: existence, resolved target, arguments, working directory, canonical HotKey, icon, description and window style. Extra obsolete links are detected. COM's equivalent icon forms (`exe` / `exe,0`) are normalized to avoid repeated false repairs.
- Differences are repaired through the existing backup/stage/rollback transaction, followed by an offer to restart Explorer. Healthy startup does not write or create a backup. First launch without config and invalid-config load do not auto-apply an empty configuration. Unexpected files/subfolders, missing ownership, invalid configuration or external conflicts stop automatic repair.
- Real COM tests modified target arguments/HotKey, added an obsolete link and removed an expected link, then verified repaired equality and backup existence. The exact startup reconciliation service also passed healthy no-write and external-conflict no-write cases. Windows tests: 20/20; Core: 15/15; Chrome: 10/10.
- `scripts/package-installer.ps1 -Version 0.1.0` generated self-contained portable and extension ZIPs plus `ShortcutManager-Setup-win-x64-v0.1.0.exe`. Build tool: Inno Setup 6.7.3, downloaded from its official repository, SHA-256/signature checked. No application dependency was added. CI/release workflows now build and publish the Setup artifact alongside ZIPs.
- Actual per-user installation, same-version update/reinstall, uninstall and final reinstall all exited 0. Windows uninstall registration and Start-menu launcher were verified. All 404 installed payload files matched the published files by SHA-256. Config plus all 20 managed-folder files (19 links and marker) remained byte-identical through the lifecycle. User data/backups and managed runtime links are deliberately preserved on uninstall.
- Installed app startup logged `repaired=False, differences=0` for the real saved configuration. Launching from the installed Start-menu shortcut opened the expected executable; native accessibility inspection confirmed the visible status `起動時監査: 保存済み設定とショートカットは一致しています。`. Normal close passed.
- Installer is unsigned; Authenticode signing and Store publication remain separate owner-credential work. No additional manual gate remains for these features. Audit verifies persisted link data, not Explorer's private active HotKey table or other processes' runtime key registrations.

## 2026-10-01 live-refresh and UI follow-up

- Apply reports changed shortcut lifecycle events to Explorer synchronously: deletion for links moved out of service, then creation/update for committed links, plus the managed-directory update. Rollback emits the matching reverse lifecycle. Windows regression tests verify replacement, removal, and rollback notification order. Live testing proved these notifications are necessary for Shell cache coherence but insufficient to rebuild Explorer's `.lnk` HotKey registration table.
- The packaged native GUI loaded the current host configuration and displayed three physical keyboard rows in `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` order. The refreshed card, input, table, status, and action hierarchy were inspected in the running Windows application; opening the Q editor preserved the existing data.
- A temporary `F=一時メモ帳テスト` application entry targeting `C:\Windows\System32\notepad.exe` was added and applied successfully. Physical `CTRL+ALT+F` did not work in the existing Explorer session and worked after Windows restarted. This proves the `.lnk` was persisted correctly and the notification-only approach is insufficient, but does not by itself prove that restarting Explorer alone is sufficient.
- The GUI now offers a warned, consent-based reload of only the current session's Shell Explorer after Apply and as a separate recovery action. The implementation resolves the Shell PID through `GetShellWindow`, refuses another session, restarts Explorer, waits for the replacement Shell, and reports the old/new PIDs. It deliberately says only that Shell was reloaded and asks the owner to verify the HotKey; it does not claim activation before that physical check.
- Public API research found no documented command equivalent to the shortcut Properties dialog's HotKey Apply behavior. `IShellLink::SetHotkey` and `IPersistFile::Save` persist the `.lnk`; `SHChangeNotify` provides file/directory lifecycle notifications; neither documents rebuilding Explorer's active HotKey table. `RegisterHotKey` would require the registering window/thread to remain alive, which conflicts with the product's non-resident requirement. See [IShellLinkW::SetHotkey](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinkw-sethotkey), [Shell Links](https://learn.microsoft.com/windows/win32/shell/links), [SHChangeNotify](https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify), and [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey).
- On 2026-10-27 the owner added `U=一時メモ帳テスト` and Apply succeeded with 20 generated links plus automatic backup `20261027-112732538_06e74b58021041a6a10d43187fd92769.zip`. The consent-based Explorer action changed the Shell PID from `12764` to `27160` without restarting Windows. Physical `CTRL+ALT+U` then opened Notepad, passing the no-Windows-restart live gate. The app incorrectly reported failure because the old PID disappeared between Shell discovery and process metadata access; the implementation now treats that already-exited race as idempotent and only reports success after observing a different current-session Shell PID.
- The first reporting fix covered termination, but the next integrated run still reported failure while Explorer successfully changed `27160 → 26816`. The remaining exception came from resolving or waiting on a stale Shell-window PID after process exit. `GetShellProcess`, `Terminate`, and `WaitForExitAsync` now all treat an already-exited process as an expected race; session mismatch and failure to observe a different replacement PID remain hard failures.
- The fully race-tolerant rerun succeeded and logged Explorer PID `26816 → 27328`. After removing the temporary U through the product transaction (backup `20261027-113726005_13d0087eb0454aa78eca3ab09c77fe80.zip`), another successful reload logged `27328 → 32368`; the owner physically confirmed `CTRL+ALT+U` no longer did anything. Product scan found exactly 19 managed links, zero U entries, and the persisted config returned to 19 enabled shortcuts with its original SHA-256 `283db1121cfd85e2fb4fe9538a5bb51f922f5bc085d5e63d3dbb71ab4a0dc936`.
- The owner also observed an unnecessary foreground Explorer window during Shell recovery. The reloader now gives Windows' automatic Shell restart a five-second grace period and calls `explorer.exe` only if no replacement Shell appears. Unit coverage distinguishes the auto-restart path (no explicit launch) from the recovery fallback.
- Current automated follow-up results: Core 15/15, Windows 17/17, Chrome 10/10, and Release build 0 warnings/errors. The final automatic-recovery run logged `success=True`, Explorer PID `32368 → 45204`, and completed in about 0.9 seconds. The owner confirmed that no additional file-manager window appeared in front. The physical activation, physical removal, success-reporting, and no-foreground-window live gates all passed.

## Current-machine evidence

- Product command-mode inventory read 240 unmanaged `.lnk/.url` items before Apply, 243 total after safe Apply, and 259 total after full Apply (19 managed shortcuts).
- Chrome has currently open groups named `研究` and `7脱` (observed via connected Chrome tab inventory).
- Existing proven PoC route has `CTRL+ALT+R` at `Windows Shortcut Manager PoC\Research - Chrome Tab Group.lnk`.
- Formal Tab Bridge v0.1.0 was copied over the same unpacked-extension path with timestamped directory backups, preserving extension ID `plmgefhiiengedjjpiheeobbcclokojf`. Manifest/file read-back passed.
- Canonical Protocol v1 Dispatcher URLs for `研究` and `7脱` were launched directly. Before and after each call Chrome inventory stayed at 19 tabs, target groups remained present (3 `研究` tabs and 7 `7脱` tabs), and zero `chrome-extension://` Dispatcher tabs remained. The old PoC parser would reject `action=focus-group`, so this also proves Chrome loaded the formal generic implementation.
- External `作業開始` BAT was backed up and updated to exactly six lines opening ChatGPT Web, Notion, and Google Drive, with no Gmail/Slack/LINE/TimeTree. Read-back verification passed.
- Gitignored `local/bootstrap-config.json` represents the requested A-Z layout. Native ChatGPT App and Slack entries are disabled because those applications were not detected.

## Real Apply / Backup / Restore

The actual product `ManagedShortcutApplier` was used against the default user Start Menu managed folder; no hand-authored final `.lnk` was used.

1. Preflight confirmed the managed folder and config did not exist.
2. Nonconflicting desired keys `I=Gemini`, `L=DeepL`, `V=Canva` were applied.
3. Apply result: success, 3 generated links, config hash `c492d4293e657f273949ba5ca11102060ba4a35e2f78b52883d6aabdc7516324`.
4. Product scan read back each managed `.lnk` as `CTRL+ALT+I/L/V`, target `explorer.exe`, with the expected quoted URL and no parse errors.
5. Same config was re-applied successfully (idempotence).
6. A restore probe changed managed state to one `I` link.
7. `--restore-backup` validated the backup, automatically backed up current state, and restored all 3 links through the same Apply transaction.
8. Forced commit failure integration test verifies prior config and link bytes are restored; unmanaged nonempty folder protection test verifies refusal without the marker.

After explicit owner authorization, the final conflict migration and full Apply were executed:

9. `scripts/migrate-unmanaged-hotkeys.ps1` created `local/migration-backups/unmanaged-hotkeys-20260930-212648408.zip`, containing the manifest plus all 16 originals. ZIP SHA-256: `6686ae0e96594c5c2cc784d3b8353e1537c1467337ac2653844e1d95c411dded`.
10. Product scan found all 16 exact source paths and zero remaining HotKeys.
11. Full `--apply-config local/bootstrap-config.json` succeeded with 19 generated links and automatic backup `20260930-212800778_2c224551a3ab481ebe7bc7cc67edc26d.zip`.
12. Persisted config SHA-256 is `283db1121cfd85e2fb4fe9538a5bb51f922f5bc085d5e63d3dbb71ab4a0dc936`, matching the Apply result.
13. Post-Apply product scan found 19 managed links and all 19 expected `CTRL+ALT` key letters, with no missing or extra managed key. External HotKeys C and K remain, but do not overlap the configured set.
14. The migration rollback error path was hardened to continue restoring all changed files and preserve both original and restore-error diagnostics. PowerShell AST parsing reported zero errors; a post-migration `-PlanOnly` revalidated all 16 exact paths without writes.
15. Live use initially reported stale `CTRL+ALT+G → 学生ポータル` behavior despite correct file read-back (`G=GitHub`, `P=学生ポータル`, old files empty). Apply and migration now send synchronous per-item and directory Shell change notifications after changes and rollback. Tests passed and the 19-link config was reapplied successfully with backup `20261001-013805033_c68bd4408f504f63b9dccb5fb39772ca.zip`.
16. Explorer process `15756` started at `2026-09-27 17:01:45`, before the `2026-09-30 21:26:49` migration. This supports the diagnosis that the current Explorer session may retain pre-migration HotKey registrations; a sign-out/restart is the remaining clean invalidation test if live G/P remains stale.
17. Owner live verification after Windows restart passed: `CTRL+ALT+G → GitHub` and `CTRL+ALT+P → 学生ポータル`. This confirms the managed links are correct and the earlier G behavior was retained pre-migration Explorer state.
18. Owner final live verification passed all requested representative paths with Shortcut Manager closed: `R=研究`, `X=7脱`, `Z=作業開始`, `G=GitHub`, `P=学生ポータル`, and `A=LINE`. The R/X group routes focused Chrome correctly and left no Dispatcher tab behind.

## WPF GUI smoke

Final audit initially found a real startup crash: the read-only DataGrid projection properties were being bound with the WPF default TwoWay mode. The columns now explicitly use `Mode=OneWay`. After rebuilding and repackaging, the self-contained `ShortcutManager.exe` displayed a responsive `Shortcut Manager` window within one second, accepted a normal window-close request, exited cleanly within five seconds, and left zero processes. The packaged command mode also completed a real scan of 259 shortcuts. The follow-up native inspection verified the modern UI, physical three-row key order, existing-config load, editor interaction, and successful Apply status.

## Full-layout conflict migration

The full desired Apply was initially blocked because these 16 enabled keys were owned by unmanaged shortcuts: A, B, D, E, G, M, N, O, P, Q, R, T, W, X, Y, Z. Important paths included the old PoC R shortcut, old Gemini X shortcut, and old `ctrl_alt_z.bat.lnk` Z shortcut.

Full evidence with exact paths is stored only in gitignored `local/inventory.json` because it contains owner-specific absolute paths.

`scripts/migrate-unmanaged-hotkeys.ps1` was added as the recoverable migration mechanism. Its `-PlanOnly` run first validated all 16 exact paths. After explicit authorization, elevated execution backed up all originals and cleared only their HotKey fields; it did not delete any unmanaged shortcut. Read-back found zero remaining HotKeys on the 16 targets.

## Manual/live verification

- Passed after Windows restart: `G=GitHub`, `P=学生ポータル`, `R=研究`, `X=7脱`, `Z=作業開始`, and `A=LINE`.
- Representative HotKeys continued to work with Shortcut Manager closed, confirming the Manager is non-resident and Windows `.lnk` HotKeys are the runtime trigger.
- R/X focused their intended Chrome groups, brought Chrome forward, and left no Dispatcher tab behind.
- Native Windows UI inspection passes for the modern layout and edit flow. The integrated Apply → consent prompt → Explorer PID change → physical HotKey check passed without restarting Windows. Corrected success reporting, temporary U removal, and automatic Shell recovery without an extra foreground window also passed. No manual gates remain for M9/M10.

## Publication status

### GitHub Release v0.1.1 — published 2026-10-28

- [PR #107](https://github.com/Soki0909/windows-shortcut-manager/pull/107) was reviewed with no blocking findings and merged into main after [PR CI 40620906065](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40620906065) succeeded. Issue #8 closed on merge.
- Source/tag commit: `b838d0692002c42857725eaf740c000ef9f3c6be`. [Main CI 40621198679](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40621198679) and [Release 40621230598](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40621230598) succeeded. Release passed Core 15/15, Windows 20/20, Chrome 10/10, ZIP/Setup generation and the seven-frame icon verification.
- [Shortcut Manager v0.1.1](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v0.1.1) published at 11:31 JST as a normal release. v0.1.0 artifacts/tag were preserved.
- Independently downloaded all four published attachments, matched all three payload hashes against both the published checksum file and GitHub asset digests, verified the Windows ZIP contains the expected EXE/DLL and the extension ZIP has a root manifest with version 0.1.1, then confirmed every downloaded EXE/Setup icon frame matches the original ICO.
- Owner confirmed the v0.1.1 taskbar logo on the local packaged build. Local installed v0.1.0 was not upgraded as part of publication; use the new Setup EXE to update it.

| Published artifact | Bytes | SHA-256 |
|---|---:|---|
| `ShortcutManager-Setup-win-x64-v0.1.1.exe` | 46,626,544 | `fc5888e86fd3e9b293a39c8edc153e401bc8762b46357c99f5851dad3c868d54` |
| `ShortcutManager-win-x64-v0.1.1.zip` | 65,424,881 | `a8c3579c72e8500b2403074d96886b29285d87df290eafa5c51c2f5dca1fb977` |
| `ShortcutManager-TabBridge-v0.1.1.zip` | 9,767 | `41078aebd2feac3a5a656b13ac1c5b262f805a1b7b12d4e46d2fdc8de34d0567` |

### GitHub Release v0.1.0 — published 2026-10-27

- Release: [Shortcut Manager v0.1.0](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v0.1.0), published at 12:50 JST, normal release (not draft/prerelease).
- Tag source: `7f0201ef875ec3edfcd14c26fd9cb0b7c652e805`, fast-forwarded onto `main` before tagging.
- [CI run 40446133147](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40446133147) and [Release run 40446184352](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40446184352) succeeded. Release runner passed Core 15/15, Windows 20/20 and Chrome 10/10, then built ZIPs and the installer with Inno Setup 6.7.3.
- Published attachments: Setup EXE, portable ZIP, TabBridge ZIP and `SHA256SUMS.txt`. Public artifacts were rebuilt on GitHub and have their own hashes; the local-build hashes below are historical local verification evidence.
- Downloaded all four public attachments independently, matched all three payload SHA-256 values against the public checksum file and GitHub asset digests, and verified the Windows executable and extension root manifest are present in their ZIPs.

| Published artifact | Bytes | SHA-256 |
|---|---:|---|
| `ShortcutManager-Setup-win-x64-v0.1.0.exe` | 46,556,702 | `181a39fe1d7a32d5bacc9d6aeba8e4c2aea023231e8a2034cb9204b3323dff20` |
| `ShortcutManager-win-x64-v0.1.0.zip` | 65,293,356 | `08452e8e51e2bb80c3afb8ec126d031fefb5533f9207b54a74864d62263eabd1` |
| `ShortcutManager-TabBridge-v0.1.0.zip` | 9,767 | `968072e14ca2e3931cf14bc9a6b70e4d6e1a8eb85cd4e0e9d76593322badaad0` |

Final local artifacts:

- `artifacts/ShortcutManager-win-x64-v0.1.0.zip` — 63,237,908 bytes — SHA-256 `459ec99bae72f000d9344c786630617e32cd2d51a8bf240e00141f05d99f627f`
- `artifacts/ShortcutManager-TabBridge-v0.1.0.zip` — 9,632 bytes — SHA-256 `4b911cff56d1023beb95b053e19461723291761ced775fb7967cec58798a949e`
- `artifacts/ShortcutManager-Setup-win-x64-v0.1.0.exe` — 46,570,164 bytes — SHA-256 `ce1d40d3560697f200b6a396f753cfaafd080bd5464abe7ee9c767ff66e3cee0`
- `artifacts/SHA256SUMS.txt`

ZIP inspection verified `ShortcutManager.exe` is present (and the old `ShortcutManager.App.exe` name is absent). Extension ZIP has 16 entries, `manifest.json` at root, and no development-only `package.json`.

- GitHub Release artifacts: credential-free generation is implemented locally and in Actions.
- Git remote is `https://github.com/Soki0909/windows-shortcut-manager.git`. After explicit owner authorization, initial commit `b76312d` (`feat: complete Shortcut Manager v0.1.0`) was pushed to `origin/main` on 2026-10-01.
- Chrome Web Store upload/publish: not performed; requires owner developer account, listing, consent, and review.
- Microsoft Store/MSIX: not performed; requires owner account and packaging/signing decisions.
- Authenticode signing: not performed; no certificate was supplied.
- GitHub repository push: completed to `origin/main`. Repository visibility was not changed. GitHub Release v0.1.0 was subsequently published on 2026-10-27 at the owner's explicit request, as recorded above.
- Final audit on 2026-10-01 reran all automated gates successfully, verified both artifact hashes against `SHA256SUMS.txt`, inspected both ZIP structures, matched the packaged scan's exact 19 managed HotKeys, and confirmed zero resident Manager processes.
- Final follow-up audit on 2026-10-27 regenerated both packages from the completed M9/M10 code, independently matched both SHA-256 values, inspected the 404-entry Windows ZIP and 16-entry extension ZIP, and launched the final self-contained GUI. It displayed `Shortcut Manager`, responded, and exited normally within five seconds. All M9/M10 implementation and live gates are complete; commits are pushed to `origin/codex/live-refresh-modern-ui`.
