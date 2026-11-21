# Issue #87 検証記録: 複数Chromeプロファイルと拡張ID取り違えの診断

検証日: 2026-11-21（JST）

## 設計判断

- Managerは非常駐・ローカル中心の設計を維持し、Chrome内部プロファイル設定ファイルの非公式な読み取りや個人プロファイル名のハードコードを行わない。
- Chromeの公開APIでは外部アプリ（Manager）から「どのプロファイルにどの拡張IDが入っているか」「どのプロファイルが前面か」を直接照会できない。この制約を `ChromeSetupGuide.cs` および `src/ChromeExtension/help.html` に明示する。
- 代わりに、複数プロファイル時の実挙動と、IDが正しいのに動かない場合の段階的な切り分け手順（拡張メニュー単体確認 → 対象プロファイルを前面にして「対象をテスト」 → 拡張メニューの「直近のキー起動エラー」有無でプロファイル到達を確認）を案内する。

## 隔離Chromeでの実測結果

- 実行環境: 隔離ユーザーデータディレクトリ上の Chrome for Testing (`Chrome/149.0.7827.55`)、拡張 `src/ChromeExtension` (`v0.4.0`)
- 確認した手順と結果:
  1. Profile A (`Default`) のみを開き、拡張をロードして折りたたみ済みグループ `Profile A fixture` を作成。プロファイル指定なしで `chrome.exe chrome-extension://<id>/dispatch.html?...` を起動したところ、Profile A のグループが展開され、Dispatcherタブが自動終了した (`firstOpenExpandedA: true`)。
  2. Profile B (`Profile 1`、拡張未導入) を `--profile-directory="Profile 1"` で前面起動した状態で、同じプロファイル指定なしURIを起動したところ、Chromeは最後にアクティブだったProfile BへURIを渡し、Profile Aのグループは折りたたみのまま変化しなかった (`literalUriLeftAUnchanged: true`)。
  3. Profile Aのウィンドウを前面に戻してから同じURIを起動したところ、Profile Aのグループが再び展開され、新しいDispatcherタブが自動終了した (`switchBackExpandedA: true`)。
  4. Profile Aで拡張が有効な状態で未登録グループ名のURIを起動したところ、Dispatcherが `group-not-found` を返した (`missingGroupDiagnostic: "group-not-found"`)。

## 変更ファイル

- `src/ShortcutManager.App/Views/ChromeSetupGuide.cs`
- `src/ChromeExtension/help.html`
- `docs/verification/ISSUE_87.md`
