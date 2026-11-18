using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ShortcutManager.Windows.Removal;

// The only writable handle here is for a newly created archive temporary file.
internal sealed class RemovalArchiveDestination : IDisposable
{
    private const uint ReadWriteDelete = 0xC0010000;
    private const uint WriteThrough = 0x80000000;
    private const int FileRenameInfo = 3;
    private const int FileDispositionInfo = 4;
    private readonly string temporaryPath;
    private readonly string destination;
    public FileStream Stream { get; }

    public RemovalArchiveDestination(string destination)
    {
        this.destination = destination;
        temporaryPath = Path.Combine(Path.GetDirectoryName(destination)!, $".shortcut-manager-removal-{Guid.NewGuid():N}.tmp");
        var handle = RemovalFileInspection.CreateFile(temporaryPath, ReadWriteDelete, FileShare.None,
            IntPtr.Zero, FileMode.CreateNew, WriteThrough, IntPtr.Zero);
        try
        {
            if (handle.IsInvalid) throw RemovalFileInspection.NativeError(temporaryPath);
            SetTemporary(handle, true);
            if (!RemovalFileInspection.SamePath(RemovalFileInspection.FinalPath(handle, temporaryPath), temporaryPath))
                throw new InvalidDataException("アーカイブの一時ファイルが別の場所に解決されました。");
            Stream = new FileStream(handle, FileAccess.ReadWrite);
        }
        catch { handle.Dispose(); throw; }
    }

    public static void RequireSpace(string directory, ulong bytes)
    {
        if (!GetDiskFreeSpaceEx(directory, out var available, out _, out _))
            throw RemovalFileInspection.NativeError(directory);
        if (available < bytes) throw new IOException("削除前アーカイブを保存する空き容量が足りません。");
    }

    public void Publish()
    {
        var handle = Stream.SafeFileHandle;
        var nameOffset = (int)Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.FileName));
        var size = nameOffset + (destination.Length + 1) * sizeof(char);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            // ReplaceIfExists=false and RootDirectory=NULL. The path is absolute
            // and its parent directories remain open without write/delete sharing.
            Marshal.WriteInt32(buffer, 0, 0);
            Marshal.WriteIntPtr(buffer, (int)Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.Root)), IntPtr.Zero);
            Marshal.WriteInt32(buffer, (int)Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.FileNameLength)),
                destination.Length * sizeof(char));
            Marshal.Copy(destination.ToCharArray(), 0, buffer + nameOffset, destination.Length);
            Marshal.WriteInt16(buffer, size - sizeof(char), 0);
            SetTemporary(handle, false);
            if (SetFileInformation(handle, FileRenameInfo, buffer, (uint)size)) return;
            var error = RemovalFileInspection.NativeError(destination);
            SetTemporary(handle, true);
            throw new IOException("削除前アーカイブを新しいファイルとして保存できません。", error);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private void SetTemporary(SafeFileHandle handle, bool deleteOnClose)
    {
        var buffer = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(buffer, deleteOnClose ? (byte)1 : (byte)0);
            if (!SetFileInformation(handle, FileDispositionInfo, buffer, 1))
                throw RemovalFileInspection.NativeError(temporaryPath);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public void Dispose() => Stream.Dispose();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RenameInformation
    {
        public uint Flags;
        public IntPtr Root;
        public uint FileNameLength;
        public char FileName;
    }

    [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformation(SafeFileHandle file, int infoClass, IntPtr buffer, uint size);

    [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directory, out ulong available, out ulong total, out ulong free);
}
