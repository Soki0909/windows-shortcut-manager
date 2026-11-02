namespace ShortcutManager.Windows.Logging;

public sealed class FileLogger(string path, long maxBytes = 2 * 1024 * 1024, int maxFiles = 5)
{
    private readonly object gate = new();
    public string? LastFailure { get; private set; }
    public event Action? FailureChanged;

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? error = null) => Write("ERROR", error is null ? message : $"{message}: {error.GetType().Name}: {error.Message}");

    private void Write(string level, string message)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                Rotate();
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
                if (LastFailure is not null) { LastFailure = null; FailureChanged?.Invoke(); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                LastFailure = $"ログを保存できません。操作結果とログ保存は別です: {error.Message}";
                FailureChanged?.Invoke();
            }
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

