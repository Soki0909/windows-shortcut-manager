param([Parameter(Mandatory)][string]$Version,[Parameter(Mandatory)][string]$PublishDirectory,[switch]$RequireCleanSource)
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$true
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid metadata version.'}
$root=(Resolve-Path "$PSScriptRoot/..").Path
$publish=(Resolve-Path -LiteralPath $PublishDirectory).Path
$source=(git -C $root rev-parse HEAD).Trim()
if($source -notmatch '^[a-f0-9]{40}$'){throw 'Cannot identify the source commit.'}
$dirty=@(git -C $root status --porcelain --untracked-files=normal).Count -gt 0
if($RequireCleanSource -and $dirty){throw 'Release metadata requires a clean source checkout.'}
$sdk=(& dotnet --version).Trim()
$declaredSdk=(Get-Content -LiteralPath (Join-Path $root 'global.json') -Raw|ConvertFrom-Json).sdk.version
if($sdk -ne $declaredSdk){throw 'Selected SDK differs from global.json.'}
$depsPath=Join-Path $publish 'ShortcutManager.deps.json'
$deps=Get-Content -LiteralPath $depsPath -Raw|ConvertFrom-Json
$assets=Get-Content -LiteralPath (Join-Path $root 'src/ShortcutManager.App/obj/project.assets.json') -Raw|ConvertFrom-Json
$packages=@();$relationships=@();$packageIds=@{};$counter=0
foreach($library in $deps.libraries.PSObject.Properties){
    $split=$library.Name.LastIndexOf('/');if($split -lt 1){throw 'Invalid published dependency identity.'}
    $name=$library.Name.Substring(0,$split);$packageVersion=$library.Name.Substring($split+1)
    $kind=$library.Value.type
    $id='SPDXRef-Package-'+(++$counter);$packageIds[$library.Name]=$id
    $package=[ordered]@{SPDXID=$id;name=$name;versionInfo=$packageVersion;downloadLocation='NOASSERTION';filesAnalyzed=$false;licenseConcluded='NOASSERTION';licenseDeclared='NOASSERTION';copyrightText='NOASSERTION'}
    if($kind -eq 'project'){
        if($name -notin 'ShortcutManager','ShortcutManager.Core','ShortcutManager.Windows' -or $packageVersion -ne $Version){throw 'Review ownership/version of the published project dependency.'}
        $package.licenseDeclared='MIT';$package.downloadLocation="https://github.com/Soki0909/windows-shortcut-manager/tree/$source"
    }elseif($kind -in 'runtimepack','package'){
        $nugetName=if($kind -eq 'runtimepack'){$name -replace '^runtimepack\.',''}else{$name}
        $package.name=$nugetName
        $packageDirectory=$null
        foreach($folder in $assets.packageFolders.PSObject.Properties.Name){
            $candidate=Join-Path $folder ("$nugetName/$packageVersion".ToLowerInvariant())
            if(Test-Path -LiteralPath $candidate -PathType Container){$packageDirectory=$candidate;break}
        }
        if(-not $packageDirectory){throw 'Published package is missing from the resolved package cache.'}
        $hashPath=Join-Path $packageDirectory ($nugetName.ToLowerInvariant()+'.'+$packageVersion+'.nupkg.sha512')
        $hashBytes=[Convert]::FromBase64String(([IO.File]::ReadAllText($hashPath)).Trim())
        if($hashBytes.Length -ne 64){throw 'Invalid resolved package SHA-512.'}
        $package.checksums=@(@{algorithm='SHA512';checksumValue=[Convert]::ToHexString($hashBytes).ToLowerInvariant()})
        $package.downloadLocation="https://api.nuget.org/v3-flatcontainer/$($nugetName.ToLowerInvariant())/$packageVersion/$($nugetName.ToLowerInvariant()).$packageVersion.nupkg"
        $package.externalRefs=@(@{referenceCategory='PACKAGE-MANAGER';referenceType='purl';referenceLocator="pkg:nuget/$nugetName@$packageVersion"})
        $nuspecPath=Join-Path $packageDirectory ($nugetName.ToLowerInvariant()+'.nuspec')
        [xml]$nuspec=[IO.File]::ReadAllText($nuspecPath)
        $license=$nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='license'][@type='expression']")
        if($license){$package.licenseDeclared=$license.InnerText}
        $package.comment='Package license metadata is not a replacement for bundled third-party notices.'
    }else{throw 'Review unsupported published dependency kind before release.'}
    $packages+=$package
}
$appIdentity="ShortcutManager/$Version"
if(-not $packageIds.ContainsKey($appIdentity)){throw 'Application package is missing from published dependencies.'}
$relationships+=@{spdxElementId='SPDXRef-DOCUMENT';relationshipType='DESCRIBES';relatedSpdxElement=$packageIds[$appIdentity]}
$target=$deps.targets.PSObject.Properties|Where-Object Name -eq $deps.runtimeTarget.name
if(-not $target){throw 'Published dependency target is missing.'}
foreach($library in $target.Value.PSObject.Properties){
    foreach($dependency in $library.Value.dependencies.PSObject.Properties){
        $identity=$dependency.Name+'/'+$dependency.Value
        if(-not $packageIds.ContainsKey($identity)){throw 'Dependency edge has no resolved package.'}
        $relationships+=@{spdxElementId=$packageIds[$library.Name];relationshipType='DEPENDS_ON';relatedSpdxElement=$packageIds[$identity]}
    }
}
$created=[DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$sbom=[ordered]@{spdxVersion='SPDX-2.3';dataLicense='CC0-1.0';SPDXID='SPDXRef-DOCUMENT';name="ShortcutManager-win-x64-$Version";documentNamespace="https://github.com/Soki0909/windows-shortcut-manager/sbom/$source/$([guid]::NewGuid().ToString('N'))";creationInfo=@{created=$created;creators=@('Tool: ShortcutManager-write-build-metadata-1')};packages=$packages;relationships=$relationships}
$build=[ordered]@{FormatVersion=1;ProductVersion=$Version;SourceRepository='https://github.com/Soki0909/windows-shortcut-manager';SourceCommit=$source;SourceDirty=$dirty;SdkVersion=$sdk;RuntimeTarget=$deps.runtimeTarget.name;DependencySha256=(Get-FileHash -LiteralPath $depsPath).Hash.ToLowerInvariant();SbomSha256=$null}
[IO.File]::WriteAllText((Join-Path $publish 'SBOM.spdx.json'),($sbom|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
$build.SbomSha256=(Get-FileHash -LiteralPath (Join-Path $publish 'SBOM.spdx.json')).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $publish 'BUILD-INFO.json'),($build|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Host "Build metadata written: $($packages.Count) published packages; SDK $sdk; source dirty=$dirty."
