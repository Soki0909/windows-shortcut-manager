using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ShortcutManager.Windows.Removal;

internal readonly record struct RemovalFileIdentity(uint Volume, ulong Index, ulong Created);

internal static class RemovalFileInspection
{
    private const uint ReadAttributes = 0x80;
    private const uint OpenDirectory = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;

    public static FileAttributes? Attributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public static string DirectoryPath(string path, ICollection<SafeFileHandle>? locked = null)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new InvalidDataException("通常の絶対パスで保存先を指定してください。");
        var directory = new DirectoryInfo(path);
        var parents = new Stack<DirectoryInfo>();
        for (var parent = directory; parent is not null; parent = parent.Parent) parents.Push(parent);
        string? canonical = null;
        foreach (var part in parents)
        {
            var attributes = Attributes(part.FullName);
            if (attributes is not null && (attributes.Value & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != FileAttributes.Directory)
                throw new InvalidDataException($"フォルダのリンクまたは別の種類のファイルを保護します: {part.FullName}");
            if (attributes is null)
            {
                if (locked is not null) throw new DirectoryNotFoundException($"保存先が存在しません: {part.FullName}");
                canonical = canonical is null ? part.FullName : Path.Combine(canonical, part.Name);
                continue;
            }
            var handle = CreateFile(part.FullName, ReadAttributes, locked is null ? FileShare.ReadWrite | FileShare.Delete : FileShare.Read,
                IntPtr.Zero, FileMode.Open, OpenDirectory | OpenReparsePoint, IntPtr.Zero);
            try
            {
                if (handle.IsInvalid) throw NativeError(part.FullName);
                var info = ReadInformation(handle, part.FullName);
                if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != FileAttributes.Directory)
                    throw new InvalidDataException($"フォルダが照会中に置き換わりました: {part.FullName}");
                var resolved = FinalPath(handle, part.FullName);
                if (canonical is not null && !SamePath(Path.GetDirectoryName(resolved) ?? resolved, canonical))
                    throw new InvalidDataException($"フォルダが別の場所に解決されました: {part.FullName}");
                canonical = resolved;
                if (locked is not null) { locked.Add(handle); continue; }
            }
            catch { handle.Dispose(); throw; }
            handle.Dispose();
        }
        return Path.TrimEndingDirectorySeparator(canonical!);
    }

    public static RemovalFileIdentity Identity(FileStream stream, string path)
    {
        var info = ReadInformation(stream.SafeFileHandle, path);
        return new(info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow,
            ((ulong)(uint)info.CreationTime.dwHighDateTime << 32) | (uint)info.CreationTime.dwLowDateTime);
    }

    public static FileStream OpenFile(string path, int maxBytes)
    {
        var attributes = Attributes(path);
        if (attributes is null || (attributes.Value & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("通常ファイルとして確認できません。リンクやフォルダを保護します。");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var info = ReadInformation(stream.SafeFileHandle, path);
            if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 || info.Links != 1)
                throw new InvalidDataException("リンクや別名があるファイルを保護します。");
            if (!SamePath(FinalPath(stream.SafeFileHandle, path), path))
                throw new InvalidDataException("ファイルが別の場所に解決されました。");
            if (stream.Length > maxBytes) throw new InvalidDataException("ファイルが読取り上限を超えています。");
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static bool SamePath(string left, string right) =>
        Path.TrimEndingDirectorySeparator(left).Equals(Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    public static bool IsWithin(string path, string root) =>
        SamePath(path, root) || path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static FileInformation ReadInformation(SafeFileHandle handle, string path) =>
        GetFileInformationByHandle(handle, out var info) ? info : throw NativeError(path);

    internal static string FinalPath(SafeFileHandle handle, string path)
    {
        var result = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(handle, result, (uint)result.Capacity, 0);
        if (length == 0) throw NativeError(path);
        if (length >= result.Capacity) throw new InvalidDataException("正規化されたパスが長すぎます。");
        var resolved = result.ToString();
        if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + resolved[8..];
        if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) return resolved[4..];
        throw new InvalidDataException($"正規化されたパスを確認できません: {path}");
    }

    internal static IOException NativeError(string path) =>
        new($"ファイルの実体を確認できません: {path}", new Win32Exception(Marshal.GetLastWin32Error()));

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public FileAttributes Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, IntPtr security,
        FileMode disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder result, uint length, uint flags);
}
