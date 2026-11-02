param([string]$LatestInnoTag)
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$true
$policy=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'build-tools.json') -Raw|ConvertFrom-Json
if(-not $LatestInnoTag){$LatestInnoTag=(gh api repos/jrsoftware/issrc/releases/latest --jq .tag_name).Trim()}
if($LatestInnoTag -notmatch '^is-(\d+)_(\d+)_(\d+)$'){throw 'Unknown upstream Inno release format; inspect upstream before updating the pin.'}
$latest=[version]::new([int]$Matches[1],[int]$Matches[2],[int]$Matches[3])
$current=[version]$policy.InnoSetup.Version
[pscustomobject]@{Tool='Inno Setup';PinnedVersion=$current.ToString();LatestVersion=$latest.ToString();UpgradeAvailable=($latest -gt $current);ReleaseUrl="https://github.com/jrsoftware/issrc/releases/tag/$LatestInnoTag"}
