param(
    [Parameter(Mandatory)][string[]]$Path,
    [Parameter(Mandatory)][string]$AllowedRoot,
    [ValidateSet('Unsigned','Certificate')][string]$Mode='Unsigned',
    [string]$CertificateThumbprint=$env:CODE_SIGN_THUMBPRINT,
    [string]$TimestampServer=$env:CODE_SIGN_TIMESTAMP_URL,
    [string]$SignToolPath=$env:CODE_SIGN_TOOL_PATH,
    [ValidateSet('CurrentUser','LocalMachine')][string]$CertificateStore='CurrentUser',
    [switch]$RequireSigned
)
$ErrorActionPreference='Stop'
if($RequireSigned -and $Mode -ne 'Certificate'){throw 'A signed release requires Certificate mode; unsigned fallback is forbidden.'}
$root=[IO.Path]::GetFullPath($AllowedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if(-not (Test-Path -LiteralPath $root -PathType Container)){throw 'Signing root does not exist.'}
$files=@(foreach($entry in $Path){
    $file=[IO.Path]::GetFullPath($entry)
    if(-not $file.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Signing target is outside the artifact root.'}
    if([IO.Path]::GetExtension($file) -notin '.exe','.dll' -or -not (Test-Path -LiteralPath $file -PathType Leaf)){throw 'Signing requires an existing EXE or DLL.'}
    for($item=Get-Item -LiteralPath $file;$null -ne $item;){
        if($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){throw 'Signing does not follow reparse points.'}
        if($item.FullName.TrimEnd([IO.Path]::DirectorySeparatorChar) -eq $root){break}
        $item=if($item -is [IO.DirectoryInfo]){$item.Parent}else{$item.Directory}
    }
    $file
})
if($Mode -eq 'Certificate'){
    if($CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$'){throw 'Specify the exact code-signing certificate thumbprint. No private key/password belongs in source.'}
    $timestampUri=$null
    if(-not [Uri]::TryCreate($TimestampServer,[UriKind]::Absolute,[ref]$timestampUri) -or $timestampUri.Scheme -notin 'http','https'){throw 'An RFC3161 timestamp service URL is required.'}
    $certificate=Get-Item -LiteralPath "Cert:\$CertificateStore\My\$CertificateThumbprint"
    $now=[DateTime]::UtcNow
    if(-not $certificate.HasPrivateKey -or $certificate.NotBefore.ToUniversalTime() -gt $now -or $certificate.NotAfter.ToUniversalTime() -le $now){throw 'Signing certificate has no usable private key or is outside its validity period.'}
    $eku=@($certificate.Extensions|Where-Object {$_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]})
    if(-not @($eku.EnhancedKeyUsages|Where-Object Value -eq '1.3.6.1.5.5.7.3.3').Count){throw 'Certificate must explicitly allow code signing.'}
    if(-not $SignToolPath){
        $command=Get-Command signtool.exe -ErrorAction SilentlyContinue
        if($command){$SignToolPath=$command.Source}
        else{
            $sdk=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
            if(Test-Path -LiteralPath $sdk){$SignToolPath=Get-ChildItem -LiteralPath $sdk -Directory|Where-Object Name -match '^\d+\.\d+\.\d+\.\d+$'|Sort-Object {[version]$_.Name} -Descending|ForEach-Object {Join-Path $_.FullName 'x64/signtool.exe'}|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1}
        }
    }
    if(-not $SignToolPath -or (Get-AuthenticodeSignature -LiteralPath $SignToolPath).Status -ne 'Valid'){throw 'A valid signed Windows SDK SignTool is required.'}
    foreach($file in $files){
        $arguments=@('sign','/fd','SHA256','/tr',$TimestampServer,'/td','SHA256','/sha1',$CertificateThumbprint,'/s','My')
        if($CertificateStore -eq 'LocalMachine'){$arguments+='/sm'}
        & $SignToolPath @arguments $file|Out-Host
        if($LASTEXITCODE -ne 0){throw 'SignTool signing failed; do not publish or reuse stale hashes.'}
        & $SignToolPath verify /pa /all /tw $file|Out-Host
        if($LASTEXITCODE -ne 0){throw 'SignTool Authenticode/timestamp verification failed.'}
        $signature=Get-AuthenticodeSignature -LiteralPath $file
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint -or $null -eq $signature.TimeStamperCertificate){throw 'Signed file must have the selected publisher, a trusted signature and a timestamp.'}
    }
}
foreach($file in $files){
    $signature=Get-AuthenticodeSignature -LiteralPath $file
    if($signature.Status -notin 'NotSigned','Valid'){throw 'Invalid Authenticode signature in an artifact.'}
    [pscustomobject]@{Name=[IO.Path]::GetFileName($file);Status=$signature.Status.ToString();SignerThumbprint=$signature.SignerCertificate.Thumbprint;Timestamped=($null -ne $signature.TimeStamperCertificate);Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}
}
