param([string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
[xml]$props = Get-Content -Raw "$root\Directory.Build.props"
$productVersion = [string]$props.Project.PropertyGroup.Version
$manifest = Get-Content -Raw "$root\src\ChromeExtension\manifest.json" | ConvertFrom-Json
if ($productVersion -ne $manifest.version) { throw "Version mismatch: product=$productVersion extension=$($manifest.version)" }
if ($ExpectedVersion -and $productVersion -ne $ExpectedVersion.TrimStart('v')) { throw "Expected $ExpectedVersion, found $productVersion" }
foreach ($forbidden in @('commands','host_permissions','content_scripts','externally_connectable')) {
  if ($manifest.PSObject.Properties.Name -contains $forbidden) { throw "Forbidden extension manifest key: $forbidden" }
}
if ($manifest.manifest_version -ne 3) { throw 'Manifest V3 is required.' }
Write-Host "Version/policy check passed: $productVersion"

