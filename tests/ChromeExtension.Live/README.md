# Issue110実機検証fixture

個人Chrome・製品版を変更せず、専用Chrome for Testingで実施する。

1. `src/ChromeExtension/`をgitignored専用フォルダへコピーし、このフォルダのharness.html/jsを追加する。
2. 公式Chrome for Testingを専用`--user-data-dir`、`--load-extension`で起動する。個人プロファイルは使わない。
3. IDをchrome://extensionsで読み、`chrome-extension://<id>/harness.html`を開く。
4. 「テストグループ作成」を1回押す。example.com/example.orgの2タブが対象。「状態を更新」でcollapsed・active・Dispatcher残留を確認する。
5. `scripts/run-debug.ps1`でDebugを構築・起動する。製品版も含むスキャンで未使用と確認したキーのみ使う。他版HotKeyは変更しない。
6. 非Chrome前面×対象展開／折りたたみ、Chrome前面×対象展開／折りたたみを実HotKeyで確認する。連続呼出し後もDispatcherが残らずmemberが2個のままであることを確認する。
7. Worker停止・復帰後に繰り返す。「別ウィンドウに同名グループ作成」で同名・複数窓も確認する。対象なし・Chrome未起動も検証する。
8. テスト用HotKeyのみDebugのApplyで除去する。製品版と個人Chromeには触れない。

fixtureはテスト用コピーにだけ追加し、製品Extension ZIPには含めない。
