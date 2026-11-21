param([string]$Version = '1.0.0', [string]$CompilerPath, [switch]$SkipPublish,[ValidateSet('Unsigned','Certificate')][string]$SigningMode='Unsigned',[switch]$RequireSigned)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
if ($RequireSigned -and $SigningMode -ne 'Certificate') { throw 'A signed release requires Certificate mode; unsigned fallback is forbidden.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be X.Y.Z.' }
& "$PSScriptRoot\verify-version.ps1" -ExpectedVersion $Version
if (-not $CompilerPath) {
    $candidates = @("$root\.work\tools\inno\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $CompilerPath) { $CompilerPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
}
if (-not $CompilerPath) { throw 'The pinned Inno Setup tool is required. Run prepare-installer-tools.ps1 or pass -CompilerPath <ISCC.exe>.' }
$publish = Join-Path $root 'artifacts\publish\win-x64'
if (-not $SkipPublish) {
    & "$PSScriptRoot\package.ps1" -Version $Version -SigningMode $SigningMode -RequireSigned:$RequireSigned
    if ($LASTEXITCODE -ne 0) { throw 'Package generation failed.' }
}
if (-not (Test-Path -LiteralPath "$publish\ShortcutManager.exe")) { throw 'Published app is missing.' }
$signingRecord=Get-Content -LiteralPath (Join-Path $publish 'SIGNING.json') -Raw|ConvertFrom-Json
if($signingRecord.Mode -ne $SigningMode -or $RequireSigned -and $SigningMode -ne 'Certificate'){throw 'Published app signing policy does not match Setup signing policy.'}
$payloadManifest=Join-Path $root 'artifacts/installer-payload.ini'
& "$PSScriptRoot/write-installer-payload.ps1" -Version $Version -PublishDirectory $publish -OutputPath $payloadManifest
$artifacts = Join-Path $root 'artifacts'
$setupName = "ShortcutManager-Setup-win-x64-v$Version.exe"
$setup = Join-Path $artifacts $setupName
$compileArguments = @("/DAppVersion=$Version", "/DPublishDir=$publish", "/DPayloadManifest=$payloadManifest")
if ($SigningMode -eq 'Certificate') {
    $stage = Join-Path $artifacts ('inno-signing-' + [guid]::NewGuid().ToString('N'))
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    if (-not (Test-Path -LiteralPath $pwsh -PathType Leaf)) { throw 'Certificate mode requires PowerShell 7.' }
    for ($directory = Get-Item -LiteralPath $artifacts; $null -ne $directory; $directory = $directory.Parent) {
        if ($directory.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Installer signing rejects reparse ancestors.' }
    }
    New-Item -ItemType Directory -Path $stage | Out-Null
    # Inno expands $q/$f itself. A literal dollar in a path must remain literal.
    $hostPath = $pwsh.Replace('$','$$')
    $callback = (Join-Path $PSScriptRoot 'sign-inno-output.ps1').Replace('$','$$')
    $stageArgument = $stage.Replace('$','$$')
    $signCommand = '-sShortcutManagerCertificate=$q{0}$q -NoProfile -NonInteractive -File $q{1}$q -AllowedRoot $q{2}$q -SetupName {3} -Path $f' -f $hostPath,$callback,$stageArgument,$setupName
    $compileArguments += @('-ni',"/O$stage",'/DSignInstaller=1',$signCommand)
}
& $CompilerPath @compileArguments "$root\installer\ShortcutManager.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
if ($SigningMode -eq 'Certificate') {
    $stagedSetup = Join-Path $stage $setupName
    $setupSignature = @(& "$PSScriptRoot/sign-files.ps1" -Path $stagedSetup -AllowedRoot $stage)
    if ($setupSignature[0].Status -ne 'Valid' -or -not $setupSignature[0].Timestamped -or $setupSignature[0].SignerThumbprint -ne $env:CODE_SIGN_THUMBPRINT) {
        throw 'Compiled Setup must have the selected publisher, a trusted signature and a timestamp.'
    }
    [IO.File]::Move($stagedSetup,$setup,$true)
    [IO.Directory]::Delete($stage)
} else {
    $setupSignature = @(& "$PSScriptRoot/sign-files.ps1" -Path $setup -AllowedRoot $artifacts)
}
[IO.File]::WriteAllText((Join-Path $artifacts 'SIGNING-SETUP.json'),(@{FormatVersion=1;Mode=$SigningMode;Files=$setupSignature}|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$files = @("ShortcutManager-win-x64-v$Version.zip", "ShortcutManager-TabBridge-v$Version.zip", "ShortcutManager-Setup-win-x64-v$Version.exe")
$checksums = foreach ($name in $files) {
    $hash = Get-FileHash -LiteralPath (Join-Path $artifacts $name) -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $name"
}
[IO.File]::WriteAllLines((Join-Path $artifacts 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
