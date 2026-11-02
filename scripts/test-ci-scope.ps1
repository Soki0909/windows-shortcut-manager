#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$cases = @(
    @{ Paths=@('docs/verification/VERIFICATION.md'); Expected=@($false,$false,$false) },
    @{ Paths=@('README.md','scripts/sync-roadmap.ps1'); Expected=@($false,$false,$false) },
    @{ Paths=@('src/ShortcutManager.App/MainWindow.xaml'); Expected=@($true,$false,$false) },
    @{ Paths=@('src/ShortcutManager.Core/Serialization/ConfigJson.cs'); Expected=@($true,$false,$false) },
    @{ Paths=@('tests/ChromeExtension.Tests/protocol.test.js'); Expected=@($false,$true,$false) },
    @{ Paths=@('src/ChromeExtension/service-worker.js'); Expected=@($true,$true,$true) },
    @{ Paths=@('installer/ShortcutManager.iss'); Expected=@($true,$false,$false) },
    @{ Paths=@('tests/Installer.Tests/PayloadMaintenance.iss'); Expected=@($true,$false,$false) },
    @{ Paths=@('scripts/lib/ExtensionDeployment.psm1'); Expected=@($false,$false,$true) },
    @{ Paths=@('scripts/publish-release.ps1'); Expected=@($true,$false,$false) },
    @{ Paths=@('LICENSE'); Expected=@($true,$false,$false) },
    @{ Paths=@('THIRD-PARTY-NOTICES.md'); Expected=@($true,$false,$false) },
    @{ Paths=@('assets/branding/README.md'); Expected=@($true,$false,$false) },
    @{ Paths=@('new-unknown-build-input'); Expected=@($true,$true,$true) },
    @{ Paths=@('.github/workflows/ci.yml'); Expected=@($true,$true,$true) },
    @{ Paths=@('Directory.Build.props'); Expected=@($true,$true,$true) },
    @{ Paths=@('src\ChromeExtension\manifest.json'); Expected=@($true,$true,$true) },
    @{ Paths=@('docs/design/readme.md','src/ShortcutManager.Windows/Backup/BackupService.cs'); Expected=@($true,$false,$false) }
)
foreach ($case in $cases) {
    $scope = & "$PSScriptRoot/get-ci-scope.ps1" -Paths $case.Paths
    $actual = @($scope.Windows,$scope.Extension,$scope.Deployment)
    if (($actual -join ',') -ne ($case.Expected -join ',')) { throw "CI scope mismatch: $($case.Paths) => $actual" }
}
$fallback = & "$PSScriptRoot/get-ci-scope.ps1" -Base ('0' * 40)
if (-not ($fallback.Windows -and $fallback.Extension -and $fallback.Deployment)) { throw 'New/unknown comparison must run all checks.' }
$missing = & "$PSScriptRoot/get-ci-scope.ps1" -Base ('a' * 40)
if (-not ($missing.Windows -and $missing.Extension -and $missing.Deployment) -or $LASTEXITCODE -ne 0) { throw 'Unavailable base must fall back without failing scope detection.' }
Write-Host "CI scope cases passed: $($cases.Count + 2)"
