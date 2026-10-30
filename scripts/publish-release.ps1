param([Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if ($Tag -notmatch '^v(\d+\.\d+\.\d+)$') { throw 'Release tag must be vX.Y.Z.' }
$version = $Matches[1]
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Publish only through the release workflow.' }
if ((git rev-list -n 1 $Tag) -ne (git rev-parse HEAD)) { throw 'Tag/checkout mismatch.' }
$names = @("ShortcutManager-win-x64-$Tag.zip", "ShortcutManager-TabBridge-$Tag.zip", "ShortcutManager-Setup-win-x64-$Tag.exe", 'SHA256SUMS.txt')
$existing = @(gh release list --limit 100 --json tagName,isDraft | ConvertFrom-Json) | Where-Object tagName -eq $Tag
if ($existing -and -not $existing.isDraft) { throw 'Published releases are immutable. Use a new version.' }
if (-not $existing) {
    $assets = $names | ForEach-Object { Join-Path 'artifacts' $_ }
    gh release create $Tag @assets --draft --verify-tag --title "Shortcut Manager $Tag" --notes-file "docs/releases/$Tag.md"
} else {
    $release = gh release view $Tag --json assets | ConvertFrom-Json
    if (@($release.assets).Count -ne 4 -or @($release.assets | Where-Object { $_.name -notin $names }).Count) {
        throw 'Draft assets are incomplete/unexpected. Inspect the draft, remove it explicitly, then rerun. Assets are never overwritten.'
    }
}
$download = Join-Path $env:RUNNER_TEMP ("release-download-" + [Guid]::NewGuid().ToString('N'))
gh release download $Tag --dir $download
if (-not $existing) {
    foreach ($name in $names) {
        if ((Get-FileHash -LiteralPath "artifacts/$name").Hash -ne (Get-FileHash -LiteralPath (Join-Path $download $name)).Hash) { throw "Downloaded asset differs: $name" }
    }
}
& "$PSScriptRoot/verify-release-artifacts.ps1" -Version $version -ArtifactDirectory $download -RunInstallerSmoke
gh release edit $Tag --draft=false --latest
Write-Host "Published verified release: $Tag"
