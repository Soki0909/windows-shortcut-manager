param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_EVENT_NAME -ne 'workflow_dispatch' -or
    $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or -not $env:RUNNER_TEMP) {
    throw 'Installer capture is restricted to a disposable GitHub Windows runner.'
}

$release = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rollback-release.json') -Raw | ConvertFrom-Json).Source
$fixture = Join-Path $env:RUNNER_TEMP ('sm-capture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture, $OutputDirectory | Out-Null
$setup = Join-Path $fixture 'ShortcutManager-Setup.exe'
Invoke-WebRequest -Uri $release.SetupUrl -OutFile $setup
if ((Get-FileHash -LiteralPath $setup).Hash -ne $release.SetupSha256) { throw 'Published Setup hash mismatch.' }
gh attestation verify $setup --repo $env:GITHUB_REPOSITORY --signer-workflow "$env:GITHUB_REPOSITORY/.github/workflows/release.yml" --source-digest $release.TagCommit --source-ref "refs/tags/v$($release.Version)" --deny-self-hosted-runners

$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B51D02E8-2292-464D-B2CD-1486E5315750}_is1'
if (Test-Path -LiteralPath $registry) { throw 'Unexpected previous Setup state.' }

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class SetupCapture {
    private delegate bool EnumProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out int process);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder title, int count);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect bounds);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr context, uint flags);

    public static IntPtr FindWizard(int[] ownedProcesses) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) => {
            GetWindowThreadProcessId(window, out int process);
            if (Array.IndexOf(ownedProcesses, process) < 0 || !IsWindowVisible(window)) return true;
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "TWizardForm") return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
'@

function Get-SetupProcesses {
    Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($fixture + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) }
}

$start = [Diagnostics.ProcessStartInfo]::new($setup)
$start.UseShellExecute = $false
$start.ArgumentList.Add('/CURRENTUSER')
$start.ArgumentList.Add('/LANG=japanese')
$start.Environment['TEMP'] = $fixture
$start.Environment['TMP'] = $fixture
$process = [Diagnostics.Process]::Start($start)
try {
    $timeout = [Diagnostics.Stopwatch]::StartNew()
    $window = [IntPtr]::Zero
    while ($window -eq [IntPtr]::Zero -and $timeout.Elapsed.TotalSeconds -lt 30) {
        $owned = @(Get-SetupProcesses)
        $window = [SetupCapture]::FindWizard([int[]]@($owned.Id))
        if ($window -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 250 }
    }
    if ($window -eq [IntPtr]::Zero) { throw 'No visible Inno Setup wizard on the runner desktop.' }
    $title = [Text.StringBuilder]::new(200)
    [void][SetupCapture]::GetWindowText($window, $title, $title.Capacity)
    if (-not $title.ToString().Contains('Shortcut Manager', [StringComparison]::Ordinal)) { throw 'Unexpected wizard title.' }
    $bounds = [SetupCapture+Rect]::new()
    if (-not [SetupCapture]::GetWindowRect($window, [ref]$bounds)) { throw 'Wizard bounds are unavailable.' }
    $width = $bounds.Right - $bounds.Left
    $height = $bounds.Bottom - $bounds.Top
    if ($width -notin 300..1400 -or $height -notin 250..1100) { throw "Unexpected wizard size: $width by $height." }
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $drawing = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $context = $drawing.GetHdc()
            try { if (-not [SetupCapture]::PrintWindow($window, $context, 0)) { throw 'Setup window did not render.' } }
            finally { $drawing.ReleaseHdc($context) }
        } finally { $drawing.Dispose() }
        $bitmap.Save((Join-Path $OutputDirectory '00-setup.png'), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Write-Host "Published v$($release.Version) wizard captured at $width by $height. No installer control was activated."
} finally {
    foreach ($ownedProcess in @(Get-SetupProcesses)) {
        if (-not $ownedProcess.HasExited) { $ownedProcess.Kill() }
    }
    $process.Dispose()
}
if (Test-Path -LiteralPath $registry) { throw 'Setup created an unexpected registration.' }
