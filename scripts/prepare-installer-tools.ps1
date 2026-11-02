param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
$tools = Join-Path $root '.work\tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
$policy=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'build-tools.json') -Raw|ConvertFrom-Json
$inno=$policy.InnoSetup
if($inno.Version -notmatch '^\d+\.\d+\.\d+$' -or $inno.ReleaseTag -ne ('is-'+$inno.Version.Replace('.','_')) -or $inno.Sha256 -notmatch '^[a-f0-9]{64}$' -or $inno.InstallerFile -ne "innosetup-$($inno.Version)-x64.exe" -or $inno.DownloadUrl -ne "https://github.com/jrsoftware/issrc/releases/download/$($inno.ReleaseTag)/$($inno.InstallerFile)"){throw 'Invalid pinned Inno Setup build policy.'}
$download = Join-Path $tools $inno.InstallerFile
Invoke-WebRequest -Uri $inno.DownloadUrl -OutFile $download
$expected = $inno.Sha256
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Inno Setup download checksum mismatch.' }
if ((Get-AuthenticodeSignature -LiteralPath $download).Status -ne 'Valid') { throw 'Inno Setup signature is not valid.' }
$destination = Join-Path $tools 'inno'
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',"/DIR=`"$destination`"") -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Inno Setup tool installation failed: $($process.ExitCode)" }
