# Windows Shortcut → Chrome Extension Dispatcher PoC v1.1.1

## 目的

`Ctrl+Alt+R` → Windows `.lnk` → `chrome.exe` → `chrome-extension://<ID>/dispatch.html?action=research` → Chrome Extension → 既存の「研究」タブグループ

という経路だけを検証する最小PoC。

## v1.0.0からの変更

- `commands` を削除。Chrome側はキーバインドを知らない。
- `dispatch.html` / `dispatch.js` を追加。
- Dispatcherは `action=research` / `action=seven` をグループ名へ変換し、service workerへメッセージ送信。
- 成功時はDispatcherタブを `chrome.tabs.remove()` で自動削除。
- PopupにExtension IDを表示。

## 実機テスト手順

1. `chrome://extensions` で「デベロッパー モード」をON。
2. 「パッケージ化されていない拡張機能を読み込む」から `chrome-tab-group-launcher-v1.1.1` を読み込む。
3. Chrome上に名前が完全一致するタブグループ `研究` を作り、折りたたむ。
4. 拡張Popupを開き、表示される `Extension ID` を確認する。
5. まずDispatcher単体を確認:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\windows\open-dispatcher-direct.ps1 -ExtensionId <32文字のID>
   ```

   `研究` が展開・フォーカスされ、Dispatcherタブが消えれば第1段階成功。

6. Windows `.lnk` + HotKeyを作成:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\windows\install-research-poc.ps1 -ExtensionId <32文字のID>
   ```

7. Chromeで `研究` を折りたたむ。
8. VS Codeやメモ帳などChrome以外のアプリを前面にする。
9. `Ctrl+Alt+R` を押す。

## 成功条件

- Chrome以外のアプリから `Ctrl+Alt+R` を押せる。
- Chromeが前面に来る。
- `研究` グループが展開される。
- そのグループ内の最後に使ったタブがアクティブになる。
- 一時的なDispatcherタブが残らない。

## 失敗時に見る場所

1. `.lnk` 自体が起動しない
   - Start Menu配下に `Windows Shortcut Manager PoC/研究 - Chrome Tab Group.lnk` があるか。
   - `.lnk` のプロパティに `Ctrl+Alt+R` が入っているか。
   - 他のショートカットとHotKeyが競合していないか。

2. Chromeは開くが拡張ページが開かない
   - Extension IDが一致しているか。
   - 拡張が有効か。
   - `open-dispatcher-direct.ps1` でも再現するか。

3. Dispatcherは開くが研究へ移動しない
   - Popupで `研究` が一覧に見えるか。
   - グループ名が完全一致しているか。
   - `chrome://extensions` → 拡張 → Service worker のコンソールを確認。

## 削除

```powershell
powershell -ExecutionPolicy Bypass -File .\windows\remove-research-poc.ps1
```

## 今回まだやらないこと

- `7脱` 用 `.lnk` の本反映
- A〜Z全キーの一括反映
- Shortcut Manager UI
- Native Messaging / localhost / Named Pipe

まずこの経路がWindows実機で成立するかだけを確認する。


## v1.1.1 fix

Windows PowerShell 5.1 compatibility: all `.ps1` files are now ASCII-only to avoid UTF-8-without-BOM parsing issues. The PoC shortcut filename is `Research - Chrome Tab Group.lnk`.

## v1.1.2 fix
Windows PowerShell can unwrap a one-item pipeline result to a scalar string. The previous scripts then used `$candidates[0]`, which indexed the first character (`C`) instead of selecting the Chrome path. v1.1.2 selects the first Chrome path with `Select-Object -First 1` into `$chrome` and passes that string directly.
