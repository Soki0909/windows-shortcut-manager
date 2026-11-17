#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ShortcutManager.Windows.ShellLinks;

public static class ShortcutReferenceGuard
{
    // AllowLinkToLink is safe only for an acyclic chain. Do not Resolve or save
    // any source. Read the stored filesystem targets, with a finite depth bound.
    public static void EnsureAcyclic(string source, IShellLinkService links, string? destination = null, string? managedDirectory = null)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var forbiddenFile = destination is null ? null : NormalizeExistingOrNewPath(destination);
        var forbiddenDirectory = managedDirectory is null ? null : NormalizeExistingOrNewPath(managedDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var current = Path.GetFullPath(source);
        while (current.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var identity = NormalizeExistingOrNewPath(current);
            if (!visited.Add(identity) || identity.Equals(forbiddenFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("元ショートカットの参照が循環しています。元ファイルを確認してください。");
            if (forbiddenDirectory is not null && identity.StartsWith(forbiddenDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("管理フォルダ内のリンクを元ショートカットとして参照できません。管理外の原本を選んでください。");
            if (visited.Count > 16) throw new InvalidDataException("元ショートカットの参照が深すぎるため、安全に反映できません。");
            var target = links.Read(current).TargetPath;
            if (!target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) break;
            if (!Path.IsPathFullyQualified(target)) throw new InvalidDataException("相対的なリンク間参照は、安全に検証できません。");
            current = Path.GetFullPath(target);
        }
    }

    private static string NormalizeExistingOrNewPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        // FILE_READ_ATTRIBUTES and FILE_FLAG_BACKUP_SEMANTICS also allow opening
        // a directory. Resolve aliases without reading or changing file data.
        using var handle = CreateFile(fullPath, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error is 2 or 3) return fullPath; // A new managed destination may not exist yet.
            throw new IOException($"ショートカットの参照先を確認できません: {fullPath}", new Win32Exception(error));
        }

        var pathBuffer = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(handle, pathBuffer, (uint)pathBuffer.Capacity, 0);
        if (length == 0 || length >= pathBuffer.Capacity)
            throw new IOException($"ショートカットの参照先を確認できません: {fullPath}", new Win32Exception(Marshal.GetLastWin32Error()));
        var finalPath = pathBuffer.ToString();
        if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + finalPath[8..];
        if (finalPath.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) return finalPath[4..];
        return finalPath;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string filename, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint capacity, uint flags);
}
