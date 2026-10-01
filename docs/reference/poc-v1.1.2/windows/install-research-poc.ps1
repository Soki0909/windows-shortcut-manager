param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[a-p]{32}$')]
  [string]$ExtensionId
)

$ErrorActionPreference = 'Stop'

function Find-Chrome {
  $chrome = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
  ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

  if (-not $chrome) {
    throw 'chrome.exe was not found.'
  }

  return $chrome
}

$chrome = Find-Chrome
$programs = [Environment]::GetFolderPath('Programs')
$pocDir = Join-Path $programs 'Windows Shortcut Manager PoC'
New-Item -ItemType Directory -Force -Path $pocDir | Out-Null

$shortcutPath = Join-Path $pocDir 'Research - Chrome Tab Group.lnk'
$dispatcherUrl = "chrome-extension://$ExtensionId/dispatch.html?action=research"

$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $chrome
$shortcut.Arguments = '"' + $dispatcherUrl + '"'
$shortcut.WorkingDirectory = Split-Path $chrome
$shortcut.Description = 'Open Chrome tab group: Research (PoC)'
$shortcut.Hotkey = 'CTRL+ALT+R'
$shortcut.IconLocation = "$chrome,0"
$shortcut.WindowStyle = 1
$shortcut.Save()

Write-Host 'PoC shortcut created.'
Write-Host "Chrome: $chrome"
Write-Host "Shortcut: $shortcutPath"
Write-Host 'HotKey: Ctrl+Alt+R'
Write-Host "Dispatcher: $dispatcherUrl"
