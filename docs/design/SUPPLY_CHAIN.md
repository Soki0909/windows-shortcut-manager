# 配布物とビルド依存の保守

対象: #60。通常の製品動作に依存を追加せず、ビルド元・同梱部品・公開物を対応付ける。

## 固定と更新検知

- .NET SDKは`global.json`で10.0.403を指定し、rollForwardを無効にする。CI/Releaseのsetup-dotnetも同じファイルを使う。
- GitHub Actionsは公式repositoryから取得したcommit SHAへ固定する。版はコメントだけに残す。major更新も手動で互換性を確認してから取り込む。来歴生成には公式actions/attestを使う。#142ではcheckout7.0.1/setup-dotnet6.0.0/setup-node7.1.0の公式SHA/MITとhosted runner互換を確認し、CI後に更新した。
- Dependabotは毎週、github-actions、nuget、dotnet-sdkを確認する。Action/NuGetは関連更新をまとめ、自動mergeはしない。major更新は互換性と対応Windows/SDKを確認する。
- Inno Setupは`scripts/build-tools.json`に版・公式URL・SHA-256を固定する。準備時にhashとAuthenticodeの両方を検証してからworkspaceのビルドツールを準備する。`Build tool updates` workflowは毎週公式最新releaseを照会し、新版/不明な版形式なら失敗して保守担当に確認を促す。自動ダウンロード/固定値変更は行わない。
- 定期workflowが停止している場合、保守担当は月1回とリリース前に`./scripts/check-build-tools.ps1`を実行し、新版をIssue化する。初回で6.7.3→7.1.1を検出し#139として対応した。

根拠: [DependabotのSDK更新](https://devblogs.microsoft.com/dotnet/using-dependabot-to-manage-dotnet-sdk-updates/)、Innoの[公式変更履歴](https://jrsoftware.org/files/is7-whatsnew.htm)と[利用条件](https://jrsoftware.org/files/is/license.txt)。同梱される著作権表示を残す。

## 更新PRの検証

保守担当はリリース元・変更履歴・ライセンス・新しい権限/通信・対応環境を確認する。NuGet追加/更新では推移依存と`dotnet list ShortcutManager.sln package --vulnerable --include-transitive`も確認し、検出した問題を記録する。実runtimeの原文noticeはpublishが選んだpackから同梱し、SBOMだけで権利表記を置き換えない。

変更した境界のテスト・ビルド・配布検査を実行する。共有SDK/Action変更ではCI全job、installer tool変更では使い捨てWindows runnerのinstall/CLI/audit-only link/uninstallを通す。個人PCに開発Setupをインストールしない。tool URL/hashは公式配布実体と照合し、単にCIが要求する値へ書き換えない。

## 配布記録

Windows ZIP/Setupのpayloadへ次を同梱する。

- `SBOM.spdx.json`: SPDX 2.3。実publishのdeps.jsonから製品project、NuGet、self-contained runtime packと依存関係を列挙する。NuGetは解決済みcacheのSHA-512、nuspecのlicense expression、purlを記録する。不明なlicenseはNOASSERTIONとし、MITと推測しない。テスト専用依存は同梱部品として数えない。
- `BUILD-INFO.json`: 製品版、固定repository、commit、dirtyの有無、実SDK、runtime target、deps/SBOMのSHA-256。個人パス、環境変数一覧、ユーザー名、設定、URL履歴、tokenは出力しない。Release workflowはclean checkoutを要求する。
- `SIGNING.json`: 自作バイナリの署名後hash/状態。[Code signing policy](CODE_SIGNING.md)に従う。

SBOMはpackage単位で、runtimeの全ファイルや組み込まれた第三者ソースを個別解析した一覧ではない。runtimeの第三者noticeも同梱する。Tab Bridgeは第三者runtimeライブラリをbundleしておらず、Windows用SBOMの範囲に含めない。拡張ZIPは同じソースcommitから生成し、独立した最終hash/来歴の対象にする。

配布検査はSBOMとdepsの版/件数/依存辺の完全一致、package hash形式、metadata hash、要求されたcommitとclean状態を確認する。過去版の調査用にはmetadata未同梱を許容できるが、新Releaseのpublishではmetadataを必須にする。

## 署名された来歴

Release workflowは最終ZIP・Setup・SHA256SUMSにGitHub artifact attestationを作成する。OIDC/attestations権限は配布jobだけに付け、鍵をrepositoryへ置かない。workflow_dispatchは公開せず来歴の生成/検証まで、tagの場合はdraftから再downloadした全4assetの来歴とhashも確認してから公開する。

検証ではrepositoryだけでなく、`.github/workflows/release.yml`、ソースcommit/ref、GitHub-hosted runnerを固定する。BUILD-INFOの自己申告を署名された来歴と同じ証拠にしない。Windows Authenticodeとも別であり、attestationがあっても未署名EXEがコード署名済みにはならない。[GitHub公式の仕組み](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations)を使う。

```powershell
# 新しい来歴対応版の公式downloadで実行。過去版にあるとは仮定しない。
gh attestation verify ./ShortcutManager-Setup-win-x64-vX.Y.Z.exe `
  --repo Soki0909/windows-shortcut-manager `
  --signer-workflow Soki0909/windows-shortcut-manager/.github/workflows/release.yml `
  --source-ref refs/tags/vX.Y.Z --source-digest <release-commit> --deny-self-hosted-runners
```

ZIP/timestamp/署名/SBOM時刻は実行ごとに異なるため、別実行の成果物がbyte単位で同じとは主張しない。ソースと依存版・tool policyを固定し、各公開物そのもののhash/署名/来歴を確認する。
