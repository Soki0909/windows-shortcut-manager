# Chrome実機検証

`scripts/test-chrome-live.mjs`は新しい専用プロファイルとコピーした拡張だけを使う。Playwrightは既存テストランタイムを指定でき、製品へのnpm依存は追加しない。

## 準備

1. 公式Chrome for TestingとPlaywrightを用意する。個人用プロファイルは指定しない。
2. `scripts/run-debug.ps1 -NoLaunch`と`dotnet build tests/ShortcutManager.LiveDriver -c Debug`を実行する。
3. `TEST_CHROME_PATH`にCfTのchrome.exe、必要なら`PLAYWRIGHT_MODULE_PATH`に既存playwrightパッケージのパスを指定する。
4. 空または未作成のDebug設定だけを使う。登録済み／設定済みDebugデータは拒否する。製品設定をコピーしない。

## 実行

`node scripts/test-chrome-live.mjs`。

- `TEST_SKIP_HOTKEY=1`: 実Chrome・生成lnkの自動検証のみ。実HotKey完了の証拠にはしない。
- `TEST_MANUAL_HOTKEY=1`: 各READY表示で所有者が実キーを押す。Chrome前面の段階は専用ページ内の緑ボタンを押してから入力する。
- `TEST_DESKTOP_HOTKEY=1`: OSが新規Start-menu登録を読み込まない場合の隔離検証用。生成lnkの同一コピーを一意名でDesktopへ作り、終了時にバイト一致を確認して除去する。既存ファイルを上書きしない。
- `TEST_ALLOW_EXPLORER_RESTART=1`: **実行の都度、所有者の事前同意が必要**。テストリンク作成後と削除後に既存のExplorer再読み込み処理を使う。既定では再起動しない。

新仕様は前面アプリに関係なくcollapsedを反転する。実キーでは非Chrome×展開、Chrome×展開、Chrome×折りたたみ、非Chrome×折りたたみの4条件を確認する。Managerは閉じ、独立したテスト用WinForms画面を非Chrome前面に使う。

ServiceWorker.stopWorker、同名groupの複数窓、別group選択、最後のmember復帰、並列要求、旧focus-groupとPopup、Chrome冷間起動、Dispatcher残留も確認する。Cold-startの応答観測だけをコピー先dispatch.jsへ加え、製品sourceは変更しない。CDPのloopback接続はこのテストプロセスだけが使い、製品にサーバーやポートを追加しない。

結果はgitignored `local/chrome-live-<timestamp>/evidence.json`とverified.png。scanやパスを含む生の証拠は公開せず、検証記録には条件・結果・コマンドだけを転記する。

終了時は自分が作ったDesktopコピーとDebugのテストHotKeyを除去し、以前の空Debug設定を復元する。外部HotKeyのスキャン結果が同じことを確認する。失敗時はcleanup.jsonとプロセス状態を確認してから再開する。
