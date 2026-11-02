# 公開後の完了記録

タグに含める `docs/implementation/RELEASE_<version>.md` は準備と検証の記録です。公開後の事実はmainへ別コミットで追記し、この索引から参照します。公開済みタグ・バイナリ・checksumを記録修正のために移動/上書きしません。

| 公開版 | 準備・検証 | 公開後の結果 |
| --- | --- | --- |
| v0.3.0 | [準備記録](../implementation/RELEASE_0.3.0.md) | [公開・製品更新完了](post-release/v0.3.0.md) |
| v0.3.1 | [準備記録](../implementation/RELEASE_0.3.1.md) | [公開・製品更新完了](post-release/v0.3.1.md) |

## 毎回の担当と更新タイミング

リリースを実行する担当（所有者の依頼で作業するエージェントを含む）が、公開物の独立検証後と所有者環境の更新確認後に以下を追記し、mainへ統合します。Chrome再読み込みなどを所有者が行った場合、本人の確認と自動検証を区別して記録します。

- source/tagの完全SHA、main CI/ReleaseのURL、公開日時とURL。
- 公開した3ペイロードのSHA-256、公開checksumとasset digestの照合、ZIP/EXE版の確認。過去版を再照合した場合は当初の確認日時と区別する。
- 製品更新前のバックアップ、公式Setup更新、設定と管理リンク不変、インストールファイル照合、起動監査、拡張のID/実行version確認。
- `passed`、`manual-confirmed`、`pending`、`not-tested`、`blocked`を使い分ける。未実施の別PCまで完了と書かない。
- 個人用絶対パス、URL履歴、設定本体、ログ本文、token等は公開記録へ転記しない。必要なら件数/ハッシュ/成功状態へ要約する。
- [検証記録](../verification/VERIFICATION.md)と実装計画から公開後記録へリンクし、準備時点の未完了項目だけが残らないようにする。

ロードマップIssue #1は各マイルストーンの統合・Issueクローズ後に同期します。

```powershell
./scripts/sync-roadmap.ps1          # 読取り照合。古いチェック状態は非0で報告
./scripts/sync-roadmap.ps1 -Update  # 子Issueのopen/closedに合わせチェックだけを更新
./scripts/sync-roadmap.ps1          # 更新後に再照合
```

この操作は子Issueの状態を正本とし、完了理由・将来スコープは変更しません。#1の初期105件という件数は初回調査の履歴であり、後から発見したIssueの現在総数ではありません。追加課題はGitHubのopen一覧とv1.0.0計画で追跡します。

## 公開後チェックリスト

- [ ] 準備チェックと実際の公開/製品更新結果を区別したレポートを作成。
- [ ] 公開物のSHA・version・構造と、main/tag/CI/Releaseの一致を確認。
- [ ] 所有者PCの更新結果/残る手動確認/外部前提を明記。
- [ ] レポートに個人データを含めていないことを確認。
- [ ] 索引、実装計画、VERIFICATION、ロードマップチェックを同期。
- [ ] 最終的な更新結果のコミットがmainへ反映済みであることを確認。
