param([string]$PublishDirectory="$PSScriptRoot/../artifacts/publish/win-x64",[string]$Version='0.3.1')
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $PublishDirectory).Path
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('sm-metadata-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture|Out-Null
$names=@('BUILD-INFO.json','SBOM.spdx.json','ShortcutManager.deps.json')
$original=@{};foreach($name in $names){$original[$name]=(Get-FileHash -LiteralPath (Join-Path $source $name)).Hash}
$cases=0
function Reset-Fixture{foreach($name in $names){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $fixture -Force}}
function Save-Fixture($Build,$Sbom){
    $path=Join-Path $fixture 'SBOM.spdx.json'
    [IO.File]::WriteAllText($path,($Sbom|ConvertTo-Json -Depth 30))
    $Build.SbomSha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $fixture 'BUILD-INFO.json'),($Build|ConvertTo-Json -Depth 8))
}
try{
    & "$PSScriptRoot/verify-build-metadata.ps1" -Directory $source -ExpectedVersion $Version
    $cases++
    $mutations=@(
        @{Change={param($b,$s) $b.DependencySha256='0'*64};Expected='hash mismatch'},
        @{Change={param($b,$s) $s.packages[0].versionInfo='999.0.0'};Expected='published dependency'},
        @{Change={param($b,$s) $s.packages=@($s.packages|Select-Object -Skip 1)};Expected='coverage mismatch'},
        @{Change={param($b,$s) $s.relationships=@($s.relationships|Select-Object -Skip 1)};Expected='coverage mismatch'},
        @{Change={param($b,$s) $s.packages[1].checksums[0].checksumValue='bad'};Expected='hash is missing or malformed'},
        @{Change={param($b,$s) $b.SourceDirty=$true};Expected='dirty source';Clean=$true}
    )
    foreach($case in $mutations){
        Reset-Fixture
        $build=Get-Content -LiteralPath (Join-Path $fixture 'BUILD-INFO.json') -Raw|ConvertFrom-Json
        $sbom=Get-Content -LiteralPath (Join-Path $fixture 'SBOM.spdx.json') -Raw|ConvertFrom-Json
        & $case.Change $build $sbom
        Save-Fixture $build $sbom
        $message=$null
        try{& "$PSScriptRoot/verify-build-metadata.ps1" -Directory $fixture -ExpectedVersion $Version -RequireCleanSource:([bool]$case.Clean)|Out-Null}catch{$message=$_.Exception.Message}
        if($message -notmatch $case.Expected){throw "Metadata corruption passed or failed at the wrong boundary: $message"}
        $cases++
    }
    $message=$null
    try{& "$PSScriptRoot/verify-build-metadata.ps1" -Directory $source -ExpectedSourceCommit ('0'*40)|Out-Null}catch{$message=$_.Exception.Message}
    if($message -notmatch 'source commit mismatch'){throw 'Wrong source commit passed the metadata gate.'}
    $cases++
    foreach($name in $names){if((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne $original[$name]){throw 'Metadata tests changed an original.'}}
    Write-Host "Build metadata checks passed ($cases); dependency, package/edge coverage, package hash, clean source and exact commit enforced."
}finally{
    foreach($name in $names){$path=Join-Path $fixture $name;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
    Remove-Item -LiteralPath $fixture
}
