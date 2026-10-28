param([ValidateSet('Release')][string]$Configuration = 'Release',[string]$Version = '0.1.1')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish\win-x64'
& "$PSScriptRoot\verify-version.ps1" -ExpectedVersion $Version
$resolvedArtifacts = [IO.Path]::GetFullPath($artifacts).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$resolvedPublish = [IO.Path]::GetFullPath($publish)
if (-not $resolvedPublish.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe publish path: $resolvedPublish" }
if (Test-Path -LiteralPath $resolvedPublish) { Remove-Item -LiteralPath $resolvedPublish -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish | Out-Null
& dotnet publish "$root\src\ShortcutManager.App\ShortcutManager.App.csproj" -c $Configuration -r win-x64 --self-contained true -o $publish --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path -LiteralPath (Join-Path $publish 'ShortcutManager.exe'))) { throw 'Published ShortcutManager.exe was not found.' }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$windowsZip = Join-Path $artifacts "ShortcutManager-win-x64-v$Version.zip"
$extensionZip = Join-Path $artifacts "ShortcutManager-TabBridge-v$Version.zip"
Remove-Item -LiteralPath $windowsZip,$extensionZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$publish\*" -DestinationPath $windowsZip -CompressionLevel Optimal
$extensionFiles = Get-ChildItem -LiteralPath "$root\src\ChromeExtension" -Force | Where-Object Name -notin @('README.md','package.json')
Compress-Archive -Path $extensionFiles.FullName -DestinationPath $extensionZip -CompressionLevel Optimal
$checksums = @($windowsZip,$extensionZip) | ForEach-Object { $hash=Get-FileHash -Algorithm SHA256 -LiteralPath $_; "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))" }
[IO.File]::WriteAllLines((Join-Path $artifacts 'SHA256SUMS.txt'), $checksums, [Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$extensionEntries = [IO.Compression.ZipFile]::OpenRead($extensionZip)
try { if (-not ($extensionEntries.Entries.FullName -contains 'manifest.json')) { throw 'Extension ZIP must contain manifest.json at ZIP root.' } } finally { $extensionEntries.Dispose() }
Write-Host "Artifacts created in $artifacts"
