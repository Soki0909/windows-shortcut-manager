param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
$tools = Join-Path $root '.work\tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
$download = Join-Path $tools 'innosetup-6.7.3.exe'
Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
$expected = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Inno Setup download checksum mismatch.' }
if ((Get-AuthenticodeSignature -LiteralPath $download).Status -ne 'Valid') { throw 'Inno Setup signature is not valid.' }
$destination = Join-Path $tools 'inno'
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',"/DIR=`"$destination`"") -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Inno Setup tool installation failed: $($process.ExitCode)" }
