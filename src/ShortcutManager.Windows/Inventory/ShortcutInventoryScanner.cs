using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Inventory;

public sealed record ShortcutInventoryItem(string SourcePath, string FileType, string DisplayName, string Hotkey, string Target, string Arguments, string WorkingDirectory, string Icon, bool IsManaged, string? ParseError = null);
public sealed record InventoryScanIssue(string Path, string Reason, string Message);
public sealed record InventoryScanReport(IReadOnlyList<ShortcutInventoryItem> Items, IReadOnlyList<InventoryScanIssue> Issues, IReadOnlyList<string> Roots)
{
    public int ReadFailureCount => Items.Count(item => item.ParseError is not null);
    public bool IsComplete => Issues.Count == 0 && ReadFailureCount == 0;
    public string Summary => $"正常 {Items.Count-ReadFailureCount} 件、読取り失敗 {ReadFailureCount} 件、未走査・除外 {Issues.Count} 箇所。" + (IsComplete ? "指定範囲の走査が完了しました。" : "一部を確認できていません。結果が全件とは限りません。");
}

public sealed class ShortcutInventoryScanner(IShellLinkService shellLinks, UrlShortcutReader urlReader, Func<string,string[]>? enumerateEntries = null)
{
    public static IReadOnlyList<string> DefaultRoots => new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        }.Where(path=>!string.IsNullOrWhiteSpace(path)).ToArray();

    public Task<IReadOnlyList<ShortcutInventoryItem>> ScanDefaultAsync(string managedDirectory, CancellationToken cancellationToken = default) => ScanAsync(DefaultRoots,managedDirectory,cancellationToken);
    public async Task<IReadOnlyList<ShortcutInventoryItem>> ScanAsync(IEnumerable<string> roots, string managedDirectory, CancellationToken cancellationToken = default) => (await ScanReportAsync(roots,managedDirectory,cancellationToken)).Items;
    public Task<InventoryScanReport> ScanReportAsync(IEnumerable<string> roots, string managedDirectory, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var uniqueRoots = DeduplicateRoots(roots);
        var issues = new List<InventoryScanIssue>();
        var files = uniqueRoots.SelectMany(root=>EnumerateShortcutFiles(root,issues,cancellationToken)).Distinct(StringComparer.OrdinalIgnoreCase);
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
                    if(string.IsNullOrWhiteSpace(url.Url)) throw new InvalidDataException("URL欄がないか空です。元ファイルを確認してください。");
                    results.Add(new(path, ".url", Path.GetFileNameWithoutExtension(path), url.Hotkey, url.Url, string.Empty, string.Empty, url.IconFile, isManaged));
                }
            }
            catch (Exception error) { results.Add(new(path, Path.GetExtension(path), Path.GetFileNameWithoutExtension(path), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, isManaged, error.Message)); }
        }
        return new InventoryScanReport(results,issues,uniqueRoots);
    }, cancellationToken);

    private IReadOnlyList<string> EnumerateShortcutFiles(string root, List<InventoryScanIssue> issues, CancellationToken cancellationToken)
    {
        var files=new List<string>(); var pending=new Stack<string>();
        try
        {
            for(var directory=new DirectoryInfo(root);directory is not null;directory=directory.Parent)
                if((File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint)!=0) {issues.Add(new(root,"reparse-point","リンク・ジャンクションの配下は走査しません。"));return files;}
        }
        catch(Exception error) when(error is UnauthorizedAccessException or IOException or System.Security.SecurityException) {issues.Add(new(root,"unavailable",error.Message));return files;}
        pending.Push(root);
        while(pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach(var path in (enumerateEntries ?? Directory.GetFileSystemEntries)(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes=File.GetAttributes(path);
                        if((attributes & FileAttributes.ReparsePoint)!=0) {issues.Add(new(path,"reparse-point","リンク・ジャンクションは走査対象から除外しました。"));continue;}
                        if((attributes & FileAttributes.Directory)!=0) pending.Push(path);
                        else if(path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".url",StringComparison.OrdinalIgnoreCase)) files.Add(path);
                    }
                    catch(Exception error) when(error is UnauthorizedAccessException or IOException or System.Security.SecurityException) {issues.Add(new(path,"unavailable",error.Message));}
                }
            }
            catch(Exception error) when(error is UnauthorizedAccessException or IOException or System.Security.SecurityException) {issues.Add(new(directory,"unavailable",error.Message));}
        }
        return files;
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
