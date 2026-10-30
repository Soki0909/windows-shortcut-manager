using System.Text.RegularExpressions;

namespace ShortcutManager.Core.Protocol;

public sealed partial class DispatcherUriBuilder
{
    [GeneratedRegex("^[a-p]{32}$")]
    private static partial Regex ExtensionIdPattern();

    public Uri BuildFocusGroupUri(string extensionId, string groupName)
    {
        if (!ExtensionIdPattern().IsMatch(extensionId)) throw new ArgumentException("Extension ID must be 32 characters in a-p.", nameof(extensionId));
        var normalizedName = groupName.Trim();
        if (normalizedName.Length is < 1 or > 200) throw new ArgumentException("Group name must be 1 to 200 characters.", nameof(groupName));
        return new Uri($"chrome-extension://{extensionId}/dispatch.html?v=1&action=focus-group&group={Uri.EscapeDataString(normalizedName)}");
    }

    public Uri BuildToggleGroupUri(string extensionId, string groupName)
    {
        var focus = BuildFocusGroupUri(extensionId, groupName).AbsoluteUri;
        return new Uri(focus.Replace("action=focus-group", "action=toggle-group", StringComparison.Ordinal) + "&minExtensionVersion=0.2.0");
    }
}

