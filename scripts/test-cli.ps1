param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe = Join-Path $root 'src/ShortcutManager.App/bin/Debug/net10.0-windows/ShortcutManager.Debug.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build Debug first with scripts/run-debug.ps1 -NoLaunch.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('shortcut-cli-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$valid = Join-Path $fixture 'valid.json'
$invalid = Join-Path $fixture 'invalid.json'
[IO.File]::WriteAllText($valid, '{"schemaVersion":1,"dispatchProtocolVersion":1,"settings":{},"shortcuts":[]}')
[IO.File]::WriteAllText($invalid, '{}')
function Invoke-Probe([string[]]$Arguments, [int]$ExpectedExit) {
    $process = Start-Process -FilePath $exe -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(20000)) {
        $process.Kill()
        throw 'Debug command timed out (possible interactive dialog).'
    }
    if ($process.ExitCode -ne $ExpectedExit) { throw "Expected $ExpectedExit, got $($process.ExitCode): $Arguments" }
}
$success = Join-Path $fixture 'success.json'
$failure = Join-Path $fixture 'failure.json'
Invoke-Probe @('--validate-config',"`"$valid`"",'--result',"`"$success`"") 0
if (-not (Get-Content -LiteralPath $success -Raw | ConvertFrom-Json).ok) { throw 'Valid config failed.' }
Invoke-Probe @('--validate-config',"`"$invalid`"",'--result',"`"$failure`"") 1
if ((Get-Content -LiteralPath $failure -Raw | ConvertFrom-Json).ok -ne $false) { throw 'Invalid config reported success.' }
Invoke-Probe @('--validate-config','--result',"`"$failure`"") 1
Invoke-Probe @('--validate-config',"`"$valid`"") 1
Invoke-Probe @('--unknown-option','--result',"`"$failure`"") 1
# A result destination below a regular file cannot be created.
Invoke-Probe @('--validate-config',"`"$valid`"",'--result',"`"$invalid/result.json`"") 1
if ((Get-Content -LiteralPath $valid -Raw) -notmatch '"schemaVersion":1') { throw 'Input changed.' }
if ((Get-Content -LiteralPath $invalid -Raw) -ne '{}') { throw 'Invalid input changed.' }
Write-Host "CLI probes passed (6): $fixture"
