# 対応環境と検証マトリクス / #54

更新: 2026-11-03。正式v1.0.0の全条件がpassedした表ではない。実行済み・未検証・配布対象外・条件付きの区別を公開する。

## 配布要件と確定前の条件

- 要件定義の対象はWindows x64。現在の成果物はwin-x64のself-contained WPFアプリ/Setupで、通常起動に.NET SDKの追加導入や管理者昇格を要求しない設計。
- Setupは`PrivilegesRequired=lowest`、`MinVersion=10.0`、`x64compatible`。これは技術的な受け入れ条件であって、全Windows 10・Windows on ARMのエミュレーション・Windows Serverを製品として検証済みという宣言ではない。正式版前に、下記の対応表とSetup下限/architectureを整合させる。
- OSは同梱.NETの公式対応とOS自体のライフサイクルも満たす必要がある。[MicrosoftのWindows対応一覧](https://learn.microsoft.com/en-us/dotnet/core/install/windows)と[.NET 10の対応OSデータ](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.json)をリリース候補ごとに再確認する。Windows 10のすべてのエディション/ビルドを一括でサポート対象とはしない。
- macOS/Linux、32bit Windows向け配布、ARM64ネイティブ版、ブラウザーだけでのManager実行は対象外。通常のChrome以外のブラウザーは現在の正式配布対象ではない。要件ファイルを変更したものではない。

## 代表条件と現在の証拠

| 環境・条件 | 確認する内容 | 現在の状態・証拠 |
|---|---|---|
| Windows hosted runner / x64 / Release | build、トランザクション、COM、CLI、配布構造、Setup更新 | CI 41557876433: Core39/Windows185、全4job success。runner OS/imageは同runのdotnet jobのSet up job記録を参照。Windows clientの実GUI全条件の代わりではない |
| 所有者PC / x64 / Debug | 関連テストと製品分離 | #26/#27: 実起動2passed、URI/SMB2skip。Debug/Release build成功。日常製品の更新証拠ではない |
| 日本語・空白・引用符・末尾バックスラッシュ・空引数 | 引数と実際の作業フォルダ | WinExeプローブの実レポートpassed。元lnk byte/HotKey不変も確認 |
| localhost SMB / UNC日本語・空白 / hosted runner | 共有からの実起動・作業フォルダ | CIのUNCプローブpassed、skip0。任意のサーバー、Kerberos、VPN、オフライン切断の全条件とは別 |
| 独自URI / hosted runner | nonce登録先へURIだけが届く | CIのURIプローブpassed、skip0。Windows Storeアプリすべてを起動した証拠ではない |
| 維持サポート中のWindows 11 client / x64 | Setup→初回→編集→反映→実キー→復元→更新 | v1.0.0候補の一連操作は未完了。正式対応ビルドと実行記録を確定する |
| .NET公式対象のWindows 10 LTSC/Enterprise / x64 | 同上、Setupの下限/エディション整合 | 未検証。Setupが許可することだけでサポート済みにしない |
| 標準ユーザー / 非管理者トークン | Setup・通常反映・バックアップ・復元、共有Start Menuの読取り拒否 | 設計/隔離拒否テストあり。非管理者tokenでのv1候補の一連GUI操作は未検証 |
| JIS / USキーボード | 文字キー選択、実Ctrl+Alt、IME有無、Tab/Enter/Escape/F1/F5 | 文字キー選択は実装済み。両配列・IMEでの主要操作と実キーの代表マトリクスは未完了 (#20) |
| 100/125/150/200% DPI | 追加・編集・設定・監査・復元の全文/ボタン到達 | 非表示の通常/小画面描画はあり。実DPI切替による確認は未検証 (#19) |
| 小画面 / 480x320–520x400相当のダイアログ | 全文・確認・取消へのスクロール到達 | 関連画面の非表示描画passed。メイン画面の全条件・物理画面の作業領域判定は未完了 |
| 複数モニター / 混在DPI / 負の座標 | Ownerと同じmonitorへの表示、移動後も範囲内、復帰 | 未検証。現在の制約が主作業領域基準であることだけでは完了にしない (#19) |
| Narrator / 高コントラスト | 名前・キー・状態・処理完了・エラー、色以外の識別 | 未完了。AutomationPropertiesの一部追加や静的描画を実機passとはしない (#21) |
| 外部LINE/Slack/ChatGPT/Outlookと特殊lnk | 元リンクの昇格、Store関連付け等 | 所有者環境の対象アプリ/既存記録と区別し、v1候補の不足を#32で追跡 |
| ARM64上のx64エミュレーション / Serverデスクトップ | Installerが許可する範囲の扱い | 製品の正式対応として未検証。CI runnerの存在や.NET対応だけから一般配布対象へ拡大しない |

## 実行記録の方法と残るゲート

候補commit・製品/拡張の実行version・OS数値版/エディション・プロセスarchitecture・通常/管理者token・入力配列/IME・倍率/画面構成・操作手順・実結果・rollback/残存物を記録する。個人のアプリ一覧や設定原本を公開ログへ添付しない。失敗は再現条件と保持された資料、未実施は必要な環境を明記する。

代表シナリオはSetup→空状態→Web/アプリ/BAT/Chrome登録→対象テスト→差分/反映→Manager終了後の実キー→バックアップ/復元→旧版から更新→uninstall時保持。GUIと実キーはAPI/描画の成功から推定しない。日常製品の更新にはRelease Setupを使用し、バックアップと戻し方を確認する。

正式版の配布対象の確定、上記代表GUI/入力/DPI/非管理者の実行、Setup制約との整合が未完了のため#54はopenのままとする。環境の準備がない条件を失敗と数えたり、逆にpassedへ置換したりしない。
