param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnet restore "$PSScriptRoot\..\ShortcutManager.sln"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet build "$PSScriptRoot\..\ShortcutManager.sln" -c $Configuration --no-restore --nologo
exit $LASTEXITCODE

