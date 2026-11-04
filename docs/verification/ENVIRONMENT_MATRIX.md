# 対応環境と検証マトリクス / #54

更新: 2026-11-05。正式v1.0.0の全条件がpassedした表ではない。実行済み・未検証・配布対象外・条件付きの区別を公開する。公式資料の取得点とSetup変更の根拠は[Issue #54の検証記録](ISSUE_54.md)を参照。

## 配布要件と確定前の条件

- 要件定義の対象はWindows x64。現在の成果物はwin-x64のself-contained WPFアプリ/Setupで、通常起動に.NET SDKの追加導入や管理者昇格を要求しない設計。
- Setupは`PrivilegesRequired=lowest`、`MinVersion=10.0.14393`、`x64compatible`。旧`10.0`ではWindows 10 1507/1511も通るため、公式.NET 10一覧で最古のWindows client 1607に対応する数値下限へ揃えた。これは**必要条件だけ**であり、1607 Home/Proを含む全Windows 10やServerをサポートする宣言ではない。
- `x64compatible`はx64 WindowsとWindows 11 Arm64上のx64エミュレーションを許可する。Innoと.NETの公式資料でこの実行方式を確認できるため維持し、製品で未検証という理由だけで既存の技術的許可を狭めない。ARM64ネイティブ版や実機動作の確認済みという意味ではない。[Innoのarchitecture定義](https://jrsoftware.org/ishelp/topic_archidentifiers.htm)。
- OSは同梱.NETの公式対応とOS自体のライフサイクルも満たす必要がある。[MicrosoftのWindows対応一覧](https://learn.microsoft.com/en-us/dotnet/core/install/windows)と[.NET 10の対応OSデータ](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.json)をリリース候補ごとに再確認する。数値下限だけではSKU・ESU契約・終了日を識別できず、Setup通過を製品サポート判定の代わりにしない。
- macOS/Linux、32bit Windows向け配布、ARM64ネイティブ版、ブラウザーだけでのManager実行は対象外。通常のChrome以外のブラウザーは現在の正式配布対象ではない。要件ファイルを変更したものではない。

## OS・architectureの判定表

「候補」は正式版で検証すべき対象であり、製品の全機能を受け入れ済みという意味ではない。公式.NET資料・OS lifecycle・製品実行証拠の3つを分けて判定する。

| 環境 | 2026-11-05時点の判定 | Setupとの関係 / 次に必要な確認 |
|---|---|---|
| Windows 11 / x64 / OS・.NETの双方が維持サポート中 | 正式版の主な検証候補 | 数値条件は通過。エディション/機能更新版ごとのlifecycleを確認し、下記シナリオを実施 |
| Windows 11 Home 26H2 / x64 | 所有者PCの実行実績あり、公式.NET一覧との照合は保留 | 公式.NETデータは2026-09-28更新で26H2未記載。OS側の現行版であることだけから.NET公式対象と断定しない。候補時に資料/追跡情報を再確認 |
| Windows 10 Enterprise LTSC 2019 (1809) / LTSC 2021 (21H2) / x64 | .NET一覧にある検証候補、製品GUIは未検証 | 17763/19044は数値下限を満たす。固有lifecycleと候補時点の更新状態も要確認 |
| Windows 10 1607 Enterprise LTSB / x64 | 通常lifecycleは2026年10月終了。条件付き調査のみ | 14393は下限を満たすが、通常サポート中とは扱わない。ESU等の条件と.NET側の扱いを別途確認。Home/Proの対応を意味しない |
| Windows 10 1507/1511、32bit Windows | この配布の対象外 | 1507/1511は数値下限、32bit OSはarchitecture条件で拒否。旧OS上での実拒否試験は未実施 |
| Windows 10のその他エディション、OSサポート終了済みのWindows 11 | 正式版のサポート確定対象に含めない | Setupが通る場合がある。Home/Pro 24H2は2026年10月終了、Enterprise/Education 23H2も2026年11月終了予定のため候補時再確認 |
| Windows 11 Arm64 / x64エミュレーション | 技術的に許可、製品未検証 | `x64compatible`を通過。ネイティブARM64配布はない。HotKey/COM/Chromeを実機検証するまで正式対応としない |
| Windows Serverデスクトップ / x64 | hosted CI実行環境、一般利用者向けGUIは未検証 | 現在の数値条件はServer 2016以降も許可。Server Core/Nanoには通常のGUI/Explorer実行証拠がなく製品対象へ広げない |

エディション別の終了状況は[Windows 11 Home/Pro](https://learn.microsoft.com/en-us/lifecycle/products/windows-11-home-and-pro)、[Enterprise/Education](https://learn.microsoft.com/en-us/lifecycle/products/windows-11-enterprise-and-education)、[LTSC 2019](https://learn.microsoft.com/en-us/lifecycle/products/windows-10-enterprise-ltsc-2019)、[LTSC 2021](https://learn.microsoft.com/en-us/lifecycle/products/windows-10-enterprise-ltsc-2021)、[2016 LTSB](https://learn.microsoft.com/en-us/lifecycle/products/windows-10-2016-ltsb)で確認する。固定した製品内のエディション許可リストや日付による自動削除・設定変更は追加していない。

## 代表条件と現在の証拠

| 環境・条件 | 確認する内容 | 現在の状態・証拠 |
|---|---|---|
| Windows Server 2025 Datacenter 10.0.26100 / x64 / Release | build、トランザクション、COM、CLI、配布構造、Setup更新 | [CI 41893214665](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41893214665): Core39/Windows208・skip0、配布/旧Setup更新保持passed。image windows-2025-vs2026 / 20261030.294.1。Windows clientの実GUI全条件の代わりではない |
| 所有者PC / Windows 11 Home 26H2 10.0.26300.9818 / x64 | 関連テストと製品分離、OS情報の読取り | 2026-11-05のOS/architecture/非昇格PowerShell tokenを読取り確認。#157のhelper8件と関連実起動2件passed。配列・倍率・物理HotKeyはこの記録から推定しない |
| 所有者PC / v0.4.0公式Setup更新 | 製品更新、保存設定/管理リンクの保持 | [2026-11-03更新記録](../releases/post-release/v0.4.0.md): 本体413ファイル一致、データ28/管理23保持、読取り監査差分0。拡張ファイル22件一致。Chrome再読み込みと物理キーはpending/not-tested。今回のtoken読取りを過去のSetup実行tokenの証拠に転用しない |
| 日本語・空白・引用符・末尾バックスラッシュ・空引数 | 引数と実際の作業フォルダ | WinExeプローブの実レポートpassed。元lnk byte/HotKey不変も確認 |
| localhost SMB / UNC日本語・空白 / hosted runner | 共有からの実起動・作業フォルダ | CIのUNCプローブpassed、skip0。任意のサーバー、Kerberos、VPN、オフライン切断の全条件とは別 |
| 独自URI / hosted runner | nonce登録先へURIだけが届く | CIのURIプローブpassed、skip0。Windows Storeアプリすべてを起動した証拠ではない |
| 維持サポート中のWindows 11 client / x64 | Setup→初回→編集→反映→実キー→復元→更新 | v1.0.0候補の一連操作は未完了。正式対応ビルドと実行記録を確定する |
| .NET公式対象のWindows 10 LTSC/Enterprise / x64 | 同上、Setupの下限/エディション整合 | 未検証。Setupが許可することだけでサポート済みにしない |
| 標準ユーザー / 非管理者トークン | Setup・通常反映・バックアップ・復元、共有Start Menuの読取り拒否 | 設計/隔離拒否テストあり。非管理者tokenでのv1候補の一連GUI操作は未検証 |
| JIS / USキーボード | 文字キー選択、実Ctrl+Alt、IME有無、Tab/Enter/Escape/F1/F5 | 文字キー選択は実装済み。両配列・IMEでの主要操作と実キーの代表マトリクスは未完了 (#20) |
| 100/125/150/200% DPI | 追加・編集・設定・監査・復元の全文/ボタン到達 | 非表示の通常/小画面描画はあり。実DPI切替による確認は未検証 (#19) |
| 小画面 / 480x320–520x400相当のダイアログ | 全文・確認・取消へのスクロール到達 | 関連画面と主画面800x600の非表示描画passed。主画面も最小480x360とし、非表示HWNDの800x600変更を確認。実画面全条件は未完了 |
| 複数モニター / 混在DPI / 負の座標 | Ownerと同じmonitorへの表示、移動後も範囲内、復帰 | Owner/現在monitor/DPI別制限とPerMonitor manifestを実装。計算/実EXE/非表示HWNDの検査passed。実際の混在DPI移動・切断/再接続は未検証 (#19) |
| Narrator / 高コントラスト | 名前・キー・状態・処理完了・エラー、色以外の識別 | 行名/入力ラベル/実結果LiveRegionとPeer検査を追加。実Narratorと高コントラスト全体の配色は未完了。静的描画を実機passとはしない (#21) |
| 外部LINE/Slack/ChatGPT/Outlookと特殊lnk | 元リンクの昇格、Store関連付け等 | 所有者環境の対象アプリ/既存記録と区別し、v1候補の不足を#32で追跡 |
| ARM64上のx64エミュレーション / Serverデスクトップ | Installerが許可する範囲の扱い | 製品の正式対応として未検証。CI runnerの存在や.NET対応だけから一般配布対象へ拡大しない |

## 実機で埋める代表組合せ

| ID | OS / token | 入力 / IME | 倍率・画面 | 状態 |
|---|---|---|---|---|
| C1 | 維持サポート中のWindows 11 x64 / 標準ユーザー | JIS / 日本語IME on・off | 100% / 単一monitor | 未実施。現在のPCのOS情報だけでは完了にしない |
| C2 | 同じ候補 / 標準ユーザー | US / IME off・日本語IME on | 125% / 単一monitor | 未実施。仮想キー注入を物理配列の検証としない |
| C3 | 同じ候補 / 標準ユーザー | JIS / IME on・off | 150%→200% / 小画面 | 未実施。OS設定を無断変更しない |
| C4 | 同じ候補 / 標準ユーザー | JISまたはUS | 100%と200% / 混在DPI・負座標・接続解除 | 未実施。計算/非表示HWNDの成功とは分ける |
| C5 | 検証するWindows 10 LTSC x64 / 標準ユーザー | JISまたはUS / IME on・off | 100%と150% | 環境未用意。正式対応範囲へ含める前のゲート |
| C6 | 同じWindows 11候補 / 標準ユーザー | キーボードのみ / Narrator | 標準配色とOSコントラストテーマ | 未実施。Peer/注入paletteテストの成功とは分ける |

各行で次節の一連操作を実行し、未検証・環境未用意・実行失敗を区別する。管理者tokenで成功しても標準ユーザー行を埋めない。OS/倍率/配列を変更する実機試験は所有者の利用状況と調整してから行う。

## 実行記録の方法と残るゲート

候補commit・製品/拡張の実行version・OS数値版/エディション・プロセスarchitecture・通常/管理者token・入力配列/IME・倍率/画面構成・操作手順・実結果・rollback/残存物を記録する。個人のアプリ一覧や設定原本を公開ログへ添付しない。失敗は再現条件と保持された資料、未実施は必要な環境を明記する。

代表シナリオはSetup→空状態→Web/アプリ/BAT/Chrome登録→対象テスト→差分/反映→Manager終了後の実キー→バックアップ/復元→旧版から更新→uninstall時保持。GUIと実キーはAPI/描画の成功から推定しない。日常製品の更新にはRelease Setupを使用し、バックアップと戻し方を確認する。

正式版の配布対象の確定、上記代表GUI/入力/DPI/非管理者の実行、Setup制約との整合が未完了のため#54はopenのままとする。環境の準備がない条件を失敗と数えたり、逆にpassedへ置換したりしない。
