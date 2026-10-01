param(
  [Parameter(Mandatory=$true)][string]$PathList,
  [Parameter(Mandatory=$true)][string]$BackupDirectory,
  [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class ShortcutManagerIni {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int eventId, uint flags, string item1, System.IntPtr item2);
}
'@

function Notify-ShellItemChanged([string]$Path) {
  [ShortcutManagerIni]::SHChangeNotify(0x00002000, 0x00001005, [IO.Path]::GetFullPath($Path), [IntPtr]::Zero)
}

function Read-HotKey([string]$Path) {
  if ($Path.EndsWith('.lnk', [StringComparison]::OrdinalIgnoreCase)) {
    $shell = New-Object -ComObject WScript.Shell
    try {
      $shortcut = $shell.CreateShortcut($Path)
      try { return [string]$shortcut.Hotkey }
      finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null }
    }
    finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
  }
  $line = Get-Content -LiteralPath $Path | Where-Object { $_ -match '^HotKey=' } | Select-Object -First 1
  if (-not $line) { return '' }
  return $line.Substring('HotKey='.Length)
}

function Clear-HotKey([string]$Path) {
  if ($Path.EndsWith('.lnk', [StringComparison]::OrdinalIgnoreCase)) {
    $shell = New-Object -ComObject WScript.Shell
    try {
      $shortcut = $shell.CreateShortcut($Path)
      try { $shortcut.Hotkey = ''; $shortcut.Save() }
      finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null }
    } finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
    return
  }
  if (-not [ShortcutManagerIni]::WritePrivateProfileString('InternetShortcut', 'HotKey', '0', $Path)) {
    throw "Failed to clear .url HotKey: $Path (Win32=$([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"
  }
}

$rawPaths = Get-Content -Raw -LiteralPath $PathList | ConvertFrom-Json
if ($rawPaths -is [string]) { $rawPaths = @($rawPaths) }
$paths = @($rawPaths | ForEach-Object { [IO.Path]::GetFullPath([string]$_) } | Select-Object -Unique)
if ($paths.Count -eq 0) { throw 'Path list is empty.' }

$managedDirectory = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('Programs')) 'Shortcut Manager')).TrimEnd('\') + '\'
$items = @()
foreach ($path in $paths) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Shortcut not found: $path" }
  if ($path.StartsWith($managedDirectory, [StringComparison]::OrdinalIgnoreCase)) { throw "Managed shortcut must not be migrated as unmanaged: $path" }
  $extension = [IO.Path]::GetExtension($path)
  if ($extension -notin @('.lnk','.url')) { throw "Unsupported shortcut type: $path" }
  $items += [pscustomobject]@{
    originalPath = $path
    extension = $extension.ToLowerInvariant()
    hotkey = Read-HotKey $path
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
  }
}

$items | Format-Table originalPath,hotkey,sha256 -AutoSize
if ($PlanOnly) {
  Write-Host "Plan only: $($items.Count) shortcut(s), no files changed."
  exit 0
}

$backupRoot = [IO.Path]::GetFullPath($BackupDirectory)
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$staging = Join-Path ([IO.Path]::GetTempPath()) "shortcut-manager-migration-$([Guid]::NewGuid().ToString('N'))"
$filesDirectory = Join-Path $staging 'files'
New-Item -ItemType Directory -Force -Path $filesDirectory | Out-Null
$backupPath = Join-Path $backupRoot "unmanaged-hotkeys-$(Get-Date -Format 'yyyyMMdd-HHmmssfff').zip"

try {
  for ($index = 0; $index -lt $items.Count; $index++) {
    $copyName = '{0:D3}{1}' -f $index,$items[$index].extension
    Copy-Item -LiteralPath $items[$index].originalPath -Destination (Join-Path $filesDirectory $copyName) -ErrorAction Stop
    $items[$index] | Add-Member -NotePropertyName backupEntry -NotePropertyValue "files/$copyName"
  }
  $manifest = [pscustomobject]@{ formatVersion=1; createdAt=[DateTimeOffset]::Now; operation='clear-unmanaged-hotkeys'; items=$items }
  [IO.File]::WriteAllText((Join-Path $staging 'manifest.json'), ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
  Compress-Archive -LiteralPath (Join-Path $staging 'manifest.json'),$filesDirectory -DestinationPath $backupPath -CompressionLevel Optimal

  $changed = New-Object System.Collections.Generic.List[string]
  try {
    foreach ($item in $items) {
      $changed.Add($item.originalPath)
      Clear-HotKey $item.originalPath
      $current = Read-HotKey $item.originalPath
      if ($current -and $current -ne '0') { throw "HotKey read-back was not empty: $($item.originalPath) ($current)" }
      Notify-ShellItemChanged $item.originalPath
    }
  } catch {
    $migrationError = $_.Exception.Message
    $restoreErrors = New-Object System.Collections.Generic.List[string]
    foreach ($path in $changed) {
      $itemIndex = [Array]::IndexOf([string[]]$paths, $path)
      if ($itemIndex -ge 0) {
        try {
          Copy-Item -LiteralPath (Join-Path $filesDirectory ('{0:D3}{1}' -f $itemIndex,$items[$itemIndex].extension)) -Destination $path -Force -ErrorAction Stop
          Notify-ShellItemChanged $path
        } catch {
          $restoreErrors.Add("$path`: $($_.Exception.Message)")
        }
      }
    }
    if ($restoreErrors.Count -gt 0) {
      throw "Migration failed and rollback was incomplete. Original error: $migrationError Restore errors: $($restoreErrors -join ' | ')"
    }
    throw "Migration failed and changed files were restored: $migrationError"
  }

  Write-Host "Migration complete: $($items.Count) HotKey(s) cleared."
  Write-Host "Backup: $backupPath"
} finally {
  if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
