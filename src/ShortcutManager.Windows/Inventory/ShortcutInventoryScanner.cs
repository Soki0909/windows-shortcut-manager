using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Inventory;

public sealed record ShortcutInventoryItem(string SourcePath, string FileType, string DisplayName, string Hotkey, string Target, string Arguments, string WorkingDirectory, string Icon, bool IsManaged, string? ParseError = null);

public sealed class ShortcutInventoryScanner(IShellLinkService shellLinks, UrlShortcutReader urlReader)
{
    public Task<IReadOnlyList<ShortcutInventoryItem>> ScanDefaultAsync(string managedDirectory, CancellationToken cancellationToken = default)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };
        return ScanAsync(roots, managedDirectory, cancellationToken);
    }

    public Task<IReadOnlyList<ShortcutInventoryItem>> ScanAsync(IEnumerable<string> roots, string managedDirectory, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<ShortcutInventoryItem>>(() =>
    {
        var uniqueRoots = DeduplicateRoots(roots);
        var files = uniqueRoots.SelectMany(EnumerateShortcutFiles).Distinct(StringComparer.OrdinalIgnoreCase);
        var results = new List<ShortcutInventoryItem>();
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isManaged = IsWithin(path, managedDirectory);
            try
            {
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var link = shellLinks.Read(path);
                    results.Add(new(path, ".lnk", Path.GetFileNameWithoutExtension(path), link.Hotkey, link.TargetPath, link.Arguments, link.WorkingDirectory, link.IconLocation, isManaged));
                }
                else
                {
                    var url = urlReader.Read(path);
                    results.Add(new(path, ".url", Path.GetFileNameWithoutExtension(path), url.Hotkey, url.Url, string.Empty, string.Empty, url.IconFile, isManaged));
                }
            }
            catch (Exception error) { results.Add(new(path, Path.GetExtension(path), Path.GetFileNameWithoutExtension(path), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, isManaged, error.Message)); }
        }
        return results;
    }, cancellationToken);

    private static IReadOnlyList<string> EnumerateShortcutFiles(string root)
    {
        if (!Directory.Exists(root)) return [];
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return Directory.EnumerateFiles(root, "*.lnk", options)
                .Concat(Directory.EnumerateFiles(root, "*.url", options))
                .ToArray();
        }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    public static IReadOnlyList<string> DeduplicateRoots(IEnumerable<string> roots)
    {
        var ordered = roots.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path.Length).ToList();
        var result = new List<string>();
        foreach (var root in ordered)
            if (!result.Any(parent => IsWithin(root, parent))) result.Add(root);
        return result;
    }

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
