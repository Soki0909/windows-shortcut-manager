param()
$ErrorActionPreference='Stop'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('sm-payload-test-'+[guid]::NewGuid().ToString('N'))
$source=Join-Path $fixture 'publish';$outside=Join-Path $fixture 'outside';$output=Join-Path $fixture 'manifest.ini'
New-Item -ItemType Directory -Path $source,$outside,(Join-Path $source '日本語')|Out-Null
$cases=0
function Assert-Blocked([hashtable]$Arguments){
    $before=if(Test-Path -LiteralPath $output){(Get-FileHash -LiteralPath $output).Hash}else{$null}
    $failed=$false;try{& "$PSScriptRoot/write-installer-payload.ps1" @Arguments|Out-Null}catch{$failed=$true}
    if(-not $failed){throw 'Expected payload boundary rejection.'}
    if($before -and (Get-FileHash -LiteralPath $output).Hash -ne $before){throw 'Rejected payload generation changed an existing output.'}
    $script:cases++
}
try{
    [IO.File]::WriteAllText((Join-Path $source 'app.exe'),'test-only file')
    [IO.File]::WriteAllText((Join-Path $source '日本語/補助🧪.txt'),'unicode fixture')
    & "$PSScriptRoot/write-installer-payload.ps1" -PublishDirectory $source -OutputPath $output -Version 0.3.1
    $bytes=[IO.File]::ReadAllBytes($output);$text=[IO.File]::ReadAllText($output)
    if($bytes[0] -ne 255 -or $bytes[1] -ne 254 -or $text -notmatch 'Count=2' -or -not $text.Contains('日本語\補助🧪.txt') -or $text.Contains($source)){throw 'Payload record is not Unicode, relative and complete.'}
    foreach($file in Get-ChildItem -LiteralPath $source -File -Recurse){if(-not $text.Contains((Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant())){throw 'Missing actual payload hash.'}}
    $cases++
    Assert-Blocked @{PublishDirectory=$source;OutputPath=$output;Version='invalid'}
    $reserved=Join-Path $source '.shortcut-manager-payload.ini';[IO.File]::WriteAllText($reserved,'unknown')
    Assert-Blocked @{PublishDirectory=$source;OutputPath=$output;Version='0.3.1'};Remove-Item -LiteralPath $reserved
    $reserved=Join-Path $source 'unins000.exe';[IO.File]::WriteAllText($reserved,'unknown')
    Assert-Blocked @{PublishDirectory=$source;OutputPath=$output;Version='0.3.1'};Remove-Item -LiteralPath $reserved
    Assert-Blocked @{PublishDirectory=$outside;OutputPath=$output;Version='0.3.1'}
    [IO.File]::WriteAllText((Join-Path $outside 'external.txt'),'unmanaged')
    $link=Join-Path $source 'linked';New-Item -ItemType Junction -Path $link -Target $outside|Out-Null
    Assert-Blocked @{PublishDirectory=$source;OutputPath=$output;Version='0.3.1'}
    Remove-Item -LiteralPath $link
    $link=Join-Path $fixture 'ancestor';New-Item -ItemType Junction -Path $link -Target $source|Out-Null
    Assert-Blocked @{PublishDirectory=(Join-Path $link '日本語');OutputPath=$output;Version='0.3.1'}
    if([IO.File]::ReadAllText((Join-Path $outside 'external.txt')) -ne 'unmanaged'){throw 'External payload fixture changed.'}
    Write-Host "Installer payload generator checks passed ($cases); original output and external files preserved on rejection."
}finally{
    $resolved=[IO.Path]::GetFullPath($fixture);$prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
