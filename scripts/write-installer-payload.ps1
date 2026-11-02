param([Parameter(Mandatory)][string]$PublishDirectory,[Parameter(Mandatory)][string]$OutputPath,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid installer payload version.'}
$root=(Resolve-Path -LiteralPath $PublishDirectory).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
for($ancestor=Get-Item -LiteralPath $root;$null -ne $ancestor;$ancestor=$ancestor.Parent){
    if($ancestor.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){throw 'Installer payload does not follow reparse ancestors.'}
}
$files=[Collections.Generic.List[object]]::new()
function Read-PayloadDirectory([string]$Directory){
    $item=Get-Item -LiteralPath $Directory
    if($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){throw 'Installer payload does not follow reparse points.'}
    foreach($child in Get-ChildItem -LiteralPath $Directory -Force){
        if($child.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){throw 'Installer payload does not follow reparse points.'}
        if($child.PSIsContainer){Read-PayloadDirectory $child.FullName;continue}
        $relative=[IO.Path]::GetRelativePath($root,$child.FullName)
        if($relative.Split([char]'\')|Where-Object {$_ -in '.','..' -or $_ -ne $_.Trim([char]' ') -or $_.EndsWith('.') -or $_ -match '[\x00-\x1f:"/|?*]'}){throw 'Unsafe installer payload name.'}
        if($relative.StartsWith('.shortcut-manager-',[StringComparison]::OrdinalIgnoreCase) -or $relative.StartsWith('unins',[StringComparison]::OrdinalIgnoreCase)) {throw 'Installer metadata/uninstaller is not a publish payload.'}
        $files.Add([pscustomobject]@{Path=$relative;Sha256=(Get-FileHash -LiteralPath $child.FullName).Hash.ToLowerInvariant()})
    }
}
Read-PayloadDirectory $root
if($files.Count -lt 1 -or $files.Count -gt 4096){throw 'Installer payload count is outside its limit.'}
$lines=[Collections.Generic.List[string]]::new()
@('[Payload]','Format=ShortcutManager.InstallPayload.v1',"Version=$Version","Count=$($files.Count)")|ForEach-Object {$lines.Add($_)}
$index=0
foreach($file in $files|Sort-Object Path){
    @('',"[File$index]","Path=$($file.Path)","Sha256=$($file.Sha256)")|ForEach-Object {$lines.Add($_)}
    $index++
}
$text=$lines -join "`r`n"
if([Text.Encoding]::Unicode.GetByteCount($text) -gt 1MB){throw 'Installer payload record exceeds size limit.'}
# Windows INI APIs use the UTF-16 BOM; do not lose Unicode paths on English runners.
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$text,[Text.Encoding]::Unicode)
Write-Host "Installer payload recorded: $($files.Count) files."
