# unpacked拡張の更新と中断復旧

この手順は既に導入したTab Bridgeの同じフォルダを更新します。Chromeプロファイル、拡張storage、Windowsのキー設定は変更しません。正式な一般配布はChrome Web Storeを予定しています。

1. 現在の版と新しい版の公式TabBridge ZIP、および各リリースの `SHA256SUMS.txt` を取得します。公開リリースのdigestとchecksumが一致することを別途確認してください。任意のZIPに対して自分で計算したハッシュだけでは、公式配布元の確認にはなりません。
2. 現在の拡張の配置先・ID・versionを記録します。更新/復旧中にChromeの再読み込みを押さないでください。
3. PowerShell 7.4以降で実行します（以下は端末固有情報を含まない例）。

```powershell
./scripts/deploy-unpacked-extension.ps1 `
  -Destination '<既存の拡張フォルダ>' `
  -ArchivePath '<新しい公式TabBridge ZIP>' `
  -ArchiveSha256 '<新しいZIPの公開SHA-256>' `
  -PreviousArchivePath '<現在の版の公式TabBridge ZIP>' `
  -PreviousArchiveSha256 '<現在のZIPの公開SHA-256>'
```

正常時は、ディスク上のversion、照合結果と退避先が表示されます。以前の配布ファイルであることをバイト比較で確認できる不要ファイルだけを除き、利用者のメモや未知のフォルダは保持します。既知ファイルが改変されている場合、新版の同名ファイルで無断上書きせず停止します。既存manifestのkeyは維持し、IDを変える新keyは拒否します。

4. ディスクの更新が成功してから、Chromeの既存拡張を再読み込みし、Popupの実行versionとIDを確認します。代表的なグループ呼出しでDispatcherが残らず、元のタブが保持されることも確認します。再読み込みはディスク更新とは別の確認事項です。

## 中断・失敗した場合

Chromeを再読み込みせず、同じ配置先を指定して実行します。

```powershell
./scripts/deploy-unpacked-extension.ps1 -Destination '<同じ既存フォルダ>' -RecoverOnly
```

fingerprintを確認できた完全な旧版または新版へ戻します。復旧を実施した場合はその結果を確認してから、必要なら更新コマンドをもう一度実行してください。未処理がなければ `no-pending` を返します。退避フォルダやjournalsを削除せず、復旧途中に終了しても同じ復旧コマンドを再実行できます。

次の場合は自動復旧を停止します: 中断後の利用者による変更、旧版退避データの変更、未知/破損したmetadata、reparse point、完全な元/新状態の不在。退避証拠を保持し、journalのbefore/afterと各ファイルを比較してから手動復旧します。分からないままmetadataを削除したり、旧新版のファイルを部分上書きしたりしないでください。

初期の準備用intent保存前に中断した場合、元の配置先は変更されていません。metadataがない準備フォルダは自動整理しません。現在フォルダが元の正式版と一致することを確認し、不完全な準備フォルダを退避してから再実行してください。バックアップは自動削除されません。

検証: `./scripts/test-extension-deployment.ps1` は一時フォルダでのみ実行します。個人Chromeの再読み込みやグループ操作はしません。
