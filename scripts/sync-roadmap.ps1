#requires -Version 7.4
param([string]$Repository = 'Soki0909/windows-shortcut-manager', [int]$Issue = 1, [switch]$Update)
$ErrorActionPreference = 'Stop'
$raw = gh issue list --repo $Repository --state all --limit 1000 --json number,state
if ($LASTEXITCODE -ne 0) { throw 'Could not read issue states.' }
$issues = @($raw | ConvertFrom-Json)
if ($issues.Count -ge 1000) { throw 'Issue list may be truncated; add pagination before updating.' }
$states = @{}
foreach ($item in $issues) { $states[$item.number.ToString()] = $item.state }
$raw = gh issue view $Issue --repo $Repository --json body
if ($LASTEXITCODE -ne 0) { throw 'Could not read roadmap body.' }
$body = ($raw | ConvertFrom-Json).body
$script:changed = 0
$updated = [regex]::Replace($body, '(?m)^- \[(?<state>[ xX])\] #(?<number>\d+)(?<rest>[^\r\n]*)$', {
    param($match)
    $number = $match.Groups['number'].Value
    if (-not $states.ContainsKey($number)) { throw "Referenced issue #$number is not in the complete issue list." }
    $checked = if ($states[$number] -eq 'CLOSED') { 'x' } else { ' ' }
    if ($match.Groups['state'].Value -cne $checked) { $script:changed++ }
    return '- [' + $checked + '] #' + $number + $match.Groups['rest'].Value
})
if ($changed -eq 0) { Write-Host 'Roadmap checkbox states match child issues.'; return }
if (-not $Update) { throw "$changed roadmap checkbox states are stale. Review issue states and run with -Update to synchronize only the checkboxes." }
# Preserve all prose and links. This command updates checkboxes only, not product scope.
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('shortcut-roadmap-' + [Guid]::NewGuid().ToString('N') + '.md')
try {
    [IO.File]::WriteAllText($temporary, $updated, [Text.UTF8Encoding]::new($false))
    gh issue edit $Issue --repo $Repository --body-file $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Roadmap update failed.' }
    $raw = gh issue view $Issue --repo $Repository --json body
    if ($LASTEXITCODE -ne 0 -or ($raw | ConvertFrom-Json).body -cne $updated) { throw 'Roadmap readback mismatch; inspect the remote issue before retrying.' }
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
Write-Host "Synchronized $changed checkbox states; prose unchanged."
