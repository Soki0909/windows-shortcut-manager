using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Windows.Chrome;

namespace ShortcutManager.Windows.Launching;

public sealed class LaunchSpecResolver(ChromeLocator chromeLocator, DispatcherUriBuilder uriBuilder)
{
    public LaunchSpec Resolve(ShortcutConfig config, ShortcutDefinition shortcut)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var explorer = Path.Combine(windows, "explorer.exe");
        var icon = string.Empty;
        string target;
        string arguments;
        string workingDirectory;
        var windowStyle = 1;

        switch (shortcut.Target)
        {
            case WebsiteTarget website:
                target = explorer; arguments = Quote(website.Url); workingDirectory = windows; break;
            case ApplicationTarget app when app.LaunchMode == ApplicationLaunchMode.Executable:
                target = app.Value; arguments = app.Arguments; workingDirectory = string.IsNullOrWhiteSpace(app.WorkingDirectory) ? Path.GetDirectoryName(app.Value) ?? string.Empty : app.WorkingDirectory; icon = app.Value; break;
            case ApplicationTarget app:
                target = explorer; arguments = Quote(app.Value); workingDirectory = string.IsNullOrWhiteSpace(app.WorkingDirectory) ? windows : app.WorkingDirectory; break;
            case BatTarget bat:
                target = Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                arguments = $"/d /s /c \"\"{bat.Path}\"{(string.IsNullOrWhiteSpace(bat.Arguments) ? string.Empty : " " + bat.Arguments)}\"";
                workingDirectory = string.IsNullOrWhiteSpace(bat.WorkingDirectory) ? Path.GetDirectoryName(bat.Path) ?? string.Empty : bat.WorkingDirectory;
                windowStyle = bat.WindowMode == BatWindowMode.Minimized ? 7 : 1;
                break;
            case ChromeTabGroupTarget chrome:
                target = chromeLocator.Find(config.Settings.Chrome.ExecutablePath) ?? throw new FileNotFoundException("chrome.exe was not found.");
                arguments = Quote(uriBuilder.BuildFocusGroupUri(config.Settings.Chrome.ExtensionId, chrome.GroupName).AbsoluteUri);
                workingDirectory = Path.GetDirectoryName(target) ?? string.Empty; icon = target; break;
            default: throw new InvalidOperationException("Unsupported shortcut target.");
        }
        return new(target, arguments, workingDirectory, shortcut.Hotkey.Canonical, string.IsNullOrWhiteSpace(icon) ? target : $"{icon},0", $"Shortcut Manager: {shortcut.Name}", windowStyle);
    }

    public static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

