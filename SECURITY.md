# Security Policy

## Data handling

Shortcut ManagerとTab Bridgeはlocal-onlyです。テレメトリ、analytics、外部backend、cloud syncを実装していません。Extensionはページ本文を読み取らず、host permissions/content scriptsを要求しません。last-used tabのグループ名・ID・URL・記録時刻はChrome local storageに新しい100グループ分まで保持します。PopupでURL補助保存をオフにすると既存URLも除去し、今後はIDと時刻だけを記録します。履歴消去はタブ/グループやその他の設定を変更しません。診断表示にURL履歴や任意の例外本文を含めません。

## Secrets

Store credential、署名証明書、private key、tokenをrepositoryへcommitしないでください。GitHub Actionsで将来公開を自動化する場合はGitHub Secretsを使用します。

## Filesystem safety

Managerが破壊的に置換できるのはownership markerを持つmanaged Start Menu folderだけです。Import元はread-onlyです。Apply/Restoreの前にはbackupが作成されます。

## Reporting

脆弱性は [GitHubの非公開報告フォーム](https://github.com/Soki0909/windows-shortcut-manager/security/advisories/new)から報告してください。GitHubへのサインインが必要です。機密性のある問題を公開Issueへ投稿しないでください。再現には架空のURLと一時フォルダを使い、秘密鍵・token・実際の業務情報を送らないでください。

通常の不具合や機能要望には [公開Issueフォーム](https://github.com/Soki0909/windows-shortcut-manager/issues/new/choose)を使い、実行版、導入形態、再現手順、期待/実結果を記載してください。設定やログの原本は不要です。共有が必要な場合も、内容を自分で確認して個人情報を除いてください。

非公開報告の有効化はリポジトリ設定として管理し、[GitHubの手順](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository)に従います。保守担当は非公開で影響と再現を確認し、修正版の検証と公開時期を相談します。受信・修正期限は未設定であり、即時対応を保証する窓口ではありません。
