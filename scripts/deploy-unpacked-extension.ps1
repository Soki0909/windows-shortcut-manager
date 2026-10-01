param(
  [Parameter(Mandatory=$true)][string]$Destination,
  [string]$Source = (Join-Path $PSScriptRoot '..\src\ChromeExtension')
)
$ErrorActionPreference = 'Stop'
$sourcePath = [IO.Path]::GetFullPath($Source)
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (-not (Test-Path -LiteralPath (Join-Path $sourcePath 'manifest.json'))) { throw "Source manifest not found: $sourcePath" }
if (-not (Test-Path -LiteralPath $destinationPath)) { throw "Existing unpacked extension directory not found: $destinationPath" }
if (-not (Test-Path -LiteralPath (Join-Path $destinationPath 'manifest.json'))) { throw "Destination is not an unpacked extension: $destinationPath" }
$backup = "$destinationPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Copy-Item -LiteralPath $destinationPath -Destination $backup -Recurse -ErrorAction Stop
Get-ChildItem -LiteralPath $sourcePath -Force | Where-Object Name -notin @('README.md','package.json') | ForEach-Object {
  if ($_.PSIsContainer) {
    $targetDirectory = Join-Path $destinationPath $_.Name
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
    Get-ChildItem -LiteralPath $_.FullName -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $targetDirectory -Recurse -Force }
  } else {
    Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
  }
}
Write-Host "Backup: $backup"
Write-Host "Updated unpacked extension: $destinationPath"
Write-Host 'Chrome extension reload is required.'
