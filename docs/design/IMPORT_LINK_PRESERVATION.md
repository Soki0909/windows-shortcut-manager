# 既存lnkの起動条件保持（#32）

## 判断基準

インポートの目的は元リンクと同等に起動することであり、読めた実行先だけを設定へ複製することではない。単純exe化して同等と言えるのは、実行先、引数、作業フォルダ、表示方法、昇格条件、AppUserModelID、advertisedアプリ情報、IDList/追加データを含む起動契約がすべて同等だと確認できる場合だけである。

現在の読取りモデルはその全体を表現・検証しない。したがって `.exe` の実行先が読めても、自動インポートでは **すべて元lnk参照** を選ぶ。利用者が実行ファイル方式を手動で選ぶ機能とは分ける。元リンクの移動・削除で参照は切れるため、元ファイルの保持が必要である。元HotKeyの解除は#31の別操作であり、インポートでは行わない。

## 実測で発見した不足

従来の `explorer.exe "source.lnk"` は原本を保持するが、起動契約の同等性までは保証していなかった。隔離プローブを元lnkから直接Shell起動すると、STARTUPINFOの表示方法は保存値1/7になる。一方、Explorerを経由した起動では10（SW_SHOWDEFAULT）となり、同じ結果ではなかった。

`SetPath(source.lnk)` と `IPersistFile.Save` は、それぞれ元リンクを参照先のexeへ平坦化することがあった。新しい管理リンクは `SHParseDisplayName` で元ファイルのPIDLを取り、`SetIDList` と `SLDF_ALLOW_LINK_TO_LINK` を設定し、`IPersistStream` で保存する。Windows Shellが元リンクを開く。アプリからのテスト実行でも元リンクを直接Shellへ渡す。

反映時には元リンクの連鎖を読み取り、循環、管理フォルダ内への参照、安全に確認できない相対参照や深すぎる連鎖を拒否する。反映前後と起動時監査では、参照先パスと参照フラグを確認する。従来のExplorerを経由する管理リンクは監査で差分になり、バックアップ付きの既存反映手順で更新できる。設定ファイルの形式は共通である。

実装・実行結果と制約は [#32検証記録](../verification/ISSUE_32.md) に残す。実画面の最小化や実Storeアプリの起動は、プローブの起動情報・保存属性を確認した証拠と区別する。

## Windowsの一次資料

- [Shell link data flags](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ne-shlobj_core-shell_link_data_flags): リンク間参照は循環を作らない条件で許可する。
- [SHParseDisplayName](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname): Unicode名からPIDLを取得する。
- [ILFree](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-ilfree): 現行WindowsのShell PIDLにはCOM task allocatorの解放を使う。
- [AppUserModelIDの公式サンプル](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/DesktopToasts/CS/ShellPropertyKeys.cs): 試験用リンクのPropertyKey。
- [STARTUPINFOW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfow): プローブが読み取る起動時の表示情報。
