# 既存lnkの起動条件保持（#32）

## 判断基準

インポートの目的は元リンクと同等に起動することであり、読めた実行先だけを設定へ複製することではない。単純exe化して同等と言えるのは、実行先、引数、作業フォルダ、表示方法、昇格条件、AppUserModelID、advertisedアプリ情報、IDList/追加データを含む起動契約がすべて同等だと確認できる場合だけである。

現在の読取りモデルはその全体を表現・検証しない。したがって `.exe` の実行先が読めても、自動インポートでは **すべて元lnk参照** を選ぶ。利用者が実行ファイル方式を手動で選ぶ機能とは分ける。元リンクの移動・削除で参照は切れるため、元ファイルの保持が必要である。元HotKeyの解除は#31の別操作であり、インポートでは行わない。

## 実測で発見した不足

従来の `explorer.exe "source.lnk"` は原本を保持するが、起動契約の同等性までは保証していなかった。隔離プローブを元lnkから直接Shell起動すると、STARTUPINFOの表示方法は保存値1/7になる。一方、Explorerを経由した起動では10（SW_SHOWDEFAULT）となり、同じ結果ではなかった。

また、単に新lnkの `SetPath(source.lnk)` を呼ぶと、`SLDF_ALLOW_LINK_TO_LINK` を先に設定しても実行先へ平坦化された。元リンクを参照するには、ファイルを指すPIDLを `SHParseDisplayName` で取得して `SetIDList` へ渡し、リンク間参照フラグを明示する方式を検証する。

実装・実行結果と制約は [#32検証記録](../verification/ISSUE_32.md) に残す。実画面の最小化や実Storeアプリの起動は、プローブの起動情報・保存属性を確認した証拠と区別する。

## Windowsの一次資料

- [Shell link data flags](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ne-shlobj_core-shell_link_data_flags): リンク間参照は循環を作らない条件で許可する。
- [SHParseDisplayName](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname): Unicode名からPIDLを取得する。
- [ILFree](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-ilfree): 現行WindowsのShell PIDLにはCOM task allocatorの解放を使う。
- [AppUserModelIDの公式サンプル](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/DesktopToasts/CS/ShellPropertyKeys.cs): 試験用リンクのPropertyKey。
- [STARTUPINFOW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfow): プローブが読み取る起動時の表示情報。
