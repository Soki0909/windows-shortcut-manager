param([Parameter(Mandatory)][string]$Version, [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts", [switch]$RunInstallerSmoke)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
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
            $entry = $zip.GetEntry('manifest.json'); if (-not $entry) { throw 'Root manifest missing.' }
            $reader = [IO.StreamReader]::new($entry.Open()); try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            if ($manifest.version -ne $Version -or $manifest.manifest_version -ne 3) { throw 'Extension version mismatch.' }
            foreach ($key in @('commands','host_permissions','content_scripts','externally_connectable')) { if ($manifest.PSObject.Properties.Name -contains $key) { throw "Forbidden manifest key: $key" } }
        } else {
            if ('ShortcutManager.exe' -notin $names -or 'ShortcutManager.runtimeconfig.json' -notin $names) { throw 'Windows payload missing.' }
            $probe = Join-Path ([IO.Path]::GetTempPath()) ("sm-version-" + [Guid]::NewGuid().ToString('N') + '.exe')
            try {
                $inputStream = $zip.GetEntry('ShortcutManager.exe').Open()
                $outputStream = [IO.File]::Create($probe)
                try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose(); $outputStream.Dispose() }
                if ((Get-Item -LiteralPath $probe).VersionInfo.ProductVersion.Split('+')[0] -ne $Version) { throw 'Windows ZIP version mismatch.' }
                $appSignature = Get-AuthenticodeSignature -LiteralPath $probe
                if ($appSignature.Status -notin @('NotSigned','Valid')) { throw "Invalid app signature: $($appSignature.Status)" }
                Write-Host "App Authenticode: $($appSignature.Status)"
            } finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
        }
    } finally { $zip.Dispose() }
}
$setup = Join-Path $ArtifactDirectory $expected[2]
$signature = Get-AuthenticodeSignature -LiteralPath $setup
if ($signature.Status -notin @('NotSigned','Valid')) { throw "Invalid Setup signature: $($signature.Status)" }
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
