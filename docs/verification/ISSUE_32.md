# #32 既存lnkの代表起動条件の検証

2026-11-05。作業中。対応PRの最終結果を追記するまで、受け入れ完了の証拠として使わない。

## 発見

- 現行importerは既に全lnkを元パス参照にしていた。通常/最小化の読戻しはあるが、元リンクを直接Shell起動した場合との同等性は未確認だった。
- STARTUPINFOの保存値1/7を起動先で確認する試験を追加し、従来のExplorer参照経由が10になることを再現。原本の保存属性と起動先の受信値は別物として扱う。直接Shell起動との比較でも差分を確認したため、期待値を弱めず呼出し経路を修正する。
- AllowLinkToLinkだけを設定してSetPathで元lnkを指定しても、読戻し先はexeになった。SetIDListで元ファイルを参照する方式を検証する。

## 検証範囲

隔離した一時フォルダと製品非同梱のLaunchProbeだけを使う。元HotKeyはリンク属性として保存・読戻しするが、キーを登録/押下しない。原本bytes・mtime・引数・作業フォルダ・表示属性・AppUserModelIDを確認する。個人の設定や管理リンク、インストール済み製品、Storeアプリは操作しない。

実画面の最小化や、実Storeアプリ固有の有効化/認証を検証したとは主張しない。

## 2026-11-05 中断時点の保全

所有者の中断依頼によりWIPとして保全。#32は未完了、main未統合、PR未作成。main側の[再開メモ](https://github.com/Soki0909/windows-shortcut-manager/blob/main/docs/implementation/PAUSE_2026-11-05.md)を参照する。

- 担当は、IPersistFile.Saveでは参照が平坦化され、IPersistStream保存でAllowLinkToLinkと元PIDLが維持されることを確認した。通常/最小化/AppUserModelID付き3件で元lnk直接起動・生成wrapper・import起動の引数/作業フォルダ/STARTUPINFO一致、元bytes/mtime/HotKey/識別情報不変を報告した。
- 実Applyのbackup/stage/readback、生成lnkからの起動と監査を含む必要回帰25件passed/runner専用2件skipを担当が報告。その後に必須flagの読戻し/比較を追加した。
- 最後のhandle97062は31 passed/1 failedで終了。唯一の失敗は、rawでAllowLinkToLinkを除いたfixtureをWindowsがLoad拒否するケースに対して、比較差分だけを期待した試験。native拒否と比較の境界を整理する修正と、再実行が残る。失敗を完了扱いにしない。
- 循環・管理フォルダ参照のguardもWIP。COM/IPersistStreamの資源解放、参照属性の監査、旧リンク/通常exe/backup互換性、原本保護をmainレビューで再確認する。
- 担当は利用上限で終了。中断確認時にdotnet/testhost/LaunchProbeの実行プロセスなし。最終差分の検証・hosted CIを終えるまで採用しない。
