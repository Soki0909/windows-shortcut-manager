using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.Shell;

public sealed class ShellChangeNotifier : IShellChangeNotifier
{
    private const int ShcneCreate = 0x00000002;
    private const int ShcneDelete = 0x00000004;
    private const int ShcneUpdateItem = 0x00002000;
    private const int ShcneUpdateDir = 0x00001000;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlush = 0x1000;

    public void NotifyItemCreated(string path) =>
        NotifyPath(ShcneCreate, path);

    public void NotifyItemDeleted(string path) =>
        NotifyPath(ShcneDelete, path);

    public void NotifyItemChanged(string path) =>
        NotifyPath(ShcneUpdateItem, path);

    public void NotifyDirectoryChanged(string path) =>
        NotifyPath(ShcneUpdateDir, path);

    private static void NotifyPath(int eventId, string path) =>
        SHChangeNotify(eventId, ShcnfPathW | ShcnfFlush, Path.GetFullPath(path), IntPtr.Zero);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string item1, IntPtr item2);
}
