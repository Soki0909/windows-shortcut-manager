param([string]$Version='0.3.1',[string]$ArtifactDirectory="$PSScriptRoot/../artifacts",[string]$CompilerPath="$PSScriptRoot/../.work/tools/inno/ISCC.exe")
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$true
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows'){throw 'Installer lifecycle checks are restricted to a disposable GitHub Windows runner.'}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid lifecycle version.'}
$root=(Resolve-Path "$PSScriptRoot/..").Path
$artifacts=(Resolve-Path -LiteralPath $ArtifactDirectory).Path
$setup=Join-Path $artifacts "ShortcutManager-Setup-win-x64-v$Version.exe"
$previous=Get-Content -LiteralPath (Join-Path $root 'tests/Installer.Tests/previous-release.json') -Raw|ConvertFrom-Json
$temp=Join-Path $env:RUNNER_TEMP ('sm-lifecycle-'+[guid]::NewGuid().ToString('N'))
$app=Join-Path $temp 'legacy-app'
$data=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ShortcutManager'
$config=Join-Path $data 'config.json'
$programs=[Environment]::GetFolderPath('Programs')
$managed=Join-Path $programs 'Shortcut Manager'
$unmanaged=Join-Path $programs ('ci-unmanaged-'+[guid]::NewGuid().ToString('N')+'.lnk')
$registration='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B51D02E8-2292-464D-B2CD-1486E5315750}_is1'
if((Test-Path -LiteralPath $config) -or (Test-Path -LiteralPath $managed) -or (Test-Path -LiteralPath $registration)){throw 'Unexpected existing product state on the disposable runner; refusing to replace it.'}
New-Item -ItemType Directory -Path $temp|Out-Null
$oldSetup=Join-Path $temp 'previous-official.exe'
Invoke-WebRequest -Uri $previous.SetupUrl -OutFile $oldSetup
if((Get-FileHash -LiteralPath $oldSetup).Hash -ne $previous.SetupSha256){throw 'Pinned official previous Setup hash mismatch.'}
if((Get-AuthenticodeSignature -LiteralPath $oldSetup).Status -notin 'NotSigned','Valid'){throw 'Previous Setup signature is invalid.'}
if(-not ('ShortcutManager.Windows.ShellLinks.ShellLinkService' -as [type])){Add-Type -Path (Join-Path $root 'src/ShortcutManager.Windows/ShellLinks/ShellLinkService.cs')}
$links=[ShortcutManager.Windows.ShellLinks.ShellLinkService]::new()
$links.Write($unmanaged,[ShortcutManager.Windows.ShellLinks.ShellLinkSpec]::new($env:COMSPEC,'/c exit 0',$temp,'CTRL+ALT+V',"$env:COMSPEC,0",'Unmanaged CI fixture',1))
function Install([string]$SetupPath,[string]$Destination,[string]$Name){
    $process=Start-Process -FilePath $SetupPath -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/LANG=english',"/DIR=`"$Destination`"",'/TASKS=',"/LOG=`"$temp/$Name.log`"") -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $temp "$Name.log") -Tail 16;throw "Installer lifecycle $Name exit $($process.ExitCode)."}
}
function Uninstall([string]$Destination,[string]$Name){
    $process=Start-Process -FilePath (Join-Path $Destination 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$temp/$Name.log`"") -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0){throw "Uninstall lifecycle $Name exit $($process.ExitCode)."}
    if((Test-Path -LiteralPath (Join-Path $Destination 'ShortcutManager.exe')) -or (Test-Path -LiteralPath $registration)){throw 'Application/registration survived uninstall.'}
    foreach($name in @('Shortcut Manager.lnk','Shortcut Manager (監査のみ).lnk')){if(Test-Path -LiteralPath (Join-Path $programs $name)){throw 'Installed application launcher survived uninstall.'}}
}
function Command([string]$Destination,[string[]]$Arguments,[string]$Name){
    $result=Join-Path $temp "$Name.json"
    $commandArgs=@($Arguments)+@('--result',"`"$result`"")
    $process=Start-Process -FilePath (Join-Path $Destination 'ShortcutManager.exe') -ArgumentList $commandArgs -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0){if(Test-Path -LiteralPath $result){Get-Content -LiteralPath $result};throw "Installed CLI $Name failed."}
    Get-Content -LiteralPath $result -Raw|ConvertFrom-Json
}
$protected=@{}
function Assert-Protected{
    foreach($path in $protected.Keys){if(-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $protected[$path]){throw 'Installed update/uninstall changed retained or unmanaged data.'}}
    $managedLinks=@(Get-ChildItem -LiteralPath $managed -Filter '*.lnk')
    if($managedLinks.Count -ne 1 -or $links.Read($managedLinks[0].FullName).Hotkey -ne 'CTRL+ALT+Q' -or $links.Read($unmanaged).Hotkey -ne 'CTRL+ALT+V'){throw 'Persisted managed/unmanaged HotKey changed.'}
}
function Assert-Current([string]$Destination){
    if((Get-Item -LiteralPath (Join-Path $Destination 'ShortcutManager.exe')).VersionInfo.ProductVersion.Split('+')[0] -ne $Version){throw 'Updated executable version mismatch.'}
    $build=Get-Content -LiteralPath (Join-Path $Destination 'BUILD-INFO.json') -Raw|ConvertFrom-Json
    if($build.SourceCommit -ne (git -C $root rev-parse HEAD).Trim() -or $build.SourceDirty){throw 'Updated source identity mismatch.'}
    $key=Get-ItemProperty -LiteralPath $registration
    if($key.DisplayVersion -ne $Version -or -not $key.UninstallString.Contains($Destination,[StringComparison]::OrdinalIgnoreCase)){throw 'Per-user installed application registration mismatch.'}
    $audit=$links.Read((Join-Path $programs 'Shortcut Manager (監査のみ).lnk'))
    if($audit.TargetPath -ne (Join-Path $Destination 'ShortcutManager.exe') -or $audit.Arguments -ne '--audit-only' -or $audit.Hotkey){throw 'Updated audit-only launcher mismatch.'}
    $validation=Command $Destination @('--validate-config',"`"$config`"") ('validate-'+[guid]::NewGuid().ToString('N'))
    if(-not $validation.ok){throw 'Retained configuration cannot be validated by the installed application.'}
    Assert-Protected
}
Install $oldSetup $app 'legacy-install'
if((Get-Item -LiteralPath (Join-Path $app 'ShortcutManager.exe')).VersionInfo.ProductVersion.Split('+')[0] -ne $previous.Version){throw 'Official previous installed version mismatch.'}
$source=Join-Path $temp 'old-config.json'
$settings=[ordered]@{schemaVersion=1;dispatchProtocolVersion=1;settings=@{managedShortcutDirectory=$managed};shortcuts=@([ordered]@{id='88888888-8888-4888-8888-888888888888';name='CI keep website';enabled=$true;hotkey=@{modifiers=@('Ctrl','Alt');key='Q'};target=[ordered]@{kind='website';url='https://example.invalid/'}})}
[IO.File]::WriteAllText($source,($settings|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
$applied=Command $app @('--apply-config',"`"$source`"") 'legacy-apply'
if(-not $applied.succeeded){throw 'Official previous edition could not establish the isolated config/managed HotKey fixture.'}
$ownerConfig=Get-Content -LiteralPath $config -Raw|ConvertFrom-Json
$ownerConfig|Add-Member -NotePropertyName retainedFutureField -NotePropertyValue @{keep=$true}
[IO.File]::WriteAllText($config,($ownerConfig|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
$backup=Join-Path $data 'backups/manual-retain-fixture.zip'
Compress-Archive -LiteralPath $config -DestinationPath $backup
foreach($file in @((Get-Item -LiteralPath $config),(Get-Item -LiteralPath (Join-Path $data 'state.json')))+(Get-ChildItem -LiteralPath $managed -File)+(Get-ChildItem -LiteralPath (Join-Path $data 'backups') -File)){$protected[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash}
$protected[$unmanaged]=(Get-FileHash -LiteralPath $unmanaged).Hash
$note=Join-Path $app 'user-added.txt';[IO.File]::WriteAllText($note,'user-added application-directory fixture');$protected[$note]=(Get-FileHash -LiteralPath $note).Hash
Install $setup $app 'legacy-upgrade';Assert-Current $app
Install $setup $app 'same-version-reinstall';Assert-Current $app
Uninstall $app 'retaining-uninstall';Assert-Protected
Install $setup $app 'reinstall-after-uninstall';Assert-Current $app
Uninstall $app 'retaining-uninstall-again';Assert-Protected
Write-Host 'Official previous install/CLI Apply -> upgrade -> reinstall -> uninstall -> reinstall passed; config/state/backups and managed/unmanaged HotKeys preserved.'

# Real old/new Setup payload composition differs, using test-only files in a disposable source copy.
$lab=Join-Path $temp 'payload-app';$labPublish=Join-Path $temp 'previous-payload';New-Item -ItemType Directory -Path $labPublish|Out-Null
Copy-Item -Path (Join-Path $artifacts 'publish/win-x64/*') -Destination $labPublish -Recurse
foreach($name in @('obsolete-owned.dat','obsolete-edited.dat')){[IO.File]::WriteAllText((Join-Path $labPublish $name),'owned previous fixture payload')}
$labManifest=Join-Path $temp 'previous-payload.ini'
& "$PSScriptRoot/write-installer-payload.ps1" -PublishDirectory $labPublish -OutputPath $labManifest -Version $Version
& $CompilerPath "/DAppVersion=$Version" "/DPublishDir=$labPublish" "/DPayloadManifest=$labManifest" "/O$temp" '/Fprevious-payload-fixture' (Join-Path $root 'installer/ShortcutManager.iss') *> (Join-Path $temp 'fixture-build.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $temp 'fixture-build.log') -Tail 16;throw 'Previous payload Setup fixture did not compile.'}
Install (Join-Path $temp 'previous-payload-fixture.exe') $lab 'previous-payload-install'
$edited=Join-Path $lab 'obsolete-edited.dat';[IO.File]::WriteAllText($edited,'user edited previous payload');$editedHash=(Get-FileHash -LiteralPath $edited).Hash
$userFile=Join-Path $lab 'user-added.txt';[IO.File]::WriteAllText($userFile,'unmanaged additional file');$userHash=(Get-FileHash -LiteralPath $userFile).Hash
Install $setup $lab 'changed-payload-upgrade';Assert-Current $lab
if(Test-Path -LiteralPath (Join-Path $lab 'obsolete-owned.dat')){throw 'Unchanged owned obsolete payload was not retired.'}
if((Get-FileHash -LiteralPath $edited).Hash -ne $editedHash -or (Get-FileHash -LiteralPath $userFile).Hash -ne $userHash){throw 'Changed-payload upgrade removed edited or unmanaged files.'}
$retired=@(Get-ChildItem -LiteralPath $lab -Directory -Filter '.shortcut-manager-retired-*'|ForEach-Object {Join-Path $_.FullName 'files/obsolete-owned.dat'}|Where-Object {Test-Path -LiteralPath $_})
if($retired.Count -ne 1 -or (Get-FileHash -LiteralPath $retired[0]).Hash -ne (Get-FileHash -LiteralPath (Join-Path $labPublish 'obsolete-owned.dat')).Hash){throw 'Retired payload backup missing or different.'}
Uninstall $lab 'changed-payload-uninstall';Assert-Protected
if((Get-FileHash -LiteralPath $userFile).Hash -ne $userHash -or -not (Test-Path -LiteralPath $retired[0])){throw 'Uninstall removed unmanaged additions or retired backup.'}
Write-Host 'Changed-payload Setup upgrade passed: owned obsolete file backed up/retired; edited obsolete and unmanaged files preserved during upgrade; backups/unmanaged files retained at uninstall.'
