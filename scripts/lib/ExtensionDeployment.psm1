Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-PlainPath([string]$Path) {
    $probe = [IO.Path]::GetFullPath($Path)
    while ($probe) {
        if ((Test-Path -LiteralPath $probe) -and ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point is not supported: $probe" }
        $probe = [IO.Path]::GetDirectoryName($probe)
    }
}
function Assert-RelativeName([string]$Name) {
    if (-not $Name -or $Name.Contains('\') -or $Name.StartsWith('/') -or $Name.Contains(':')) { throw "Unsafe relative name: $Name" }
    foreach ($part in $Name.TrimEnd('/').Split('/')) {
        if (-not $part -or $part -in @('.','..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
            $part -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)') { throw "Unsafe relative name: $Name" }
    }
}
function Get-BytesHash([byte[]]$Bytes) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)) }
function Get-Tree([string]$Path) {
    Assert-PlainPath $Path
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { throw "Directory missing: $Path" }
    $map = @{}; [long]$total = 0
    $pending = [Collections.Generic.Queue[string]]::new(); $pending.Enqueue($Path)
    while ($pending.Count) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point is not supported: $($item.FullName)" }
            $relative = [IO.Path]::GetRelativePath($Path, $item.FullName).Replace('\','/'); Assert-RelativeName $relative
            if ($map.Count -ge 2048) { throw 'Extension directory exceeds 2048 entries.' }
            if ($item.PSIsContainer) { $map[$relative + '/'] = 'directory'; $pending.Enqueue($item.FullName) }
            else {
                $total += $item.Length
                if ($item.Length -gt 4MB -or $total -gt 64MB) { throw 'Extension directory exceeds bounded copy limits.' }
                $map[$relative] = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
            }
        }
    }
    return $map
}
function Test-SameTree([hashtable]$Left, [hashtable]$Right) {
    if ($Left.Count -ne $Right.Count) { return $false }
    foreach ($name in $Left.Keys) { if (-not $Right.ContainsKey($name) -or $Left[$name] -cne $Right[$name]) { return $false } }
    return $true
}
function Write-DurableJson([string]$Path, $Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 20))
    if ($bytes.Length -gt 1MB) { throw 'Recovery metadata exceeds 1 MiB.' }
    $temporary = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
    $stream = [IO.FileStream]::new($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough)
    try { $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::Move($temporary, $Path)
}
function Read-ReleaseArchive([string]$Path, [string]$Sha256) {
    if ($Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Supply the release SHA-256 from the verified public checksum file.' }
    Assert-PlainPath $Path; $file = Get-Item -LiteralPath $Path
    if ($file.Length -gt 32MB -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) { throw "Archive SHA-256/size mismatch: $Path" }
    $zip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
    try {
        if ($zip.Entries.Count -gt 256) { throw 'Too many release entries.' }
        $files = @{}; $seen = @{}; [long]$total = 0
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName; Assert-RelativeName $name
            if ($seen.ContainsKey($name.TrimEnd('/'))) { throw "Duplicate release entry: $name" }; $seen[$name.TrimEnd('/')] = $true
            if (($entry.ExternalAttributes -shr 16 -band 0xF000) -eq 0xA000) { throw "ZIP symbolic link: $name" }
            if ($name.EndsWith('/')) { continue }
            $total += $entry.Length
            if ($entry.Length -gt 4MB -or $total -gt 32MB) { throw 'Release exceeds expanded size limits.' }
            $inputStream = $entry.Open(); $output = [IO.MemoryStream]::new()
            try {
                $buffer = [byte[]]::new(8192)
                while (($count = $inputStream.Read($buffer,0,$buffer.Length)) -gt 0) {
                    if ($output.Length + $count -gt $entry.Length) { throw 'Expanded entry length mismatch.' }; $output.Write($buffer,0,$count)
                }
                if ($output.Length -ne $entry.Length) { throw 'Expanded entry length mismatch.' }; $files[$name] = $output.ToArray()
            } finally { $inputStream.Dispose(); $output.Dispose() }
        }
        foreach ($required in @('manifest.json','service-worker.js','dispatch.html','dispatch.js','popup.html','popup.js','popup.css')) { if (-not $files.ContainsKey($required)) { throw "Required extension file missing: $required" } }
        $manifest = [Text.Encoding]::UTF8.GetString($files['manifest.json']) | ConvertFrom-Json -AsHashtable
        if ($manifest.manifest_version -ne 3 -or $manifest.version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Invalid release manifest/version.' }
        foreach ($forbidden in @('commands','host_permissions','content_scripts','externally_connectable')) { if ($manifest.ContainsKey($forbidden)) { throw "Forbidden manifest key: $forbidden" } }
        foreach ($asset in @($manifest.background.service_worker, $manifest.action.default_popup) + @($manifest.icons.Values)) { if (-not $files.ContainsKey($asset)) { throw "Manifest asset missing: $asset" } }
        return @{ Files=$files; Manifest=$manifest }
    } finally { $zip.Dispose() }
}
function Read-Intent([string]$Path, [string]$Destination) {
    Assert-PlainPath $Path
    if (-not (Test-Path -LiteralPath $Path) -or (Get-Item -LiteralPath $Path).Length -gt 1MB) { throw "Recovery metadata missing/oversized. Preserve this folder for manual inspection: $Path" }
    $intent = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    if ($intent.FormatVersion -ne 1 -or $intent.Destination -cne $Destination -or $intent.Before -isnot [hashtable]) { throw 'Recovery metadata does not match this destination.' }
    foreach ($mapName in @('Before','After')) {
        if (-not $intent.ContainsKey($mapName)) { continue }; $map = $intent[$mapName]
        if ($map -isnot [hashtable] -or $map.Count -gt 2048) { throw 'Invalid recovery fingerprint.' }
        foreach ($name in $map.Keys) {
            Assert-RelativeName $name
            if (($name.EndsWith('/') -and $map[$name] -cne 'directory') -or (-not $name.EndsWith('/') -and $map[$name] -cnotmatch '^[A-F0-9]{64}$')) { throw 'Invalid recovery hash.' }
        }
    }
    return $intent
}
function Close-Transaction([string]$Transaction, [string]$Result) {
    # Predetermined sibling paths only. Never recursively delete update evidence.
    $archive = $Transaction + '.' + $Result + '-' + [Guid]::NewGuid().ToString('N')
    Assert-PlainPath $Transaction; [IO.Directory]::Move($Transaction, $archive); return $archive
}
function Repair-Deployment([string]$Destination, [string]$Transaction, [scriptblock]$Checkpoint = {}) {
    Assert-PlainPath $Destination; Assert-PlainPath $Transaction
    $journal = Join-Path $Transaction 'journal.json'
    $metadata = if (Test-Path -LiteralPath $journal) { $journal } else { Join-Path $Transaction 'intent.json' }
    $intent = Read-Intent $metadata $Destination; $previous = Join-Path $Transaction 'previous'
    $hasDestination = Test-Path -LiteralPath $Destination; $hasPrevious = Test-Path -LiteralPath $previous
    if ($hasPrevious -and -not (Test-SameTree (Get-Tree $previous) $intent.Before)) { throw 'Previous directory changed. Keep all evidence; automatic recovery stopped.' }
    & $Checkpoint 'recovery-validated'
    if ($hasDestination) {
        $current = Get-Tree $Destination
        if ($intent.ContainsKey('After') -and $hasPrevious -and (Test-SameTree $current $intent.After)) { return @{ State='new'; Backup=(Close-Transaction $Transaction 'completed') } }
        if (Test-SameTree $current $intent.Before) { return @{ State='previous'; Backup=(Close-Transaction $Transaction 'recovered') } }
        throw 'Destination changed or is mixed. Do not reload Chrome. Keep all evidence and inspect destination and previous directories.'
    }
    if (-not $hasPrevious) { throw 'Both destination and previous are missing. Preserve evidence; manual recovery is required.' }
    [IO.Directory]::Move($previous, $Destination)
    if (-not (Test-SameTree (Get-Tree $Destination) $intent.Before)) { throw 'Restored directory verification failed. Do not reload Chrome.' }
    & $Checkpoint 'recovery-restored'
    return @{ State='previous'; Backup=(Close-Transaction $Transaction 'recovered') }
}
function Invoke-ExtensionDeployment {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Destination, [string]$ArchivePath, [string]$ArchiveSha256,
        [string]$PreviousArchivePath, [string]$PreviousArchiveSha256, [switch]$RecoverOnly, [scriptblock]$Checkpoint = {})
    $Destination = [IO.Path]::GetFullPath($Destination).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($Destination) -eq [IO.Path]::GetPathRoot($Destination)) { throw 'A dedicated extension subdirectory is required.' }
    Assert-PlainPath $Destination
    $identity = (Get-BytesHash ([Text.Encoding]::UTF8.GetBytes($Destination.ToUpperInvariant()))).Substring(0,24)
    $transaction = Join-Path ([IO.Path]::GetDirectoryName($Destination)) ('.shortcut-manager-extension-' + $identity)
    $mutex = [Threading.Mutex]::new($false, 'Local\ShortcutManager.Extension.' + $identity); $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Another extension update/recovery is running.' }
        if (Test-Path -LiteralPath $transaction) {
            $result = Repair-Deployment $Destination $transaction $Checkpoint
            Write-Host "Recovered complete $($result.State) state. Evidence: $($result.Backup)"
            Write-Host 'Disk recovery only; Chrome execution version is not verified. Reload only after checking the intended version.'; return $result
        }
        if ($RecoverOnly) { return @{ State='no-pending'; Backup=$null } }
        $release = Read-ReleaseArchive $ArchivePath $ArchiveSha256; $oldRelease = Read-ReleaseArchive $PreviousArchivePath $PreviousArchiveSha256
        $before = Get-Tree $Destination
        $existingManifest = Get-Content -LiteralPath (Join-Path $Destination 'manifest.json') -Raw | ConvertFrom-Json -AsHashtable
        if ($release.Manifest.ContainsKey('key') -and (-not $existingManifest.ContainsKey('key') -or $release.Manifest.key -cne $existingManifest.key)) { throw 'New manifest key would change extension identity.' }
        if ($existingManifest.ContainsKey('key')) {
            if ($release.Manifest.ContainsKey('key') -and $release.Manifest.key -cne $existingManifest.key) { throw 'New manifest key would change extension identity.' }
            $release.Manifest['key'] = $existingManifest.key; $oldRelease.Manifest['key'] = $existingManifest.key
            $release.Files['manifest.json'] = [Text.Encoding]::UTF8.GetBytes(($release.Manifest | ConvertTo-Json -Depth 20))
        }
        if ($existingManifest.version -eq $release.Manifest.version) {
            foreach ($name in $release.Files.Keys) { if (-not $before.ContainsKey($name) -or $before[$name] -cne (Get-BytesHash $release.Files[$name])) { throw "Installed release differs: $name" } }
            Write-Host 'Requested release already matches disk. Chrome running-version verification is still required.'
            return @{ State='unchanged'; Backup=$null; Version=$release.Manifest.version }
        }
        if ($existingManifest.version -ne $oldRelease.Manifest.version) { throw 'Previous release version does not match installed extension.' }
        foreach ($name in $release.Files.Keys) {
            if (-not $before.ContainsKey($name) -or $before[$name] -eq (Get-BytesHash $release.Files[$name])) { continue }
            $owned = $oldRelease.Files.ContainsKey($name) -and $before[$name] -eq (Get-BytesHash $oldRelease.Files[$name])
            if ($name -eq 'manifest.json') { $owned = ($existingManifest | ConvertTo-Json -Depth 20 -Compress) -ceq ($oldRelease.Manifest | ConvertTo-Json -Depth 20 -Compress) }
            if (-not $owned) { throw "Refusing to overwrite modified/unknown file: $name" }
        }
        New-Item -ItemType Directory -Path $transaction | Out-Null
        Write-DurableJson (Join-Path $transaction 'intent.json') @{ FormatVersion=1; Destination=$Destination; Before=$before }
        & $Checkpoint 'intent'
        try {
            $stage = Join-Path $transaction 'stage'; New-Item -ItemType Directory -Path $stage | Out-Null
            foreach ($name in $before.Keys | Sort-Object) {
                $target = Join-Path $stage $name
                if ($name.EndsWith('/')) { [IO.Directory]::CreateDirectory($target) | Out-Null }
                else { [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null; [IO.File]::Copy((Join-Path $Destination $name), $target) }
                & $Checkpoint 'copy-old'
            }
            foreach ($name in $oldRelease.Files.Keys) {
                if (-not $release.Files.ContainsKey($name) -and $before.ContainsKey($name) -and $before[$name] -eq (Get-BytesHash $oldRelease.Files[$name])) { [IO.File]::Delete((Join-Path $stage $name)) }
            }
            foreach ($name in $release.Files.Keys | Sort-Object) {
                $target = Join-Path $stage $name; [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                $stream = [IO.FileStream]::new($target, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough)
                try { $stream.Write($release.Files[$name]); $stream.Flush($true) } finally { $stream.Dispose() }
                & $Checkpoint 'write-new'
            }
            $after = Get-Tree $stage
            foreach ($name in $release.Files.Keys) { if ($after[$name] -cne (Get-BytesHash $release.Files[$name])) { throw "Staged release mismatch: $name" } }
            foreach ($name in $before.Keys) {
                if ($name.EndsWith('/') -or $release.Files.ContainsKey($name)) { continue }
                $obsolete = $oldRelease.Files.ContainsKey($name) -and $before[$name] -eq (Get-BytesHash $oldRelease.Files[$name])
                if (-not $obsolete -and (-not $after.ContainsKey($name) -or $after[$name] -cne $before[$name])) { throw "User file not preserved: $name" }
            }
            Write-DurableJson (Join-Path $transaction 'journal.json') @{ FormatVersion=1; Destination=$Destination; Before=$before; After=$after; ReleaseSha256=$ArchiveSha256 }
            & $Checkpoint 'journal'
            if (-not (Test-SameTree (Get-Tree $Destination) $before)) { throw 'Destination changed during staging. No switch performed.' }
            [IO.Directory]::Move($Destination, (Join-Path $transaction 'previous')); & $Checkpoint 'old-moved'
            if (-not (Test-SameTree (Get-Tree (Join-Path $transaction 'previous')) $before)) { throw 'Original directory changed during switch. Keep previous data; do not reload Chrome.' }
            [IO.Directory]::Move($stage, $Destination); & $Checkpoint 'new-moved'
            if (-not (Test-SameTree (Get-Tree $Destination) $after)) { throw 'Committed extension fingerprint mismatch.' }; & $Checkpoint 'verified'
            $backup = Close-Transaction $transaction 'completed'
            Write-Host "Disk update verified: v$($release.Manifest.version). Previous directory backup: $(Join-Path $backup 'previous')"
            Write-Host 'Chrome extension reload and running-version verification are still required.'
            return @{ State='new'; Backup=$backup; Version=$release.Manifest.version }
        } catch {
            $original = $_
            try { $recovered = Repair-Deployment $Destination $transaction; Write-Warning "Complete $($recovered.State) state retained. Evidence: $($recovered.Backup)" }
            catch { Write-Warning "Automatic recovery stopped: $_. Do not reload Chrome. Run deploy-unpacked-extension.ps1 -Destination <same-path> -RecoverOnly after inspecting retained evidence." }
            throw $original
        }
    } finally { if ($acquired) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
}
Export-ModuleMember -Function Invoke-ExtensionDeployment
