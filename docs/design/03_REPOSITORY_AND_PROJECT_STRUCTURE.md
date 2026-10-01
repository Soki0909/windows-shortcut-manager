# 03. Repository / Project Structure

## 1. Exact repository tree

Codex は原則として以下を作る。

```text
windows-shortcut-manager/
├─ AGENTS.md
├─ ShortcutManager.sln
├─ Directory.Build.props
├─ README.md
├─ CHANGELOG.md
├─ SECURITY.md
├─ .gitignore
├─ .editorconfig
│
├─ src/
│  ├─ ShortcutManager.Core/
│  │  ├─ ShortcutManager.Core.csproj
│  │  ├─ Models/
│  │  ├─ Validation/
│  │  ├─ Protocol/
│  │  ├─ Planning/
│  │  └─ Utilities/
│  │
│  ├─ ShortcutManager.Windows/
│  │  ├─ ShortcutManager.Windows.csproj
│  │  ├─ ShellLinks/
│  │  ├─ Inventory/
│  │  ├─ Apply/
│  │  ├─ Backup/
│  │  ├─ Chrome/
│  │  └─ Launching/
│  │
│  ├─ ShortcutManager.App/
│  │  ├─ ShortcutManager.App.csproj
│  │  ├─ App.xaml
│  │  ├─ App.xaml.cs
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  ├─ Services/
│  │  └─ Resources/
│  │
│  └─ ChromeExtension/
│     ├─ manifest.json
│     ├─ service-worker.js
│     ├─ dispatch.html
│     ├─ dispatch.js
│     ├─ popup.html
│     ├─ popup.js
│     ├─ popup.css
│     ├─ lib/
│     │  ├─ normalize.js
│     │  ├─ dispatch-protocol.js
│     │  ├─ group-selection.js
│     │  └─ tab-selection.js
│     ├─ icons/
│     └─ package.json
│
├─ tests/
│  ├─ ShortcutManager.Core.Tests/
│  ├─ ShortcutManager.Windows.Tests/
│  └─ ChromeExtension.Tests/
│
├─ docs/
│  ├─ requirements/
│  ├─ design/
│  ├─ architecture/
│  ├─ implementation/
│  │  └─ PLAN_v0.1.0.md
│  └─ verification/
│     └─ VERIFICATION.md
│
├─ schemas/
│  ├─ config.schema.json
│  └─ dispatch-result.schema.json
│
├─ scripts/
│  ├─ build.ps1
│  ├─ test.ps1
│  ├─ package.ps1
│  ├─ verify-version.ps1
│  └─ make-local-bootstrap-config.ps1
│
└─ .github/
   └─ workflows/
      ├─ ci.yml
      └─ release.yml
```

## 2. Project references

- `ShortcutManager.Core`: no project references
- `ShortcutManager.Windows` → Core
- `ShortcutManager.App` → Core + Windows
- Core.Tests → Core
- Windows.Tests → Core + Windows

## 3. Target frameworks

Core:
`net10.0`

Windows:
`net10.0-windows`

App:
`net10.0-windows`
`UseWPF=true`

Platform:
`win-x64` is release RID.

## 4. Dependency policy

追加 NuGet は「標準ライブラリでは合理的に実装できない場合」のみ。

v0.1.0 で許容:
- xUnit / Microsoft.NET.Test.Sdk（テスト）

原則追加しない:
- MVVM framework
- DI container
- logging framework
- JSON library
- installer framework

WPF MVVM の最小基盤 (`ObservableObject`, `RelayCommand`) は小さな自前実装でよい。

## 5. Composition root

`ShortcutManager.App/App.xaml.cs` が service instances を組み立てる。

例:
- ConfigRepository
- ShortcutInventoryScanner
- ShellLinkService
- ManagedShortcutApplier
- BackupService
- ChromeLocator
- ProcessLauncher
- MainViewModel

隠れた Service Locator は使わない。
