param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
& dotnet test "$root\ShortcutManager.sln" -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& node --test "$root\tests\ChromeExtension.Tests\protocol.test.js"
exit $LASTEXITCODE

