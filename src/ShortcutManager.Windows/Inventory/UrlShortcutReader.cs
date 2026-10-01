using System.Globalization;

namespace ShortcutManager.Windows.Inventory;

public sealed record UrlShortcutInfo(string Path, string Url, string Hotkey, string IconFile, int? IconIndex);

public sealed class UrlShortcutReader
{
    public UrlShortcutInfo Read(string path)
    {
        var values = File.ReadLines(path)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        values.TryGetValue("URL", out var url);
        values.TryGetValue("HotKey", out var hotkeyText);
        values.TryGetValue("IconFile", out var iconFile);
        int? iconIndex = values.TryGetValue("IconIndex", out var iconText) && int.TryParse(iconText, CultureInfo.InvariantCulture, out var index) ? index : null;
        var hotkey = int.TryParse(hotkeyText, CultureInfo.InvariantCulture, out var value) ? DecodeHotkey(value) : string.Empty;
        return new(path, url ?? string.Empty, hotkey, iconFile ?? string.Empty, iconIndex);
    }

    public static string DecodeHotkey(int value)
    {
        var key = value & 0xff;
        var modifiers = (value >> 8) & 0xff;
        var parts = new List<string>();
        if ((modifiers & 0x02) != 0) parts.Add("CTRL");
        if ((modifiers & 0x04) != 0) parts.Add("ALT");
        if ((modifiers & 0x01) != 0) parts.Add("SHIFT");
        if (key is >= 'A' and <= 'Z') parts.Add(((char)key).ToString());
        return parts.Count > 0 ? string.Join('+', parts) : string.Empty;
    }
}

