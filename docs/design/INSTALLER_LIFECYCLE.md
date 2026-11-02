# インストール・更新・削除の確認

## 配置と保持

公式Setupは現在ユーザー用に導入し、同じAppIdで更新する。更新/再導入/アンインストールは設定・state・バックアップ・管理HotKeyとChrome側データを変更しない。アンインストール画面にも保持することを説明し、既定の選択はキャンセルとする。silent uninstallは従来どおり保持して進む。

HotKeyを止めたい場合は、アンインストール前にManagerで対象の割当を無効化して「変更を反映」する。通常のバックアップ・管理フォルダ確認・差分確認が適用される。Managerの削除だけでHotKeyが解除されたとは表示しない。

旧payload整理は[ADR-015](../architecture/ADR-015-installer-payload-retirement.md)に従う。receiptは`.shortcut-manager-payload.ini`。一致する不要ファイルだけを`.shortcut-manager-retired-*`へ旧receiptとともに退避する。改変/不明ファイルは残り、退避もuninstallで消えない。receiptのないv0.3.1等からの更新では古いファイルの所有を推測しない。

## 任意の完全削除の設計 / #52の残り

アンインストールとは別に、所有者が明示選択する削除準備を設計する。既定はすべて保持し、今のSetupはChrome storageやユーザーデータを自動削除しない。

1. Managerで現在の保存先、管理HotKey件数、削除対象、未保存候補を表示する。ZIPを管理フォルダ/data外へ書き出して検査し、外部BAT・参照元lnk・Chromeタブ/storageはZIPに含まれないと説明する。
2. 「管理HotKeyを解除」を独立選択し、所有marker・管理外競合・バックアップ・反映後検証を含む既存Applyで、定義を保存したまま無効化する。管理外lnkのHotKeyには触らない。必要なShell反映も既存の確認に従う。
3. アプリを終了し、公式uninstallを行う。設定・バックアップを消す場合は対象一覧と最終確認を別に行い、確認後に変更された対象、未知ファイル、reparseは保留する。外部へ退避したZIPは消さない。Chromeの消去はこの削除に混ぜず、拡張Popupの履歴管理/Chromeの拡張削除を本人が選ぶ。

この一括準備/データ削除UIは未実装で#52を開いたまま維持する。保持説明の追加だけで完全削除の受け入れ完了とはしない。現在の手動案内でも、データフォルダの一括再帰削除やmarkerの手書き変更を勧めない。

## 自動検証 / #56/#91

- `test-installer-payload.ps1`: Unicode、実体hash、相対パス、予約名、空payload、reparseと祖先、拒否時の原本保全。
- `test-installer-maintenance.ps1`: Setupのnative処理を12 fixtureで実行。全変更はRUNNER_TEMPの新規領域だけで、ハーネスは実インストール開始前に終了する。
- `test-installer-lifecycle.ps1`: 公開v0.3.1 Setupの固定SHAからinstallし、旧CLIのApplyで作った設定/管理lnkと管理外lnkを保持してupgrade、同版再導入、uninstall、再install。実構成が異なる旧fixture Setup→新Setupでは既知不要物の退避、改変/ユーザー追加物の保持も検査する。
- 新版の版・source commit・clean状態、HKCUの登録、audit-only launcherの実行先/引数/HotKeyなし、CLI validation、保持対象の全byte hashを判定する。

後二つは使い捨てGitHub Windows runner以外では、実行前に停止する。個人PCで環境変数を偽装しない。保存されたHotKey属性を検査するが、Explorerの実際の登録表や物理キーによる起動を検査したとはしない。最終実機確認と対応OS/DPI/配列の検証は別途継続する。
