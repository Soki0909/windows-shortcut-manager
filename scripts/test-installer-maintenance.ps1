param([string]$CompilerPath="$PSScriptRoot/../.work/tools/inno/ISCC.exe")
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows'){throw 'Native installer checks are restricted to a disposable GitHub Windows runner.'}
$fixture=Join-Path $env:RUNNER_TEMP ('sm-installer-native-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture|Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'old-file.txt'),'owned old fixture')
Copy-Item -LiteralPath (Join-Path $fixture 'old-file.txt') -Destination (Join-Path $fixture 'outside-external.txt')
for($case=0;$case -le 11;$case++){
    $base=Join-Path $fixture "case-$case";$old=Join-Path $base 'old';$next=Join-Path $base 'next';$app=Join-Path $base 'app'
    New-Item -ItemType Directory -Path $old,$next,$app|Out-Null
    [IO.File]::WriteAllText((Join-Path $old 'current.txt'),'current payload')
    [IO.File]::WriteAllText((Join-Path $next 'current.txt'),'current payload')
    $extra=switch($case){0 {'旧ファイル🧪.txt'} 1 {'modified.txt'} 4 {'linked/old.txt'} 7 {'legacy-extra.txt'} 10 {'locked.txt'} default {'old.txt'}}
    $extraPath=Join-Path $old $extra;New-Item -ItemType Directory -Path (Split-Path $extraPath) -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $fixture 'old-file.txt') -Destination $extraPath
    Copy-Item -Path "$old/*" -Destination $app -Recurse
    $receipt=Join-Path $app '.shortcut-manager-payload.ini'
    & "$PSScriptRoot/write-installer-payload.ps1" -PublishDirectory $old -OutputPath $receipt -Version 0.3.1
    $text=[IO.File]::ReadAllText($receipt).Replace('[Payload]',"[Payload]`r`nInstallDirectory=$app")
    [IO.File]::WriteAllText($receipt,$text,[Text.Encoding]::Unicode)
    if($case -eq 11){New-Item -ItemType Directory -Path (Join-Path $next 'linked')|Out-Null;[IO.File]::WriteAllText((Join-Path $next 'linked/current.txt'),'new destination fixture')}
    & "$PSScriptRoot/write-installer-payload.ps1" -PublishDirectory $next -OutputPath (Join-Path $base 'next.ini') -Version 0.3.1
    switch($case){
        1 {[IO.File]::WriteAllText((Join-Path $app $extra),'user edited obsolete file')}
        2 {[IO.File]::WriteAllText((Join-Path $app 'user-added.txt'),'unmanaged user file')}
        3 {$text=$text.Replace('Path=old.txt','Path=..\..\outside-external.txt');[IO.File]::WriteAllText($receipt,$text,[Text.Encoding]::Unicode)}
        4 {$link=Join-Path $app 'linked';Remove-Item -LiteralPath (Join-Path $link 'old.txt');Remove-Item -LiteralPath $link;New-Item -ItemType Junction -Path $link -Target $old|Out-Null;Copy-Item -LiteralPath (Join-Path $fixture 'old-file.txt') -Destination (Join-Path $old 'old.txt')}
        5 {$redirect=Join-Path $base 'redirected-app';Move-Item -LiteralPath $app -Destination $redirect;New-Item -ItemType Junction -Path $app -Target $redirect|Out-Null}
        7 {Remove-Item -LiteralPath $receipt}
        8 {[IO.File]::WriteAllText($receipt,('x'*1048577))}
        9 {$text=$text.Replace('Path=old.txt','Path=CURRENT.TXT');[IO.File]::WriteAllText($receipt,$text,[Text.Encoding]::Unicode)}
        11 {New-Item -ItemType Junction -Path (Join-Path $app 'linked') -Target $old|Out-Null}
    }
}
$protected=@{}
for($case=0;$case -le 11;$case++){
    $app=Join-Path $fixture "case-$case/app"
    $paths=@((Join-Path $app 'current.txt'))
    $keep=switch($case){1 {'modified.txt'} 2 {'user-added.txt'} 3 {'old.txt'} 4 {'linked/old.txt'} 5 {'old.txt'} 6 {'old.txt'} 7 {'legacy-extra.txt'} 8 {'old.txt'} 9 {'old.txt'} 10 {'locked.txt'} 11 {'old.txt'}}
    if($keep){$paths+=Join-Path $app $keep}
    if($case -in 3,5,8,9){$paths+=Join-Path $app '.shortcut-manager-payload.ini'}
    foreach($path in $paths){$protected[$path]=(Get-FileHash -LiteralPath $path).Hash}
}
$log=Join-Path $fixture 'compile.log'
& $CompilerPath "/DPayloadTestsRoot=$fixture" "/O$fixture" "$PSScriptRoot/../tests/Installer.Tests/PayloadMaintenance.iss" *> $log
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $log -Tail 20;throw 'Native payload test harness did not compile.'}
$locked=[IO.File]::Open((Join-Path $fixture 'case-10/app/locked.txt'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try{
    $process=Start-Process -FilePath (Join-Path $fixture 'payload-native-tests.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$fixture/run.log`"") -WindowStyle Hidden -Wait -PassThru
    if(-not (Test-Path -LiteralPath (Join-Path $fixture 'result.json'))){
        if(Test-Path -LiteralPath (Join-Path $fixture 'failure.txt')){Get-Content -LiteralPath (Join-Path $fixture 'failure.txt')}
        Get-Content -LiteralPath (Join-Path $fixture 'run.log') -Tail 14
        throw "Native payload checks failed (Setup exit $($process.ExitCode))."
    }
    $result=Get-Content -LiteralPath (Join-Path $fixture 'result.json') -Raw|ConvertFrom-Json
    if(-not $result.ok -or $result.cases -ne 12){throw 'Native payload test evidence is incomplete.'}
    foreach($path in $protected.Keys){if(-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $protected[$path]){throw 'Native payload checks changed protected file bytes.'}}
    Write-Host "Native installer payload checks passed (12), $($protected.Count) retained file hashes unchanged. Harness exited before installation; fixture only."
}finally{$locked.Dispose()}
