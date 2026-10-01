param(
  [Parameter(Mandatory=$true)][string]$Destination,
  [Parameter(Mandatory=$true)][string]$ChatGptUrl,
  [Parameter(Mandatory=$true)][string]$NotionUrl,
  [Parameter(Mandatory=$true)][string]$GoogleDriveUrl
)
$ErrorActionPreference = 'Stop'
$resolvedDirectory = Split-Path -Parent $Destination
if (-not (Test-Path -LiteralPath $resolvedDirectory)) { throw "Destination directory does not exist: $resolvedDirectory" }
if (Test-Path -LiteralPath $Destination) {
  $backup = "$Destination.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
  Copy-Item -LiteralPath $Destination -Destination $backup -ErrorAction Stop
  Write-Host "Backup: $backup"
}
$lines = @(
  '@echo off',
  ('start "" "' + $ChatGptUrl + '"'),
  'timeout /t 1 /nobreak >nul',
  ('start "" "' + $NotionUrl + '"'),
  'timeout /t 1 /nobreak >nul',
  ('start "" "' + $GoogleDriveUrl + '"')
)
[IO.File]::WriteAllLines($Destination, $lines, [Text.Encoding]::Default)
Write-Host "Updated: $Destination"
