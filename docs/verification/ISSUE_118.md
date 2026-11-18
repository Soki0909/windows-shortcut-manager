# Chromeの実起動イベントと復元グループの折りたたみ / #118

## 再現手順

Windows、Node.js 22、Chrome for Testing、Playwrightを用意する。既存のPlaywrightを使う場合は、`PLAYWRIGHT_MODULE_PATH`をそのモジュールの絶対パスに設定する。

```powershell
$env:TEST_CHROME_PATH = '<Chrome for Testing の chrome.exe の絶対パス>'
$env:PLAYWRIGHT_MODULE_PATH = '<Playwright モジュールの絶対パス>'
node scripts/test-chrome-startup-live.mjs
```

スクリプトは`local/chrome-startup-*`に毎回新しい専用プロファイルと拡張の試験用コピーを作る。専用プロファイルの開発者モードと前回セッションの復元を有効にし、初回セッションでだけChrome DevTools Protocolの`Extensions.loadUnpacked`を使う。その後はブラウザープロセスの終了を確認してから同じプロファイルを起動する。各回の結果は同じディレクトリの`result.json`に記録する。[Chrome DevTools ProtocolのExtensions API](https://chromedevtools.github.io/devtools-protocol/tot/Extensions/)

## 検証範囲

- 製品のPopupから起動時折りたたみを有効にする。Chromeの実`runtime.onStartup`を試験用リスナーで観測し、製品のハンドラーが新しい有効期限を書いたことを確認する。起動直後の観測ページはWorkerにメッセージを送信しない。[Chromeの拡張Service Workerライフサイクル](https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle)
- 異なるウィンドウに保存した二つのグループがメンバータブとともに復元され、初回の再起動で折りたたまれることを確認する。
- 有効期限内に新しいグループを作成し、同時にDispatcherの`toggle-group`とPopupの「開く」で復元グループを展開する。新規グループだけが折りたたまれ、明示的に開いたグループは開いた状態を保ち、Dispatcherタブが閉じることを確認する。
- ChromeのDevTools ProtocolでWorkerを停止し、新しいグループ作成でWorkerが復帰することを観測する。同じブラウザーセッション内で保護状態が残り、次のブラウザーセッションでは保護状態が消え、二つのグループが再び折りたたまれることを確認する。

この試験では専用ChromeにDispatcher URIを開くところから実行する。物理キーとWindows Shellの配送、Chrome Web Store版での実行は別の受け入れ確認に残る。従来の`test-chrome-live.mjs`による有効期限を明示設定した試験も別途実行できる。CIでは専用Chromeを必要としない構文確認を行う。

## 実行結果（2026-11-18）

- Windows上のChrome for Testing 148.0.7778.97、Node.js 22.15.0、Playwright 1.62.1で3回連続成功。各回とも新しい専用プロファイルで3つのブラウザーセッションを実行し、終了のたびにブラウザーのPIDが消えることを確認した。実起動イベントの受信、2窓2グループのメンバー保持、明示操作、Worker復帰、保護状態のリセットを各回で観測した。
- 開発者モードを設定する前の試作では、Chromeがunpacked拡張を無効化し、試験ページが`ERR_BLOCKED_BY_CLIENT`で開けなかった。また、試験タブの遷移が完了する前にChromeを閉じると、再起動後に一部のメンバーが新規タブとして復元された。最終試験では専用プロファイルに開発者モードを設定し、各メンバーのURLの読み込み完了を待ってからグループを作成した。[Chromiumの拡張無効化理由の定義](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/extensions/browser/disable_reason.h)
- `node --check scripts/test-chrome-startup-live.mjs`、既存の拡張テスト29件、`scripts/test-ci-scope.ps1`の21件が成功。
