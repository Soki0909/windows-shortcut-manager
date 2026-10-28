param([string]$Version = '0.1.1')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LogoResources {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
 [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr module);
 [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
 [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module, IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
 public delegate bool Callback(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
 [DllImport("kernel32.dll")] public static extern bool EnumResourceNames(IntPtr module, IntPtr type, Callback callback, IntPtr param);
 public static byte[] Read(IntPtr module, int type, IntPtr name) {
  var r=FindResource(module,name,(IntPtr)type);
  if(r==IntPtr.Zero) throw new Exception("Missing resource");
  var data=new byte[SizeofResource(module,r)];
  Marshal.Copy(LockResource(LoadResource(module,r)),data,0,data.Length); return data;
 }
}
'@
$ico = [IO.File]::ReadAllBytes("$root/src/ShortcutManager.App/Assets/shortcut_manager_logo.ico")
$frames = @{}
for ($i=0; $i -lt [BitConverter]::ToUInt16($ico,4); $i++) {
 $entry = 6 + 16*$i
 $size = if ($ico[$entry] -eq 0) {256} else {[int]$ico[$entry]}
 $length = [BitConverter]::ToUInt32($ico,$entry+8)
 $offset = [BitConverter]::ToUInt32($ico,$entry+12)
 $frames[$size] = [Convert]::ToBase64String($ico[$offset..($offset+$length-1)])
}
if (($frames.Keys | Sort-Object) -join ',' -ne '16,24,32,48,64,128,256') { throw 'Wrong ICO sizes' }
foreach ($file in @('artifacts/publish/win-x64/ShortcutManager.exe',"artifacts/ShortcutManager-Setup-win-x64-v$Version.exe")) {
 $module = [LogoResources]::LoadLibraryEx("$root/$file",[IntPtr]::Zero,2)
 if ($module -eq [IntPtr]::Zero) { throw "Cannot read $file" }
 try {
  $script:groups = @()
  $callback = [LogoResources+Callback]{ param($m,$t,$name,$p) $script:groups += ,([LogoResources]::Read($m,14,$name)); return $true }
  if (-not [LogoResources]::EnumResourceNames($module,[IntPtr]14,$callback,[IntPtr]::Zero)) { throw 'No icon groups' }
  $matched = $false
  foreach ($group in $script:groups) {
   $sizes = @()
   for ($i=0; $i -lt [BitConverter]::ToUInt16($group,4); $i++) {
    $entry=6+14*$i
    $size=if($group[$entry] -eq 0){256}else{[int]$group[$entry]}
    $id=[BitConverter]::ToUInt16($group,$entry+12)
    $bytes=[LogoResources]::Read($module,3,[IntPtr][int]$id)
    if ($frames.ContainsKey($size) -and [Convert]::ToBase64String($bytes) -eq $frames[$size]) { $sizes += $size }
   }
   if (($sizes | Sort-Object -Unique) -join ',' -eq '16,24,32,48,64,128,256') { $matched=$true }
  }
  if (-not $matched) { throw "Product icon frames mismatch: $file" }
  Write-Output "$file : all seven embedded ICO frames byte-identical"
 } finally { [void][LogoResources]::FreeLibrary($module) }
}
foreach ($line in Get-Content "$root/artifacts/SHA256SUMS.txt") {
 $hash,$name = $line -split '  ',2
 if ((Get-FileHash "$root/artifacts/$name" -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw "Hash mismatch: $name" }
}
Write-Output 'All three artifact checksums match'
