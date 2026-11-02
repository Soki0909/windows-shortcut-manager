#requires -Version 7.4
param([string]$ChildArguments)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'lib/ExtensionDeployment.psm1') -Force
if ($ChildArguments) {
    $probe = Get-Content -LiteralPath $ChildArguments -Raw | ConvertFrom-Json -AsHashtable
    $phase = $probe.Phase
    $deployment = $probe.Deployment
    $null = Invoke-ExtensionDeployment @deployment -Checkpoint { param($point) if ($point -eq $phase) { [Diagnostics.Process]::GetCurrentProcess().Kill() } }
    throw 'Expected checkpoint was not reached.'
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('extension-deploy-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/ChromeExtension'))
function New-Archive([string]$Name, [string]$Version, [bool]$Old) {
    $path = Join-Path $root $Name
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object Name -notin @('README.md','package.json')) {
            $name = [IO.Path]::GetRelativePath($source,$file.FullName).Replace('\','/')
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            if ($name -eq 'manifest.json') {
                $manifest = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -AsHashtable
                $manifest.version = $Version; $bytes = [Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Depth 20))
            }
            if ($name -eq 'service-worker.js') { $bytes = [Text.Encoding]::UTF8.GetBytes("// fixture $Version") }
            $stream = $archive.CreateEntry($name).Open(); try { $stream.Write($bytes) } finally { $stream.Dispose() }
        }
        $stream = $archive.CreateEntry($(if ($Old) { 'obsolete.js' } else { 'new.js' })).Open()
        try { $stream.Write([Text.Encoding]::UTF8.GetBytes($Version)) } finally { $stream.Dispose() }
    } finally { $archive.Dispose() }
    return $path
}
$old = New-Archive 'old.zip' '0.9.0' $true
$new = New-Archive 'new.zip' '1.0.0' $false
function New-Fixture([string]$Name) {
    $parent = Join-Path $root $Name; [IO.Directory]::CreateDirectory($parent) | Out-Null
    $destination = Join-Path $parent 'extension'
    [IO.Compression.ZipFile]::ExtractToDirectory($old,$destination)
    [IO.File]::WriteAllText((Join-Path $destination 'notes.txt'),'user notes')
    [IO.Directory]::CreateDirectory((Join-Path $destination 'user/empty')) | Out-Null
    return @{ Destination=$destination; ArchivePath=$new; ArchiveSha256=(Get-FileHash -LiteralPath $new).Hash; PreviousArchivePath=$old; PreviousArchiveSha256=(Get-FileHash -LiteralPath $old).Hash }
}
function Get-Fingerprint([string]$Path) { & (Get-Module ExtensionDeployment) { param($p) Get-Tree $p } $Path }
function Assert-Tree([string]$Path, [hashtable]$Expected) {
    $actual = Get-Fingerprint $Path
    $same = & (Get-Module ExtensionDeployment) { param($a,$b) Test-SameTree $a $b } $actual $Expected
    if (-not $same) { throw "Tree mismatch: $Path" }
}
function Assert-Throws([scriptblock]$Action, [string]$Expected) {
    try { & $Action; throw 'Expected failure did not occur.' }
    catch { if ($_.Exception.Message -notlike "*$Expected*") { throw } }
}
$passed = 0
# Normal update retains identity, unknown files and empty user directories.
$args1 = New-Fixture 'normal'; $destination = $args1.Destination
$manifest = Get-Content -LiteralPath (Join-Path $destination 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
$manifest['key'] = 'fixture-stable-key'
[IO.File]::WriteAllText((Join-Path $destination 'manifest.json'), ($manifest | ConvertTo-Json -Depth 20))
$before = Get-Fingerprint $destination
$result = Invoke-ExtensionDeployment @args1
Assert-Tree (Join-Path $result.Backup 'previous') $before
$manifest = Get-Content -LiteralPath (Join-Path $destination 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.version -ne '1.0.0' -or $manifest.key -ne 'fixture-stable-key' -or (Test-Path -LiteralPath (Join-Path $destination 'obsolete.js')) -or
    (Get-Content -LiteralPath (Join-Path $destination 'notes.txt') -Raw) -ne 'user notes' -or -not (Test-Path -LiteralPath (Join-Path $destination 'user/empty'))) { throw 'Normal preservation failed.' }
$passed++
if ((Invoke-ExtensionDeployment @args1).State -ne 'unchanged') { throw 'Repeat update is not idempotent.' }; $passed++
# Modified obsolete files are retained; changed product files block overwrites.
$args2 = New-Fixture 'modified-obsolete'
[IO.File]::WriteAllText((Join-Path $args2.Destination 'obsolete.js'),'user modified')
$null = Invoke-ExtensionDeployment @args2
if ((Get-Content -LiteralPath (Join-Path $args2.Destination 'obsolete.js') -Raw) -ne 'user modified') { throw 'Modified obsolete file removed.' }; $passed++
$args3 = New-Fixture 'modified-product'
[IO.File]::WriteAllText((Join-Path $args3.Destination 'service-worker.js'),'user modified')
$before = Get-Fingerprint $args3.Destination
Assert-Throws { Invoke-ExtensionDeployment @args3 } 'Refusing to overwrite'
Assert-Tree $args3.Destination $before; $passed++
$args4 = New-Fixture 'hash-mismatch'; $before = Get-Fingerprint $args4.Destination; $args4.ArchiveSha256 = '0' * 64
Assert-Throws { Invoke-ExtensionDeployment @args4 } 'SHA-256/size mismatch'
Assert-Tree $args4.Destination $before; $passed++
# Copy/write failures must preserve a complete state and retain all evidence.
foreach ($phase in @('copy-old','write-new','journal','old-moved','new-moved','verified')) {
    $deployment = New-Fixture ('failure-' + $phase); $before = Get-Fingerprint $deployment.Destination
    Assert-Throws { Invoke-ExtensionDeployment @deployment -Checkpoint { param($point) if ($point -eq $phase) { throw [IO.IOException]::new('injected disk/copy failure') } } } 'injected disk/copy failure'
    if ($phase -in @('new-moved','verified')) {
        if ((Get-Content -LiteralPath (Join-Path $deployment.Destination 'manifest.json') -Raw | ConvertFrom-Json).version -ne '1.0.0') { throw 'Committed version not retained.' }
    } else { Assert-Tree $deployment.Destination $before }
    if ((Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly).State -ne 'no-pending') { throw 'Caught failure left pending work.' }
    $passed++
}
# Kill a separate PowerShell process so finally/catch cannot repair the state.
function Stop-AtCheckpoint([hashtable]$Deployment,[string]$Phase,[string]$Name) {
    $argumentsFile = Join-Path $root ($Name + '.json')
    @{ Deployment=$Deployment; Phase=$Phase } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $argumentsFile
    $process = Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile','-File',"`"$PSCommandPath`"",'-ChildArguments',"`"$argumentsFile`"") -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(20000)) { $process.Kill(); throw "Child timed out: $Phase" }
    if ($process.ExitCode -eq 0) { throw 'Child did not crash.' }
}
foreach ($phase in @('intent','copy-old','write-new','journal','old-moved','new-moved','verified')) {
    $deployment = New-Fixture ('crash-' + $phase); $before = Get-Fingerprint $deployment.Destination
    Stop-AtCheckpoint $deployment $phase ('child-' + $phase)
    $recovery = Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly
    if ($phase -in @('new-moved','verified')) {
        if ($recovery.State -ne 'new' -or (Get-Content -LiteralPath (Join-Path $deployment.Destination 'manifest.json') -Raw | ConvertFrom-Json).version -ne '1.0.0') { throw 'Complete new version not recovered.' }
        Assert-Tree (Join-Path $recovery.Backup 'previous') $before
    } else { Assert-Tree $deployment.Destination $before; if ($recovery.State -ne 'previous') { throw 'Previous state not recovered.' } }
    if ((Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly).State -ne 'no-pending') { throw 'Recovery is not repeatable.' }
    $passed++
}
foreach ($phase in @('recovery-validated','recovery-restored')) {
    $deployment = New-Fixture ('crash-' + $phase); $before = Get-Fingerprint $deployment.Destination
    Stop-AtCheckpoint $deployment 'old-moved' ('prepare-' + $phase)
    Stop-AtCheckpoint @{ Destination=$deployment.Destination; RecoverOnly=$true } $phase ('child-' + $phase)
    $recovery = Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly
    Assert-Tree $deployment.Destination $before
    if ($recovery.State -ne 'previous') { throw 'Interrupted recovery did not restore previous version.' }; $passed++
}
foreach ($kind in @('traversal','duplicate','missing-file','new-key')) {
    $deployment = New-Fixture ('invalid-' + $kind); $before = Get-Fingerprint $deployment.Destination
    $badZip = Join-Path $root ($kind + '.zip'); [IO.File]::Copy($new,$badZip)
    $zip = [IO.Compression.ZipFile]::Open($badZip,[IO.Compression.ZipArchiveMode]::Update)
    try {
        switch ($kind) {
            'traversal' { $entry = $zip.CreateEntry('../escape.txt'); $message = 'Unsafe relative name' }
            'duplicate' { $entry = $zip.CreateEntry('MANIFEST.JSON'); $message = 'Duplicate release entry' }
            'missing-file' { $zip.GetEntry('popup.js').Delete(); $message = 'Required extension file missing' }
            'new-key' {
                $entry = $zip.GetEntry('manifest.json'); $reader = [IO.StreamReader]::new($entry.Open())
                try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
                $entry.Delete(); $manifest['key']='new-key'
                $writer = [IO.StreamWriter]::new($zip.CreateEntry('manifest.json').Open())
                try { $writer.Write(($manifest | ConvertTo-Json -Depth 20)) } finally { $writer.Dispose() }
                $message = 'would change extension identity'
            }
        }
    } finally { $zip.Dispose() }
    $deployment.ArchivePath=$badZip; $deployment.ArchiveSha256=(Get-FileHash -LiteralPath $badZip).Hash
    Assert-Throws { Invoke-ExtensionDeployment @deployment } $message
    Assert-Tree $deployment.Destination $before; $passed++
}
$deployment = New-Fixture 'external-edit'; Stop-AtCheckpoint $deployment 'old-moved' 'child-external-edit'
[IO.Directory]::CreateDirectory($deployment.Destination) | Out-Null
[IO.File]::WriteAllText((Join-Path $deployment.Destination 'owner.txt'),'keep this')
Assert-Throws { Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly } 'Destination changed or is mixed'
if ((Get-Content -LiteralPath (Join-Path $deployment.Destination 'owner.txt') -Raw) -ne 'keep this') { throw 'Post-crash edit changed.' }; $passed++
$deployment = New-Fixture 'previous-edited'; Stop-AtCheckpoint $deployment 'old-moved' 'child-previous-edit'
$pending = @(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($deployment.Destination)) -Directory -Force | Where-Object Name -like '.shortcut-manager-extension-*')
if ($pending.Count -ne 1) { throw 'Expected unique pending transaction.' }
$previous = Join-Path $pending[0].FullName 'previous'
[IO.File]::WriteAllText((Join-Path $previous 'owner-added.txt'),'preserve post-crash edit')
$previousTree = Get-Fingerprint $previous
Assert-Throws { Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly } 'Previous directory changed'
Assert-Tree $previous $previousTree
if (Test-Path -LiteralPath $deployment.Destination) { throw 'Changed previous data was moved without consent.' }; $passed++
$deployment = New-Fixture 'corrupt-metadata'; Stop-AtCheckpoint $deployment 'journal' 'child-corrupt-metadata'
$before = Get-Fingerprint $deployment.Destination
$pending = @(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($deployment.Destination)) -Directory -Force | Where-Object Name -like '.shortcut-manager-extension-*')
[IO.File]::WriteAllText((Join-Path $pending[0].FullName 'journal.json'),'{broken')
Assert-Throws { Invoke-ExtensionDeployment -Destination $deployment.Destination -RecoverOnly } 'Conversion from JSON failed'
Assert-Tree $deployment.Destination $before; $passed++
$deployment = New-Fixture 'reparse'; $sentinel = Join-Path $root 'outside'; [IO.Directory]::CreateDirectory($sentinel) | Out-Null
[IO.File]::WriteAllText((Join-Path $sentinel 'keep.txt'),'keep')
New-Item -ItemType Junction -Path (Join-Path $deployment.Destination 'junction') -Target $sentinel | Out-Null
Assert-Throws { Invoke-ExtensionDeployment @deployment } 'Reparse point'
if ((Get-Content -LiteralPath (Join-Path $sentinel 'keep.txt') -Raw) -ne 'keep') { throw 'Junction target changed.' }; $passed++
Write-Host "Extension deployment tests passed: $passed. Isolated evidence: $root"
