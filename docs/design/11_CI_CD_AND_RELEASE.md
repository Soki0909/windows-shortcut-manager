# 11. CI/CD / Release

## 1. CI workflow

`.github/workflows/ci.yml`

Triggers:
- push main
- pull_request

Jobs:

### dotnet
runner: `windows-latest`

Steps:
1. checkout
2. setup-dotnet 10.x
3. restore
4. build `Release`
5. test Core
6. test Windows
7. package dry-run

### extension
runner: `ubuntu-latest`

Steps:
1. checkout
2. setup-node current supported LTS
3. `node --test`
4. JSON parse manifest
5. custom policy check
6. create extension ZIP smoke
7. verify manifest is ZIP root

## 2. Policy checks

`scripts/verify-version.ps1` or cross-platform equivalent checks:

- `Directory.Build.props` Version
- Chrome manifest version
- release tag when applicable

Extension guard:
- no `commands`
- no `host_permissions`
- no `content_scripts`
- no unexpected remote URL in JS

## 3. Package script

`scripts/package.ps1 -Configuration Release -Version X.Y.Z`

Output:
`artifacts/`

### Windows
`dotnet publish`
- Release
- win-x64
- self-contained true
- no single-file requirement

ZIP whole publish folder:
`ShortcutManager-win-x64-vX.Y.Z.zip`

Folder ZIP を single EXE より優先。
WPF/native extraction edge casesを減らす。

### Chrome
copy source excluding tests/README dev files as appropriate。
ZIP root is manifest.

`ShortcutManager-TabBridge-vX.Y.Z.zip`

### Checksums
`SHA256SUMS.txt`

## 4. release.yml

Trigger:
- tag `v*`
- workflow_dispatch optional

Sequence:
1. version validation
2. full test gate
3. package
4. checksums
5. create GitHub Release via `gh` CLI / GitHub token
6. upload artifacts

Third-party release action 依存を必須にしない。

## 5. Chrome Web Store

v0.1.0:
- package is CWS-ready
- actual upload/publish is manual acceptable

Later:
- separate manual workflow `publish-chrome-web-store.yml`
- secrets only
- never repo commit credentials/private keys

## 6. Windows signing

v0.1.0 local/GitHub alpha:
unsigned acceptable。

Public stable:
- Authenticode code signing OR Microsoft Store/MSIX
- signing added as optional release stage

Certificate absenceで normal build/package を壊さない。

## 7. Release notes

`CHANGELOG.md` Keep a Changelog style。

Tag:
`v0.1.0`

Release title:
`Shortcut Manager v0.1.0`

Release notes:
- user-visible changes
- install/use
- Chrome extension setup
- known limitations
- checksums
- no auto-update note

## 8. Installation docs

README quick start:

1. Download Windows ZIP
2. Extract
3. Run ShortcutManager.exe
4. Install Chrome Extension:
   - developer mode for local alpha, or CWS later
5. paste/confirm Extension ID
6. Scan/import or add
7. Apply
8. close Manager
9. use HotKeys

No PowerShell manual steps required in finished product.
