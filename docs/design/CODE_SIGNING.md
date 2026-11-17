# Code signing policy / 導入準備

対象Issue #50・#140。証明書・署名サービスは未契約で、現在公開済みの版は未署名。所有者から利用可能な方式の準備を進める指示を受けた。秘密鍵の送付は不要。

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

app署名→ZIP生成→InnoによるSetupと内部イメージの署名→最終SHA-256の順。ZIPのSIGNING.jsonは4つの自作バイナリの署名後hash/署名状態を記録し、検査時に実体と照合する。Setupの検証記録はローカルSIGNING-SETUP.jsonへ保存する。`Unsigned`は未署名の開発用パッケージを生成する。

GitHub-hosted runnerへ証明書は用意されていない。現Release workflowはUnsignedのまま。採択/契約後に署名段階とpublisherを固定し、`publish-release.ps1 -RequireSigned`を通じてdraftから再downloadした配布物にも同じ発行元/信頼/timestamp検査を要求する。契約前にv1.0.0署名済みと称してタグ/公開しない。

## 実施済みと残る検証

証明書モードではInnoの`SignTool`と`SignedUninstaller=yes`を使い、Setup本体と、実行時の自己コピー・Uninstallに使われる内部イメージをコンパイラから署名する。ビルドごとの出力フォルダを使用し、コールバックは直下の指定Setup EXEとInno 7.1.1が生成する`uninst.e32.tmp` / `uninst.e64.tmp`に限定する。既存の証明書ストア・署名者・信頼チェーン・RFC3161 timestampの検証を適用し、コールバックの失敗でビルドを停止する。コンパイル後もSetup本体を検証し、成功してから配布先に移動してハッシュを作る。署名処理にはPowerShell 7が必要。 [Inno SignTool](https://jrsoftware.org/inno-setup/documentation/topic_setup_signtool.htm)、[SignedUninstaller](https://jrsoftware.org/inno-setup/documentation/topic_setup_signeduninstaller.htm)、[Inno 7.1.1のコンパイラ実装](https://github.com/jrsoftware/issrc/blob/is-7_1_1/Projects/Src/Compiler.SetupCompiler.pas)。

15件の境界テストで、原本の不変、署名必須時のfallback拒否、範囲外・欠損・非EXE・不正証明書・timestamp・junction・想定外コールバック入力の拒否を確認した。Inno 7.1.1と仮ペイロードで未署名コンパイル成功と、証明書設定なしの署名コールバック失敗によるコンパイル中断を確認した。

証明書の準備後に、同じ発行元の信頼できる署名とtimestampをSetup・内部イメージ・インストール済み`unins000.exe`で確認する。使い捨てGitHub Windows runnerで`verify-release-artifacts.ps1 -RequireSigned -RunInstallerSmoke`を実行すると、インストール済みUninstallを起動前に検証する。実署名、実サービスのtimestamp、配布物の再ダウンロード照合と発行元表示は未検証。#50・#140を継続する。
