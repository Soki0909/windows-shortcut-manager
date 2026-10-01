using ShortcutManager.Core.Models;

namespace ShortcutManager.Windows.Inventory;

public sealed class ShortcutImporter
{
    public ShortcutDefinition CreateProposal(ShortcutInventoryItem item, string key)
    {
        ShortcutTarget target;
        if (Uri.TryCreate(item.Target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") target = new WebsiteTarget { Url = item.Target };
        else if (item.Target.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || item.Target.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) target = new BatTarget { Path = item.Target, Arguments = item.Arguments, WorkingDirectory = item.WorkingDirectory };
        else if (item.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) target = new ApplicationTarget { LaunchMode = ApplicationLaunchMode.Executable, Value = item.Target, Arguments = item.Arguments, WorkingDirectory = item.WorkingDirectory };
        else target = new ApplicationTarget { LaunchMode = ApplicationLaunchMode.Shortcut, Value = item.SourcePath };
        return new() { Name = item.DisplayName, Enabled = true, Hotkey = new() { Key = key }, Target = target };
    }
}

