# 小画面・DPI・キーボード・読み上げの改善 / #19/#20/#21

## ウィンドウの配置

主画面にも共通のサイズ制限を適用する。所有者がある初回表示はOwner側、以後は自分のHWNDが属するモニターのwork areaを参照し、DPIを使ってWPFのサイズ制限へ変換する。起動・DPI変更・表示構成変更・移動/リサイズ終了後に、ウィンドウ全体が作業領域内へ入るよう調整する。位置の計算は物理pixelで行い、負の座標を切り捨てない。毎回のドラッグ位置は強制しないため、モニター境界を越えて移動できる。

主画面の中身は既存の水平/垂直スクロールを使い、小さい作業領域でも操作へ到達する。ウィンドウの最小値も480x360へ下げ、広い画面上でも利用者が小さくできる。もともと範囲内の位置/サイズは保持する。全体の位置変更は非アクティブ化フラグ付きで、他のアプリを前面へ呼び出さない。Window破棄時に自分のメッセージhookを外す。これは自分のウィンドウの表示通知であり、グローバル入力hookではない。

AppのmanifestはPerMonitorV2/PerMonitorとasInvoker・uiAccess=falseを宣言する。実EXEに埋め込まれたresourceを検証する。[WindowsのDPI宣言](https://learn.microsoft.com/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process)と[モニター作業領域](https://learn.microsoft.com/en-us/windows/desktop/gdi/multiple-monitor-system-metrics)に基づく。APIや計算の検証を、混在DPIの実機操作の代わりにはしない。

## キーボードと読み上げ

主操作へAlt+A/E/D/T/S、確認と復旧へAlt+R、設定/使い方/情報へAlt+O/H/Vを割り当てる。既存ButtonテンプレートのAccessText処理を使用する。フォーカス枠はWindowsのHighlight色へ追従する。一覧は全行選択とTabNavigation=Onceにし、上下矢印で選択後、Tabで次の操作へ進める。画面内のアクセスキーであり、登録HotKeyや常駐方式は変わらない。

一覧行の読み上げ名にはキー・割当・種類・状態等の全文を設定する。設定の入力欄は対応ラベルと結び付け、Chrome参照ボタンの目的も明示する。処理結果のLiveRegionは実際のメッセージを名前に持ち、UI dispatcher上でLiveRegionChangedを通知する。連続変更はまとめ、Window終了時は購読を解除する。[WPFアクセスキー](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.accesskeymanager.iskeyregistered)、[AutomationEvents](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationevents)を参照。

## 証拠と未実施条件

配置の純粋計算では、既存位置維持、小画面、負の座標、100/125/150/200%相当のDPI換算、モニター切断、極小/不正入力を確認する。非表示WPFでは架空の26件から通常/小画面/最下部を描画し、行名・アクセスキー・処理結果名・入力ラベルと、非表示HWNDのサイズ制限を確認する。画像を作る際はDispatcherのbinding/layout処理を待つ。

実際のAlt/Tab操作、JIS/US、Narrator、高コントラスト全画面、物理モニターの混在DPI・切断/再接続は未実施。この変更だけでは#19/#20/#21/#54をcloseしない。特に高コントラスト全体の配色追従は残る実装課題。残条件は環境マトリクスと各Issueで追跡する。
