param([Parameter(Mandatory)][string]$Directory,[string]$ExpectedVersion,[string]$ExpectedSourceCommit,[switch]$RequireCleanSource)
$ErrorActionPreference='Stop'
$metadataPath=Join-Path $Directory 'BUILD-INFO.json';$sbomPath=Join-Path $Directory 'SBOM.spdx.json';$depsPath=Join-Path $Directory 'ShortcutManager.deps.json'
foreach($path in @($metadataPath,$sbomPath,$depsPath)){if((Get-Item -LiteralPath $path).Length -gt 1MB){throw 'Build metadata exceeds size limit.'}}
$build=Get-Content -LiteralPath $metadataPath -Raw|ConvertFrom-Json
$sbom=Get-Content -LiteralPath $sbomPath -Raw|ConvertFrom-Json
$deps=Get-Content -LiteralPath $depsPath -Raw|ConvertFrom-Json
$fields=@('FormatVersion','ProductVersion','SourceRepository','SourceCommit','SourceDirty','SdkVersion','RuntimeTarget','DependencySha256','SbomSha256')
if(@($build.PSObject.Properties.Name).Count -ne $fields.Count -or @($build.PSObject.Properties.Name|Where-Object {$_ -notin $fields}).Count){throw 'Unexpected build metadata fields.'}
if($build.FormatVersion -ne 1 -or $build.SourceRepository -ne 'https://github.com/Soki0909/windows-shortcut-manager' -or $build.SourceCommit -notmatch '^[a-f0-9]{40}$' -or $build.SourceDirty -isnot [bool] -or $build.SdkVersion -notmatch '^\d+\.\d+\.\d+$' -or $build.RuntimeTarget -ne $deps.runtimeTarget.name){throw 'Invalid build identity.'}
if($ExpectedVersion -and $build.ProductVersion -ne $ExpectedVersion){throw 'Build metadata product version mismatch.'}
if($ExpectedSourceCommit -and $build.SourceCommit -ne $ExpectedSourceCommit){throw 'Build metadata source commit mismatch.'}
if($RequireCleanSource -and $build.SourceDirty){throw 'Build metadata identifies a dirty source checkout.'}
if($build.DependencySha256 -ne (Get-FileHash -LiteralPath $depsPath).Hash -or $build.SbomSha256 -ne (Get-FileHash -LiteralPath $sbomPath).Hash){throw 'Build metadata hash mismatch.'}
if($sbom.spdxVersion -ne 'SPDX-2.3' -or $sbom.dataLicense -ne 'CC0-1.0' -or $sbom.SPDXID -ne 'SPDXRef-DOCUMENT' -or @($sbom.packages).Count -ne @($deps.libraries.PSObject.Properties).Count){throw 'SBOM document/package coverage mismatch.'}
$ids=@{};$identities=@{}
foreach($package in $sbom.packages){
    if($package.SPDXID -notmatch '^SPDXRef-Package-\d+$' -or $ids.ContainsKey($package.SPDXID) -or $package.filesAnalyzed -ne $false -or -not $package.licenseDeclared){throw 'Invalid or duplicate SBOM package.'}
    $identity=$package.name+'/'+$package.versionInfo
    if($identities.ContainsKey($identity)){throw 'Duplicate SBOM package identity.'}
    $library=$deps.libraries.PSObject.Properties|Where-Object {($_.Name -replace '^runtimepack\.','') -eq $identity}
    if(@($library).Count -ne 1){throw 'SBOM package differs from the published dependency.'}
    if($library.Value.type -in 'package','runtimepack'){
        if(@($package.checksums).Count -ne 1 -or $package.checksums[0].algorithm -ne 'SHA512' -or $package.checksums[0].checksumValue -notmatch '^[a-f0-9]{128}$'){throw 'Resolved NuGet package hash is missing or malformed.'}
    }
    $ids[$package.SPDXID]=$identity;$identities[$identity]=$package.SPDXID
}
$expectedEdges=@{}
$appId=$identities['ShortcutManager/'+$build.ProductVersion]
if(-not $appId){throw 'SBOM application identity missing.'}
$expectedEdges['SPDXRef-DOCUMENT|DESCRIBES|'+$appId]=$true
$target=$deps.targets.PSObject.Properties|Where-Object Name -eq $deps.runtimeTarget.name
if(-not $target){throw 'Published target missing from dependency metadata.'}
foreach($library in $target.Value.PSObject.Properties){
    $from=$identities[($library.Name -replace '^runtimepack\.','')]
    foreach($dependency in $library.Value.dependencies.PSObject.Properties){
        $to=$identities[(($dependency.Name -replace '^runtimepack\.','')+'/'+$dependency.Value)]
        if(-not $from -or -not $to){throw 'Unresolved SBOM dependency edge.'}
        $expectedEdges["$from|DEPENDS_ON|$to"]=$true
    }
}
$seen=@{}
foreach($edge in $sbom.relationships){
    $key=$edge.spdxElementId+'|'+$edge.relationshipType+'|'+$edge.relatedSpdxElement
    if(-not $expectedEdges.ContainsKey($key) -or $seen.ContainsKey($key)){throw 'Unexpected or duplicate SBOM relationship.'}
    $seen[$key]=$true
}
if($seen.Count -ne $expectedEdges.Count){throw 'SBOM dependency relationship coverage mismatch.'}
Write-Host "Build metadata verified: $($ids.Count) published packages, source $($build.SourceCommit)."
