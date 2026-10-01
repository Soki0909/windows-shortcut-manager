namespace ShortcutManager.Windows.Chrome;

public sealed class ChromeLocator
{
    public string? Find(string? configuredPath = null)
    {
        IEnumerable<string?> candidates =
        [
            configuredPath,
            CombineEnvironment("ProgramFiles", "Google", "Chrome", "Application", "chrome.exe"),
            CombineEnvironment("ProgramFiles(x86)", "Google", "Chrome", "Application", "chrome.exe"),
            CombineEnvironment("LOCALAPPDATA", "Google", "Chrome", "Application", "chrome.exe"),
        ];
        return candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Cast<string>().FirstOrDefault(File.Exists);
    }

    private static string? CombineEnvironment(string variable, params string[] parts)
    {
        var root = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine([root, .. parts]);
    }
}

