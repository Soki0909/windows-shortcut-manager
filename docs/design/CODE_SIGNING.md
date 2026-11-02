# Code signing policy / 導入準備

対象Issue: #50。2026-11-02時点では証明書・サービス未契約。現在公開済みの版は未署名であり、署名提供を受けているとは表示しない。所有者から利用可能な方式の準備を進める指示を受けた。秘密鍵の送付は不要。

## 方式の候補

| 方式 | 適用条件・準備 | 現在の状態 |
| --- | --- | --- |
| SignPath Foundation | OSS審査、公開済みソース/配布物、役割とMFA、検証可能なビルド、各署名の承認が必要。発行元はFoundation。 | 第一の申請候補。採択・評判要件の充足は未確認。申請/利用規約への同意は未実施。 |
| CAの個人向けコード署名証明書 | 本人確認、契約、hardware/cloud key provider。Windows証明書ストアからSignToolが利用できるproviderなら下記実装を使う。 | 費用・契約とprovider連携は所有者の判断待ち。購入はしていない。 |
| Microsoft Artifact Signing Public Trust | 対応地域・主体の本人/組織確認が必要。日本の組織は対象、個人は現時点で米国/カナダのみ。 | 日本の個人として利用できる前提にはしない。主体が適合する場合に再検討。 |

一次情報: [Foundation条件](https://signpath.org/terms.html)、[Microsoftの利用資格](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart)、[Windowsの署名方式](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)。契約時に最新条件を再確認する。署名があってもWindowsの警告が常に消えるとは約束しない。

## 申請用の公開情報

- Project: Shortcut Manager / Tab Bridge、[repository](https://github.com/Soki0909/windows-shortcut-manager)、MIT、[公開済み配布](https://github.com/Soki0909/windows-shortcut-manager/releases)、[利用方法](../../README.md)。
- Windows用の非常駐ショートカット管理。Apply/Restoreは管理フォルダとバックアップを保護し、通常動作に管理者権限を要求しない。
- [Privacy/Security](../../SECURITY.md)。telemetryなし。URLを開く、Chromeへ指示する、公開ページを明示的に開く操作等では利用者の操作に応じて通信が発生する。
- 署名対象: 自作のShortcutManager.exe / ShortcutManager.dll / ShortcutManager.Core.dll / ShortcutManager.Windows.dllとSetup EXE。Microsoft runtime、Inno tool、他者のDLLを自作物として再署名しない。
- maintainer/reviewer/approverの実際の担当者、MFA、署名アカウント/権限、Foundationの表示文言は、採択と所有者確認の後に確定する。未採択の段階でスポンサー表記を追加しない。

申請フォームの送信、契約/支払い、本人確認、鍵の生成/取込みは本作業では行っていない。SignPathは採択後にサービス専用の成果物検証/承認連携を追加する必要がある。証明書ストア用の実装がSignPath APIにそのまま対応するとは扱わない。

## 実装済みの証明書ストア経路

Windows SDKの[SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)と既存PowerShellだけを使う。選択した証明書の秘密鍵・有効期間・code-signing EKUと、SignToolの信頼を確認する。SHA-256、RFC3161 timestampを必須とし、署名後にAuthenticodeと選択した発行元を検証する。対象はartifact root内のEXE/DLLに限定し、junction等を追跡しない。

```powershell
# 公開情報のみ。秘密鍵・PFX password・tokenを引数やソースへ入れない。
$env:CODE_SIGN_THUMBPRINT = '<approved 40-hex certificate thumbprint>'
$env:CODE_SIGN_TIMESTAMP_URL = '<provider RFC3161 timestamp URL>'
./scripts/package.ps1 -Version X.Y.Z -SigningMode Certificate -RequireSigned
./scripts/package-installer.ps1 -Version X.Y.Z -SkipPublish -SigningMode Certificate -RequireSigned
./scripts/verify-release-artifacts.ps1 -Version X.Y.Z -RequireSigned
```

app署名→ZIP/Setup生成→Setup署名→最終SHA-256の順。ZIPのSIGNING.jsonは4つの自作バイナリの署名後hash/署名状態を記録し、検査時に実体と照合する。Setupの検証記録はローカルSIGNING-SETUP.jsonへ保存する。必要な署名が失敗した場合、未署名へfallbackせず停止する。`Unsigned`は開発用パッケージを継続できる明示モードで、署名済みとは表示しない。

GitHub-hosted runnerへ証明書は用意されていない。現Release workflowはUnsignedのまま。採択/契約後に署名段階とpublisherを固定し、`publish-release.ps1 -RequireSigned`を通じてdraftから再downloadした配布物にも同じ発行元/信頼/timestamp検査を要求する。契約前にv1.0.0署名済みと称してタグ/公開しない。

## 実施済みと残る検証

11境界ケースで未署名検査の原本不変、署名必須時のfallback拒否、範囲外/欠損/非EXE/不正証明書/timestamp/junction拒否を確認。実証明書による署名成功、timestamp到達性、失効/信頼チェーン、GitHubからの署名付き再download、実機での発行元確認は外部前提が未用意のため未実施。#50は開いたまま維持する。
