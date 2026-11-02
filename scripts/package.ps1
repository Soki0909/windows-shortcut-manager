param([ValidateSet('Release')][string]$Configuration = 'Release',[string]$Version = '0.3.1')
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
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $root 'assets/branding/README.md') -Destination (Join-Path $publish 'BRANDING.md')
# Use the exact runtime packages selected by publish, including transitive notices.
$assets = Get-Content -LiteralPath (Join-Path $root 'src/ShortcutManager.App/obj/project.assets.json') -Raw | ConvertFrom-Json
$dependencies = Get-Content -LiteralPath (Join-Path $publish 'ShortcutManager.deps.json') -Raw | ConvertFrom-Json
$licenseDirectory = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Path $licenseDirectory | Out-Null
foreach ($runtime in @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    $library = @($dependencies.libraries.PSObject.Properties | Where-Object { $_.Name.StartsWith('runtimepack.' + $runtime + '/', [StringComparison]::OrdinalIgnoreCase) })
    if ($library.Count -ne 1) { throw "Expected one runtime package: $runtime" }
    $packagePath = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder ($library[0].Name.Substring('runtimepack.'.Length).ToLowerInvariant())
        if (Test-Path -LiteralPath $candidate) { $packagePath = $candidate; break }
    }
    if (-not $packagePath) { throw "Runtime package missing: $runtime" }
    $notices = @(Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '^(LICENSE|THIRD.PARTY.NOTICES)' })
    if (-not @($notices | Where-Object Name -match '^LICENSE').Count) { throw "Runtime license missing: $runtime" }
    if ($runtime -eq 'Microsoft.NETCore.App.Runtime.win-x64' -and -not @($notices | Where-Object Name -match '^THIRD.PARTY.NOTICES').Count) { throw 'Runtime third-party notices missing.' }
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $licenseDirectory ($runtime + '-' + $notice.Name)) }
}
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$windowsZip = Join-Path $artifacts "ShortcutManager-win-x64-v$Version.zip"
$extensionZip = Join-Path $artifacts "ShortcutManager-TabBridge-v$Version.zip"
Remove-Item -LiteralPath $windowsZip,$extensionZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$publish\*" -DestinationPath $windowsZip -CompressionLevel Optimal
$extensionFiles = Get-ChildItem -LiteralPath "$root\src\ChromeExtension" -Force | Where-Object Name -notin @('README.md','package.json')
Compress-Archive -Path $extensionFiles.FullName -DestinationPath $extensionZip -CompressionLevel Optimal
Compress-Archive -LiteralPath (Join-Path $root 'LICENSE') -DestinationPath $extensionZip -Update
$checksums = @($windowsZip,$extensionZip) | ForEach-Object { $hash=Get-FileHash -Algorithm SHA256 -LiteralPath $_; "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))" }
[IO.File]::WriteAllLines((Join-Path $artifacts 'SHA256SUMS.txt'), $checksums, [Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$extensionEntries = [IO.Compression.ZipFile]::OpenRead($extensionZip)
try { if (-not ($extensionEntries.Entries.FullName -contains 'manifest.json')) { throw 'Extension ZIP must contain manifest.json at ZIP root.' } } finally { $extensionEntries.Dispose() }
Write-Host "Artifacts created in $artifacts"
