param([string]$Version='0.3.1',[string]$ArtifactDirectory="$PSScriptRoot/../artifacts")
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $ArtifactDirectory).Path
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('sm-release-signing-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture|Out-Null
$names=@("ShortcutManager-win-x64-v$Version.zip","ShortcutManager-TabBridge-v$Version.zip","ShortcutManager-Setup-win-x64-v$Version.exe")
$original=@{}
foreach($name in $names){$original[$name]=(Get-FileHash -LiteralPath (Join-Path $source $name)).Hash}
$cases=0
try {
    foreach($name in $names){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $fixture}
    $windows=Join-Path $fixture $names[0]
    $mutations=@(
        @{ Change={param($record) $record.Files[0].Sha256='0'*64}; Expected='post-signing hash' },
        @{ Change={param($record) $record.Files[0].Status='Valid'}; Expected='differs from the signing record' },
        @{ Change={param($record) $record.Files[1].Name=$record.Files[0].Name}; Expected='duplicate signing record' }
    )
    foreach($case in $mutations){
        Copy-Item -LiteralPath (Join-Path $source $names[0]) -Destination $windows -Force
        $zip=[IO.Compression.ZipFile]::Open($windows,[IO.Compression.ZipArchiveMode]::Update)
        try {
            $entry=$zip.GetEntry('SIGNING.json');if(-not $entry){throw 'Build current signing-aware artifacts before running this test.'}
            $reader=[IO.StreamReader]::new($entry.Open());try{$record=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
            & $case.Change $record
            $entry.Delete();$entry=$zip.CreateEntry('SIGNING.json')
            $writer=[IO.StreamWriter]::new($entry.Open());try{$writer.Write(($record|ConvertTo-Json -Depth 8))}finally{$writer.Dispose()}
        }finally{$zip.Dispose()}
        $checksums=@(foreach($name in $names){"$((Get-FileHash -LiteralPath (Join-Path $fixture $name)).Hash.ToLowerInvariant())  $name"})
        [IO.File]::WriteAllLines((Join-Path $fixture 'SHA256SUMS.txt'),[string[]]$checksums)
        $message=$null
        try{& "$PSScriptRoot/verify-release-artifacts.ps1" -Version $Version -ArtifactDirectory $fixture|Out-Null}catch{$message=$_.Exception.Message}
        if(-not $message -or $message -notmatch $case.Expected){throw "Signing metadata tamper was not rejected at the expected boundary: $message"}
        $cases++
    }
    $message=$null
    try{& "$PSScriptRoot/verify-release-artifacts.ps1" -Version $Version -ArtifactDirectory $source -RequireSigned -ExpectedSignerThumbprint ('0'*40)|Out-Null}catch{$message=$_.Exception.Message}
    if($message -notmatch 'approved publisher'){throw 'Unsigned artifacts passed the signed-release gate, or failed at an unrelated boundary.'}
    $cases++
    foreach($name in $names){if((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne $original[$name]){throw 'Artifact signing tests changed an original artifact.'}}
    Write-Host "Release signing tamper/fallback checks passed ($cases); original artifacts unchanged. No installer executed."
}finally{
    $resolved=[IO.Path]::GetFullPath($fixture)
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
