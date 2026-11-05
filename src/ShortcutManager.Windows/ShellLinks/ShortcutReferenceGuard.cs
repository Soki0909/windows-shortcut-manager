namespace ShortcutManager.Windows.ShellLinks;

public static class ShortcutReferenceGuard
{
    // AllowLinkToLink is safe only for an acyclic chain. Do not Resolve or save
    // any source. Read the stored filesystem targets, with a finite depth bound.
    public static void EnsureAcyclic(string source, IShellLinkService links, string? destination = null, string? managedDirectory = null)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var forbiddenFile = destination is null ? null : Path.GetFullPath(destination);
        var forbiddenDirectory = managedDirectory is null ? null : Path.GetFullPath(managedDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var current = Path.GetFullPath(source);
        while (current.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            if (!visited.Add(current) || current.Equals(forbiddenFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("元ショートカットの参照が循環しています。元ファイルを確認してください。");
            if (forbiddenDirectory is not null && current.StartsWith(forbiddenDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("管理フォルダ内のリンクを元ショートカットとして参照できません。管理外の原本を選んでください。");
            if (visited.Count > 16) throw new InvalidDataException("元ショートカットの参照が深すぎるため、安全に反映できません。");
            var target = links.Read(current).TargetPath;
            if (!target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) break;
            if (!Path.IsPathFullyQualified(target)) throw new InvalidDataException("相対的なリンク間参照は、安全に検証できません。");
            current = Path.GetFullPath(target);
        }
    }
}
