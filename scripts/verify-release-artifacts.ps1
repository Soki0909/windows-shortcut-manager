param([Parameter(Mandatory)][string]$Version, [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts", [switch]$RunInstallerSmoke,[switch]$RequireSigned,[string]$ExpectedSignerThumbprint=$env:CODE_SIGN_THUMBPRINT,[switch]$RequireMetadata,[string]$ExpectedSourceCommit,[switch]$RequireCleanSource)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
if($RequireSigned -and $ExpectedSignerThumbprint -notmatch '^[0-9a-fA-F]{40}$'){throw 'Signed-release verification requires the approved publisher thumbprint.'}
function Assert-ReleaseSignature($Signature,[bool]$Mandatory,[string]$Thumbprint){
    if($Signature.Status -notin 'NotSigned','Valid'){throw "Invalid artifact signature: $($Signature.Status)"}
    if($Mandatory -and ($Signature.Status -ne 'Valid' -or $null -eq $Signature.TimeStamperCertificate -or $Signature.SignerCertificate.Thumbprint -ne $Thumbprint)){throw 'Artifact must have a trusted, timestamped signature from the approved publisher.'}
}
$ArtifactDirectory = (Resolve-Path -LiteralPath $ArtifactDirectory).Path
$expected = @("ShortcutManager-win-x64-v$Version.zip", "ShortcutManager-TabBridge-v$Version.zip", "ShortcutManager-Setup-win-x64-v$Version.exe")
$lines = @(Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'SHA256SUMS.txt'))
if ($lines.Count -ne 3) { throw 'Exactly three checksums required.' }
$seen = @{}
foreach ($line in $lines) {
    if ($line -notmatch '^([a-f0-9]{64})  ([A-Za-z0-9.-]+)$') { throw 'Invalid checksum line.' }
    $hash = $Matches[1]; $name = $Matches[2]
    if ($name -notin $expected -or $seen.ContainsKey($name)) { throw 'Unexpected/duplicate checksum name.' }
    $seen[$name] = $true
    if ((Get-FileHash -LiteralPath (Join-Path $ArtifactDirectory $name) -Algorithm SHA256).Hash -ne $hash) { throw "Checksum mismatch: $name" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($name in $expected | Where-Object { $_.EndsWith('.zip') }) {
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $ArtifactDirectory $name))
    try {
        $names = @($zip.Entries.FullName)
        if (($names | Sort-Object -Unique).Count -ne $names.Count -or @($names | Where-Object { $_ -match '(^/|\.\.|:|ShortcutManager\.Debug|CrashDriver|LiveDriver|harness|node_modules)' }).Count) { throw "Unsafe ZIP structure: $name" }
        if ($name -like '*TabBridge*') {
            if ('LICENSE' -notin $names) { throw 'Extension license missing.' }
            $entry = $zip.GetEntry('manifest.json'); if (-not $entry) { throw 'Root manifest missing.' }
            $reader = [IO.StreamReader]::new($entry.Open()); try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            if ($manifest.version -ne $Version -or $manifest.manifest_version -ne 3) { throw 'Extension version mismatch.' }
            foreach ($key in @('commands','host_permissions','content_scripts','externally_connectable')) { if ($manifest.PSObject.Properties.Name -contains $key) { throw "Forbidden manifest key: $key" } }
        } else {
            foreach ($required in @('LICENSE','THIRD-PARTY-NOTICES.md','BRANDING.md','licenses/Microsoft.NETCore.App.Runtime.win-x64-LICENSE.TXT','licenses/Microsoft.NETCore.App.Runtime.win-x64-THIRD-PARTY-NOTICES.TXT','licenses/Microsoft.WindowsDesktop.App.Runtime.win-x64-LICENSE')) {
                if ($required -notin $names) { throw "Windows notice missing: $required" }
            }
            if ('ShortcutManager.exe' -notin $names -or 'ShortcutManager.runtimeconfig.json' -notin $names) { throw 'Windows payload missing.' }
            $metadataNames=@('BUILD-INFO.json','SBOM.spdx.json','ShortcutManager.deps.json')
            if($RequireMetadata -or 'BUILD-INFO.json' -in $names -or 'SBOM.spdx.json' -in $names){
                $metadataProbe=Join-Path ([IO.Path]::GetTempPath()) ('sm-build-metadata-'+[guid]::NewGuid().ToString('N'))
                New-Item -ItemType Directory -Path $metadataProbe|Out-Null
                try {
                    foreach($metadataName in $metadataNames){
                        $entry=$zip.GetEntry($metadataName);if(-not $entry -or $entry.Length -gt 1MB){throw 'Build metadata is missing or exceeds size limit.'}
                        $inputStream=$entry.Open();$outputStream=[IO.File]::Create((Join-Path $metadataProbe $metadataName))
                        try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
                    }
                    & "$PSScriptRoot/verify-build-metadata.ps1" -Directory $metadataProbe -ExpectedVersion $Version -ExpectedSourceCommit $ExpectedSourceCommit -RequireCleanSource:$RequireCleanSource
                }finally{foreach($metadataName in $metadataNames){$path=Join-Path $metadataProbe $metadataName;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}};Remove-Item -LiteralPath $metadataProbe}
            }
            $signing=$null
            $signingEntry=$zip.GetEntry('SIGNING.json')
            if($signingEntry){
                if($signingEntry.Length -gt 65536){throw 'Signing record exceeds size limit.'}
                $reader=[IO.StreamReader]::new($signingEntry.Open());try{$signing=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
                if($signing.FormatVersion -ne 1 -or $signing.Mode -notin 'Unsigned','Certificate' -or @($signing.Files).Count -ne 4){throw 'Invalid Windows signing record.'}
                $owned=@('ShortcutManager.exe','ShortcutManager.dll','ShortcutManager.Core.dll','ShortcutManager.Windows.dll')
                $seenOwned=@{}
                foreach($record in $signing.Files){
                    if($record.Name -notin $owned -or $seenOwned.ContainsKey($record.Name)){throw 'Unexpected or duplicate signing record entry.'};$seenOwned[$record.Name]=$true
                    $entry=$zip.GetEntry($record.Name);if(-not $entry -or $entry.Length -gt 64MB){throw 'Owned binary is missing or too large.'}
                    $signedProbe=Join-Path ([IO.Path]::GetTempPath()) ('sm-signature-'+[guid]::NewGuid().ToString('N')+[IO.Path]::GetExtension($record.Name))
                    try {
                        $inputStream=$entry.Open();$outputStream=[IO.File]::Create($signedProbe);try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
                        if((Get-FileHash -LiteralPath $signedProbe).Hash -ne $record.Sha256){throw 'Packaged binary differs from its post-signing hash.'}
                        $actualSignature=Get-AuthenticodeSignature -LiteralPath $signedProbe
                        $publisher=if($RequireSigned){$ExpectedSignerThumbprint}else{$record.SignerThumbprint}
                        Assert-ReleaseSignature $actualSignature ($RequireSigned -or $signing.Mode -eq 'Certificate') $publisher
                        if($actualSignature.Status.ToString() -ne $record.Status -or ($null -ne $actualSignature.TimeStamperCertificate) -ne $record.Timestamped){throw 'Packaged signature differs from the signing record.'}
                    } finally {if(Test-Path -LiteralPath $signedProbe){Remove-Item -LiteralPath $signedProbe}}
                }
            }elseif($RequireSigned){throw 'Signed Windows ZIP must include its signing record.'}
            $probe = Join-Path ([IO.Path]::GetTempPath()) ("sm-version-" + [Guid]::NewGuid().ToString('N') + '.exe')
            try {
                $inputStream = $zip.GetEntry('ShortcutManager.exe').Open()
                $outputStream = [IO.File]::Create($probe)
                try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose(); $outputStream.Dispose() }
                if ((Get-Item -LiteralPath $probe).VersionInfo.ProductVersion.Split('+')[0] -ne $Version) { throw 'Windows ZIP version mismatch.' }
                $appSignature = Get-AuthenticodeSignature -LiteralPath $probe
                Assert-ReleaseSignature $appSignature $RequireSigned $ExpectedSignerThumbprint
                Write-Host "App Authenticode: $($appSignature.Status)"
            } finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
        }
    } finally { $zip.Dispose() }
}
$setup = Join-Path $ArtifactDirectory $expected[2]
$signature = Get-AuthenticodeSignature -LiteralPath $setup
Assert-ReleaseSignature $signature $RequireSigned $ExpectedSignerThumbprint
# Unsigned is the declared distribution policy, never presented as signed.
Write-Host "Setup Authenticode: $($signature.Status)"
if ($RunInstallerSmoke) {
    if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') { throw 'Installer smoke is restricted to a disposable GitHub Windows runner.' }
    $temp = Join-Path $env:RUNNER_TEMP ("shortcut-manager-smoke-" + [Guid]::NewGuid().ToString('N'))
    $install = Join-Path $temp 'app'
    New-Item -ItemType Directory -Path $temp | Out-Null
    $process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$install`"",'/TASKS=',"/LOG=`"$temp/setup.log`"") -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup exit: $($process.ExitCode)" }
    try {
        $exe = Join-Path $install 'ShortcutManager.exe'
        if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion.Split('+')[0] -ne $Version) { throw 'Installed version mismatch.' }
        $auditLinkPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Shortcut Manager (監査のみ).lnk'
        if (-not (Test-Path -LiteralPath $auditLinkPath)) { throw 'Audit-only Start Menu shortcut is missing.' }
        # WScript loses paths outside the runner's ANSI code page. Compile the
        # same Unicode reader as the product; never rename or modify the source.
        if (-not ('ShortcutManager.Windows.ShellLinks.ShellLinkService' -as [type])) {
            Add-Type -Path (Join-Path $PSScriptRoot '../src/ShortcutManager.Windows/ShellLinks/ShellLinkService.cs')
        }
        $auditLink = [ShortcutManager.Windows.ShellLinks.ShellLinkService]::new().Read($auditLinkPath)
        if ($auditLink.TargetPath -ne $exe -or $auditLink.Arguments -ne '--audit-only' -or $auditLink.Hotkey) { throw 'Audit-only shortcut target, arguments or HotKey are incorrect.' }
        $fixture = Join-Path $temp 'empty.json'
        [IO.File]::WriteAllText($fixture, '{"schemaVersion":1,"dispatchProtocolVersion":1,"settings":{},"shortcuts":[]}')
        $result = Join-Path $temp 'validate.json'
        $process = Start-Process -FilePath $exe -ArgumentList @('--validate-config',"`"$fixture`"",'--result',"`"$result`"") -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0 -or -not (Get-Content -LiteralPath $result -Raw | ConvertFrom-Json).ok) { throw 'Installed CLI smoke failed.' }
    } finally {
        $uninstaller = Join-Path $install 'unins000.exe'
        if (Test-Path -LiteralPath $uninstaller) {
            $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
            if ($process.ExitCode -ne 0) { throw 'Uninstall smoke failed.' }
        }
    }
}
Write-Host "Release artifacts verified: $Version"
