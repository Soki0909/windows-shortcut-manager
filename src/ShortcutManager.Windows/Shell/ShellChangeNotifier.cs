using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.Shell;

public sealed class ShellChangeNotifier : IShellChangeNotifier
{
    private const int ShcneUpdateItem = 0x00002000;
    private const int ShcneUpdateDir = 0x00001000;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlush = 0x1000;

    public void NotifyItemChanged(string path) =>
        SHChangeNotify(ShcneUpdateItem, ShcnfPathW | ShcnfFlush, Path.GetFullPath(path), IntPtr.Zero);

    public void NotifyDirectoryChanged(string path) =>
        SHChangeNotify(ShcneUpdateDir, ShcnfPathW | ShcnfFlush, Path.GetFullPath(path), IntPtr.Zero);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string item1, IntPtr item2);
}
