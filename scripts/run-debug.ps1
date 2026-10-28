param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
$output = Join-Path $root 'src\ShortcutManager.App\bin\Debug\net10.0-windows'
# Retire only the old generated Debug launcher, which used the product name.
$legacyLauncher = Join-Path $output 'ShortcutManager.exe'
if (Test-Path -LiteralPath $legacyLauncher) { Remove-Item -LiteralPath $legacyLauncher -Force }
& dotnet build "$root\src\ShortcutManager.App\ShortcutManager.App.csproj" -c Debug --nologo
if ($LASTEXITCODE -ne 0) { throw 'Debug build failed.' }
$executable = Join-Path $output 'ShortcutManager.Debug.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Debug executable was not found.' }
if (-not $NoLaunch) { Start-Process -FilePath $executable -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden }
