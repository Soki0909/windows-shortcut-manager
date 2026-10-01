using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.ShellLinks;

public sealed record ShellLinkSpec(string TargetPath, string Arguments, string WorkingDirectory, string Hotkey, string IconLocation, string Description, int WindowStyle = 1);
public sealed record ShellLinkInfo(string Path, string TargetPath, string Arguments, string WorkingDirectory, string Hotkey, string IconLocation, string Description, int WindowStyle);

public interface IShellLinkService
{
    void Write(string path, ShellLinkSpec spec);
    ShellLinkInfo Read(string path);
}

public sealed class ShellLinkService : IShellLinkService
{
    public void Write(string path, ShellLinkSpec spec)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Shortcut path has no directory.", nameof(path)));
        WithShortcut(path, create: true, shortcut =>
        {
            shortcut.TargetPath = spec.TargetPath;
            shortcut.Arguments = spec.Arguments;
            shortcut.WorkingDirectory = spec.WorkingDirectory;
            shortcut.Hotkey = spec.Hotkey;
            shortcut.IconLocation = spec.IconLocation;
            shortcut.Description = spec.Description;
            shortcut.WindowStyle = spec.WindowStyle;
            shortcut.Save();
            return 0;
        });
    }

    public ShellLinkInfo Read(string path) => WithShortcut(path, create: false, shortcut => new ShellLinkInfo(
        path,
        Convert.ToString(shortcut.TargetPath) ?? string.Empty,
        Convert.ToString(shortcut.Arguments) ?? string.Empty,
        Convert.ToString(shortcut.WorkingDirectory) ?? string.Empty,
        NormalizeHotkey(Convert.ToString(shortcut.Hotkey)),
        Convert.ToString(shortcut.IconLocation) ?? string.Empty,
        Convert.ToString(shortcut.Description) ?? string.Empty,
        Convert.ToInt32(shortcut.WindowStyle)));

    private static T WithShortcut<T>(string path, bool create, Func<dynamic, T> action)
    {
        if (!create && !File.Exists(path)) throw new FileNotFoundException("Shortcut was not found.", path);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new PlatformNotSupportedException("WScript.Shell is unavailable.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Could not create WScript.Shell.");
            shortcut = ((dynamic)shell).CreateShortcut(path);
            return action((dynamic)shortcut);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    private static string NormalizeHotkey(string? value)
    {
        var parts = (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        if (parts.Contains("CTRL")) result.Add("CTRL");
        if (parts.Contains("ALT")) result.Add("ALT");
        if (parts.Contains("SHIFT")) result.Add("SHIFT");
        result.AddRange(parts.Where(part => part is not "CTRL" and not "ALT" and not "SHIFT").Order(StringComparer.Ordinal));
        return string.Join('+', result);
    }
}
