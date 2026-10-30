param([string]$Version = '0.3.0', [string]$CompilerPath, [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be X.Y.Z.' }
& "$PSScriptRoot\verify-version.ps1" -ExpectedVersion $Version
if (-not $CompilerPath) {
    $candidates = @("$root\.work\tools\inno\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $CompilerPath) { $CompilerPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
}
if (-not $CompilerPath) { throw 'Inno Setup 6 is required for installer builds. Pass -CompilerPath <ISCC.exe>.' }
$publish = Join-Path $root 'artifacts\publish\win-x64'
if (-not $SkipPublish) {
    & "$PSScriptRoot\package.ps1" -Version $Version
    if ($LASTEXITCODE -ne 0) { throw 'Package generation failed.' }
}
if (-not (Test-Path -LiteralPath "$publish\ShortcutManager.exe")) { throw 'Published app is missing.' }
& $CompilerPath "/DAppVersion=$Version" "/DPublishDir=$publish" "$root\installer\ShortcutManager.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$artifacts = Join-Path $root 'artifacts'
$files = @("ShortcutManager-win-x64-v$Version.zip", "ShortcutManager-TabBridge-v$Version.zip", "ShortcutManager-Setup-win-x64-v$Version.exe")
$checksums = foreach ($name in $files) {
    $hash = Get-FileHash -LiteralPath (Join-Path $artifacts $name) -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $name"
}
[IO.File]::WriteAllLines((Join-Path $artifacts 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
