# Security Policy

## Data handling

Shortcut ManagerとTab Bridgeはlocal-onlyです。テレメトリ、analytics、外部backend、cloud syncを実装していません。Extensionはページ本文を読み取らず、host permissions/content scriptsを要求しません。last-used tab URLはChrome local storageだけに保存します。

## Secrets

Store credential、署名証明書、private key、tokenをrepositoryへcommitしないでください。GitHub Actionsで将来公開を自動化する場合はGitHub Secretsを使用します。

## Filesystem safety

Managerが破壊的に置換できるのはownership markerを持つmanaged Start Menu folderだけです。Import元はread-onlyです。Apply/Restoreの前にはbackupが作成されます。

## Reporting

公開repositoryのSecurity Advisory機能、または所有者が指定する非公開連絡先から報告してください。機密性のある問題を公開Issueへ投稿しないでください。
