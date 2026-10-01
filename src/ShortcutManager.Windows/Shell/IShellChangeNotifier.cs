namespace ShortcutManager.Windows.Shell;

public interface IShellChangeNotifier
{
    void NotifyItemCreated(string path);
    void NotifyItemDeleted(string path);
    void NotifyItemChanged(string path);
    void NotifyDirectoryChanged(string path);
}
