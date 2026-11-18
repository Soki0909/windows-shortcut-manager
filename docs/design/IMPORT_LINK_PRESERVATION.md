# 既存lnkの起動条件保持（#32）

## 判断基準

インポートの目的は元リンクと同等に起動することであり、読めた実行先だけを設定へ複製することではない。単純exe化して同等と言えるのは、実行先、引数、作業フォルダ、表示方法、昇格条件、AppUserModelID、advertisedアプリ情報、IDList/追加データを含む起動契約がすべて同等だと確認できる場合だけである。

現在の読取りモデルはその全体を表現・検証しない。したがって `.exe` の実行先が読めても、自動インポートでは **すべて元lnk参照** を選ぶ。利用者が実行ファイル方式を手動で選ぶ機能とは分ける。元リンクの移動・削除で参照は切れるため、元ファイルの保持が必要である。元HotKeyの解除は#31の別操作であり、インポートでは行わない。

## 実測で発見した不足

従来の `explorer.exe "source.lnk"` は原本を保持するが、起動契約の同等性までは保証していなかった。隔離プローブを元lnkから直接Shell起動すると、STARTUPINFOの表示方法は保存値1/7になる。一方、Explorerを経由した起動では10（SW_SHOWDEFAULT）となり、同じ結果ではなかった。

`SetPath(source.lnk)` と `IPersistFile.Save` は、それぞれ元リンクを参照先のexeへ平坦化することがあった。新しい管理リンクは `SHParseDisplayName` で元ファイルのPIDLを取り、`SetIDList` と `SLDF_ALLOW_LINK_TO_LINK` を設定し、`IPersistStream` で保存する。Windows Shellが元リンクを開く。アプリからのテスト実行でも元リンクを直接Shellへ渡す。

反映時には、元リンクの連鎖と管理フォルダをファイルハンドルで正規化する。別名から管理フォルダへ入る参照、循環、安全に確認できない相対参照、深すぎる連鎖を拒否する。SMBでは親フォルダ名を照会できる権限が必要で、照会できなければ反映を停止する。反映後の読戻しでも参照鎖を再確認し、不正な参照があれば管理データをロールバックする。元リンクは他のアプリでも編集できるため、反映完了後も同じ起動条件を保つには原本を維持する必要がある。

反映前後と起動時監査では、保存した参照先パスと参照フラグを確認する。起動時監査では元リンクの参照鎖も確認し、反映後に連鎖が管理フォルダ内へ変わった場合は差分に理由を記録する。従来のExplorerを経由する管理リンクは監査で差分になり、バックアップ付きの既存反映手順で更新できる。設定ファイルの形式は共通である。

実装・実行結果と制約は [#32検証記録](../verification/ISSUE_32.md) に残す。実画面の最小化や実Storeアプリの起動は、プローブの起動情報・保存属性を確認した証拠と区別する。

## Windowsの一次資料

- [Shell link data flags](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ne-shlobj_core-shell_link_data_flags): リンク間参照は循環を作らない条件で許可する。
- [SHParseDisplayName](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname): Unicode名からPIDLを取得する。
- [GetFinalPathNameByHandleW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew) はシンボリックリンクの参照先を解決する。SMBの正規化には各パス要素を照会できる権限が必要になる。
- [ILFree](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-ilfree): 現行WindowsのShell PIDLにはCOM task allocatorの解放を使う。
- [AppUserModelIDの公式サンプル](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/DesktopToasts/CS/ShellPropertyKeys.cs): 試験用リンクのPropertyKey。
- [STARTUPINFOW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfow): プローブが読み取る起動時の表示情報。
