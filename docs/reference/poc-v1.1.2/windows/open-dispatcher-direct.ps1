param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[a-p]{32}$')]
  [string]$ExtensionId
)

$ErrorActionPreference = 'Stop'

$chrome = @(
  "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
  "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
  "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $chrome) {
  throw 'chrome.exe was not found.'
}

$url = "chrome-extension://$ExtensionId/dispatch.html?action=research"
Write-Host "Chrome: $chrome"
Write-Host "Opening: $url"
Start-Process -FilePath $chrome -ArgumentList @($url)
