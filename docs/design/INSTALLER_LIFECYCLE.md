# インストール・更新・削除の確認

## 配置と保持

公式Setupは現在ユーザー用に導入し、同じAppIdで更新する。更新/再導入/アンインストールは設定・state・バックアップ・管理HotKeyとChrome側データを変更しない。アンインストール画面にも保持することを説明し、既定の選択はキャンセルとする。silent uninstallは従来どおり保持して進む。

HotKeyを止めたい場合は、アンインストール前にManagerで対象の割当を無効化して「変更を反映」する。通常のバックアップ・管理フォルダ確認・差分確認が適用される。Managerの削除だけでHotKeyが解除されたとは表示しない。

旧payload整理は[ADR-015](../architecture/ADR-015-installer-payload-retirement.md)に従う。receiptは`.shortcut-manager-payload.ini`。一致する不要ファイルだけを`.shortcut-manager-retired-*`へ旧receiptとともに退避する。改変/不明ファイルは残り、退避もuninstallで消えない。receiptのないv0.3.1等からの更新では古いファイルの所有を推測しない。

## 任意の完全削除の設計 / #52の残り

アンインストールとは別に、所有者が明示選択する削除準備を設計する。既定はすべて保持し、今のSetupはChrome storageやユーザーデータを自動削除しない。

公式アンインストール、管理HotKeyの解除、確認済みのローカルデータの削除を、それぞれ別の操作にする。任意の解除とデータの退避、取り消し、確定消去は、公式アンインストール前にManagerで行う設計である。手順と検証計画は [管理ショートカットの解除とローカルデータの削除](OPTIONAL_REMOVAL.md) を参照する。

削除準備の画面と処理は未実装。#52の受け入れに向けて、実装後に隔離環境で解除、データ削除、取り消しと再導入後の復元を確認する。

## 自動検証 / #56/#91

- `test-installer-payload.ps1`: Unicode、実体hash、相対パス、予約名、空payload、reparseと祖先、拒否時の原本保全。
- `test-installer-maintenance.ps1`: Setupのnative処理を12 fixtureで実行。全変更はRUNNER_TEMPの新規領域だけで、ハーネスは実インストール開始前に終了する。
- `test-installer-lifecycle.ps1`: 公開v0.3.1 Setupの固定SHAからinstallし、旧CLIのApplyで作った設定/管理lnkと管理外lnkを保持してupgrade、同版再導入、uninstall、再install。実構成が異なる旧fixture Setup→新Setupでは既知不要物の退避、改変/ユーザー追加物の保持も検査する。
- 新版の版・source commit・clean状態、HKCUの登録、audit-only launcherの実行先/引数/HotKeyなし、CLI validation、保持対象の全byte hashを判定する。

後二つは使い捨てGitHub Windows runner以外では、実行前に停止する。個人PCで環境変数を偽装しない。保存されたHotKey属性を検査するが、Explorerの実際の登録表や物理キーによる起動を検査したとはしない。最終実機確認と対応OS/DPI/配列の検証は別途継続する。
