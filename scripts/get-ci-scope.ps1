#requires -Version 7.4
param([string]$Base, [string]$Head = 'HEAD', [string[]]$Paths)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$windows = $false; $extension = $false; $deployment = $false
if (-not $PSBoundParameters.ContainsKey('Paths')) {
    if ($Base -notmatch '^[0-9a-fA-F]{40}$' -or $Base -eq ('0' * 40) -or $Head -notmatch '^(HEAD|[0-9a-fA-F]{40})$') {
        return [pscustomobject]@{ Windows=$true; Extension=$true; Deployment=$true; Reason='unknown comparison' }
    }
    git cat-file -e "$Base^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { $global:LASTEXITCODE=0; return [pscustomobject]@{ Windows=$true; Extension=$true; Deployment=$true; Reason='base unavailable' } }
    $Paths = @(git diff --name-only $Base $Head)
    if ($LASTEXITCODE -ne 0) { $global:LASTEXITCODE=0; return [pscustomobject]@{ Windows=$true; Extension=$true; Deployment=$true; Reason='diff unavailable' } }
}
foreach ($name in $Paths) {
    $name = $name.Replace('\','/')
    switch -Wildcard ($name) {
        '.github/workflows/*' { $windows=$extension=$deployment=$true; break }
        'scripts/get-ci-scope.ps1' { $windows=$extension=$deployment=$true; break }
        'scripts/test-ci-scope.ps1' { $windows=$extension=$deployment=$true; break }
        'Directory.Build.props' { $windows=$extension=$deployment=$true; break }
        'global.json' { $windows=$extension=$deployment=$true; break }
        'ShortcutManager.sln' { $windows=$extension=$deployment=$true; break }
        '*nuget.config' { $windows=$extension=$deployment=$true; break }
        'THIRD-PARTY-NOTICES.md' { $windows=$true; break }
        'assets/branding/*' { $windows=$true; break }
        '*.md' { break }
        'docs/*' { break }
        '.github/ISSUE_TEMPLATE/*' { break }
        '.gitignore' { break }
        '.editorconfig' { break }
        'scripts/sync-roadmap.ps1' { break } # Syntax checked by the changes job; never mutate issues in CI.
        'scripts/deploy-unpacked-extension.ps1' { $deployment=$true; break }
        'scripts/lib/ExtensionDeployment.psm1' { $deployment=$true; break }
        'scripts/test-extension-deployment.ps1' { $deployment=$true; break }
        'src/ChromeExtension/*' { $windows=$extension=$deployment=$true; break } # Includes the package/manifest contract.
        'tests/ChromeExtension.Tests/*' { $extension=$true; break }
        'tests/ChromeExtension.Live/*' { $extension=$true; break }
        'scripts/test-chrome-live.mjs' { $extension=$true; break }
        'src/ShortcutManager.*/*' { $windows=$true; break }
        'tests/ShortcutManager.*/*' { $windows=$true; break }
        'tests/Installer.Tests/*' { $windows=$true; break }
        'installer/*' { $windows=$true; break }
        'LICENSE' { $windows=$true; break }
        'scripts/*.ps1' { $windows=$true; break }
        default { $windows=$extension=$deployment=$true }
    }
}
return [pscustomobject]@{ Windows=$windows; Extension=$extension; Deployment=$deployment; Reason='changed paths' }
