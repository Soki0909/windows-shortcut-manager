param(
    [Parameter(Mandatory)][string]$AllowedRoot,
    [Parameter(Mandatory)][ValidatePattern('^ShortcutManager-Setup-win-x64-v\d+\.\d+\.\d+\.exe$')][string]$SetupName,
    [Parameter(Mandatory)][string]$Path
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$file = [IO.Path]::GetFullPath($Path)
$name = [IO.Path]::GetFileName($file)
if ([IO.Path]::GetDirectoryName($file) -ne $root -or $name -notin @($SetupName,'uninst.e32.tmp','uninst.e64.tmp')) {
    throw 'Inno signing callback rejected an unexpected output path.'
}
for ($directory = Get-Item -LiteralPath $root; $null -ne $directory; $directory = $directory.Parent) {
    if ($directory.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw 'Inno signing callback rejects reparse ancestors.'
    }
}
& "$PSScriptRoot/sign-files.ps1" -Path $file -AllowedRoot $root -Mode Certificate -RequireSigned -InnoTemporaryImage | Out-Null
