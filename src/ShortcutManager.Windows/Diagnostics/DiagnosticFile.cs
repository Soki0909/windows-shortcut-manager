using System.Text;

namespace ShortcutManager.Windows.Diagnostics;

public static class DiagnosticFile
{
    // Publish only a complete new file. Never overwrite config, an existing
    // diagnostic or another user file, including a destination created mid-save.
    public static void SaveNew(string path, string preview)
    {
        if (preview.Length > 128 * 1024) throw new ArgumentException("診断の容量が上限を超えています。", nameof(preview));
        var destination = Path.GetFullPath(path);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("同名のファイルが存在します。別の名前を指定してください。");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".shortcut-manager-diagnostic-{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                created = true;
                var bytes = new UTF8Encoding(false).GetBytes(preview);
                stream.Write(bytes); stream.Flush(true);
            }
            File.Move(temporary, destination, false);
        }
        finally { if (created && File.Exists(temporary)) File.Delete(temporary); }
    }
}
