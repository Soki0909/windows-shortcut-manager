using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

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
    private const int BufferSize = 32768;

    public void Write(string path, ShellLinkSpec spec)
    {
        path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? throw new ArgumentException("Shortcut path has no directory.", nameof(path)));
        WithShortcut(path, true, (link, persist) =>
        {
            link.SetPath(spec.TargetPath);
            link.SetArguments(spec.Arguments);
            link.SetWorkingDirectory(spec.WorkingDirectory);
            link.SetHotkey(EncodeHotkey(spec.Hotkey));
            var icon = spec.IconLocation;
            var index = 0;
            var comma = icon.LastIndexOf(',');
            if (comma >= 0 && int.TryParse(icon[(comma + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) { index = parsed; icon = icon[..comma]; }
            link.SetIconLocation(icon, index);
            link.SetDescription(spec.Description);
            link.SetShowCmd(spec.WindowStyle);
            persist.Save(path, true);
            return 0;
        });
    }

    public ShellLinkInfo Read(string path) => WithShortcut(path, false, (link, _) =>
    {
        var target = new StringBuilder(BufferSize); var arguments = new StringBuilder(BufferSize);
        var directory = new StringBuilder(BufferSize); var icon = new StringBuilder(BufferSize); var description = new StringBuilder(BufferSize);
        // SLGP_RAWPATH reads stored data. Never Resolve, launch, search for moved
        // targets, rewrite the source, or move a relative link to a temporary path.
        link.GetPath(target, BufferSize, IntPtr.Zero, 4);
        link.GetArguments(arguments, BufferSize);
        link.GetWorkingDirectory(directory, BufferSize);
        link.GetIconLocation(icon, BufferSize, out var iconIndex);
        link.GetDescription(description, BufferSize);
        link.GetHotkey(out var hotkey); link.GetShowCmd(out var windowStyle);
        return new ShellLinkInfo(path, target.ToString(), arguments.ToString(), directory.ToString(), DecodeHotkey(hotkey), icon.Length == 0 ? string.Empty : icon + "," + iconIndex.ToString(CultureInfo.InvariantCulture), description.ToString(), windowStyle);
    });

    private static T WithShortcut<T>(string path, bool create, Func<IShellLinkW, IPersistFile, T> action)
    {
        if (!create && !File.Exists(path)) throw new FileNotFoundException("Shortcut was not found.", path);
        object instance = new ShellLinkNative();
        try
        {
            var persist = (IPersistFile)instance;
            if (!create) persist.Load(System.IO.Path.GetFullPath(path), 0);
            return action((IShellLinkW)instance, persist);
        }
        finally { if (Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance); }
    }

    private static ushort EncodeHotkey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var parts = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries);
        var key = parts.LastOrDefault() ?? string.Empty;
        var code = key.Length == 1 && (key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') ? key[0]
            : key.StartsWith('F') && int.TryParse(key[1..], out var function) && function is >= 1 and <= 24 ? 0x6f + function : 0;
        if (code == 0 || parts.Take(parts.Length - 1).Any(part => part is not "CTRL" and not "ALT" and not "SHIFT")) throw new ArgumentException("Unsupported shortcut HotKey.", nameof(value));
        var modifiers = (parts.Contains("SHIFT") ? 1 : 0) | (parts.Contains("CTRL") ? 2 : 0) | (parts.Contains("ALT") ? 4 : 0);
        return (ushort)(code | modifiers << 8);
    }

    private static string DecodeHotkey(ushort value)
    {
        if (value == 0) return string.Empty;
        var key = value & 0xff; var modifiers = value >> 8;
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("CTRL");
        if ((modifiers & 4) != 0) parts.Add("ALT");
        if ((modifiers & 1) != 0) parts.Add("SHIFT");
        parts.Add(key is >= 'A' and <= 'Z' or >= '0' and <= '9' ? ((char)key).ToString() : key is >= 0x70 and <= 0x87 ? "F" + (key - 0x6f).ToString(CultureInfo.InvariantCulture) : "VK_" + key.ToString("X2", CultureInfo.InvariantCulture));
        return string.Join('+', parts);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkNative { }

    // Vtable order is defined by shobjidl_core.h. Use Unicode on every string;
    // WScript's ANSI conversion loses names outside the current code page.
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
