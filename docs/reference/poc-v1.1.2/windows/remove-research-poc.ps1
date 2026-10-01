$programs = [Environment]::GetFolderPath('Programs')
$pocDir = Join-Path $programs 'Windows Shortcut Manager PoC'
$shortcutPath = Join-Path $pocDir 'Research - Chrome Tab Group.lnk'

if (Test-Path $shortcutPath) {
  Remove-Item -Force $shortcutPath
  Write-Host "Removed: $shortcutPath"
} else {
  Write-Host 'PoC shortcut is not installed.'
}

if ((Test-Path $pocDir) -and -not (Get-ChildItem -Force $pocDir)) {
  Remove-Item -Force $pocDir
}
