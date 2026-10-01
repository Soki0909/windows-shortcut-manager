namespace ShortcutManager.Windows.Logging;

public sealed class FileLogger(string path, long maxBytes = 2 * 1024 * 1024, int maxFiles = 5)
{
    private readonly object gate = new();

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? error = null) => Write("ERROR", error is null ? message : $"{message}: {error.GetType().Name}: {error.Message}");

    private void Write(string level, string message)
    {
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Rotate();
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
    }

    private void Rotate()
    {
        if (!File.Exists(path) || new FileInfo(path).Length < maxBytes) return;
        for (var index = maxFiles - 1; index >= 1; index--)
        {
            var source = index == 1 ? path : $"{path}.{index - 1}";
            var destination = $"{path}.{index}";
            if (File.Exists(source)) File.Move(source, destination, true);
        }
    }
}

