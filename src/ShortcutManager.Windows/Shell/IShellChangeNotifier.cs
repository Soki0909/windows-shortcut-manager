namespace ShortcutManager.Windows.Shell;

public interface IShellChangeNotifier
{
    void NotifyItemChanged(string path);
    void NotifyDirectoryChanged(string path);
}
