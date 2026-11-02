param()
$ErrorActionPreference='Stop'
$scriptPath=Join-Path $PSScriptRoot 'sign-files.ps1'
$fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('shortcut-signing-'+[guid]::NewGuid().ToString('N'))
$allowedRoot=Join-Path $fixtureRoot 'allowed'
$outsideRoot=Join-Path $fixtureRoot 'outside'
New-Item -ItemType Directory -Path $allowedRoot,$outsideRoot|Out-Null
$file=Join-Path $allowedRoot 'fixture.exe'
$outside=Join-Path $outsideRoot 'fixture.exe'
Copy-Item -LiteralPath (Join-Path ([Environment]::SystemDirectory) 'cmd.exe') -Destination $file
Copy-Item -LiteralPath $file -Destination $outside
$before=(Get-FileHash -LiteralPath $file).Hash
$outsideBefore=(Get-FileHash -LiteralPath $outside).Hash
$cases=0
function Assert-Blocked([hashtable]$Parameters){
    $failed=$false
    try {& $scriptPath @Parameters|Out-Null}catch{$failed=$true}
    if(-not $failed){throw 'Expected signing precondition to stop the operation.'}
    if((Get-FileHash -LiteralPath $file).Hash -ne $before -or (Get-FileHash -LiteralPath $outside).Hash -ne $outsideBefore){throw 'Rejected signing changed an input.'}
    $script:cases++
}
try {
    $result=@(& $scriptPath -Path @($file) -AllowedRoot $allowedRoot -Mode Unsigned)
    if($result.Count -ne 1 -or $result[0].Name -ne 'fixture.exe' -or $result[0].Sha256 -ne $before.ToLowerInvariant() -or (Get-FileHash -LiteralPath $file).Hash -ne $before){throw 'Unsigned inspection changed the file or reported wrong metadata.'}
    $cases++
    Assert-Blocked @{Path=@($file);AllowedRoot=$allowedRoot;RequireSigned=$true}
    Assert-Blocked @{Path=@($outside);AllowedRoot=$allowedRoot}
    Assert-Blocked @{Path=@(Join-Path $allowedRoot '../outside/fixture.exe');AllowedRoot=$allowedRoot}
    Assert-Blocked @{Path=@(Join-Path $allowedRoot 'missing.exe');AllowedRoot=$allowedRoot}
    $text=Join-Path $allowedRoot 'not-an-executable.txt';Set-Content -LiteralPath $text -Value 'fixture'
    Assert-Blocked @{Path=@($text);AllowedRoot=$allowedRoot}
    Assert-Blocked @{Path=@($file);AllowedRoot=$allowedRoot;Mode='Certificate';CertificateThumbprint='';TimestampServer='https://timestamp.invalid'}
    Assert-Blocked @{Path=@($file);AllowedRoot=$allowedRoot;Mode='Certificate';CertificateThumbprint=('0'*40);TimestampServer=''}
    Assert-Blocked @{Path=@($file);AllowedRoot=$allowedRoot;Mode='Certificate';CertificateThumbprint=('0'*40);TimestampServer='file:///fixture'}
    Assert-Blocked @{Path=@($file);AllowedRoot=$allowedRoot;Mode='Certificate';CertificateThumbprint=('0'*40);TimestampServer='https://timestamp.invalid'}
    $link=Join-Path $allowedRoot 'linked';New-Item -ItemType Junction -Path $link -Target $outsideRoot|Out-Null
    Assert-Blocked @{Path=@(Join-Path $link 'fixture.exe');AllowedRoot=$allowedRoot}
    Write-Host "Signing boundary checks passed ($cases). No certificate generated/imported, no timestamp request or signing performed."
}finally{
    $resolved=[IO.Path]::GetFullPath($fixtureRoot)
    $tempPrefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
