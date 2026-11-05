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
public sealed record ShellLinkInfo(string Path, string TargetPath, string Arguments, string WorkingDirectory, string Hotkey, string IconLocation, string Description, int WindowStyle, bool IsLinkReference = false);

public interface IShellLinkService
{
    void Write(string path, ShellLinkSpec spec);
    ShellLinkInfo Read(string path);
}

public sealed class ShellLinkService : IShellLinkService
{
    private const int BufferSize = 32768;
    private const uint AllowLinkToLink = 0x00800000;
    // Do not create tracking/object-ID metadata on the unmanaged source.
    private const uint ReferencedLinkFlags = AllowLinkToLink | 0x00000100 | 0x00040000;

    public void Write(string path, ShellLinkSpec spec)
    {
        path = System.IO.Path.GetFullPath(path);
        if (spec.TargetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            ShortcutReferenceGuard.EnsureAcyclic(spec.TargetPath, this, destination: path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? throw new ArgumentException("Shortcut path has no directory.", nameof(path)));
        WithShortcut(path, true, (link, persist) =>
        {
            if (spec.TargetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                // SetPath flattens an existing .lnk even with AllowLinkToLink.
                // Keep the source file's PIDL, not a reconstructed executable.
                var data = (IShellLinkDataList)link;
                data.GetFlags(out var flags); data.SetFlags(flags | ReferencedLinkFlags);
                SHParseDisplayName(spec.TargetPath, IntPtr.Zero, out var idList, 0, out _);
                if (idList == IntPtr.Zero) throw new IOException("Could not identify the referenced Windows shortcut.");
                try { link.SetIDList(idList); }
                finally { Marshal.FreeCoTaskMem(idList); }
            }
            else link.SetPath(spec.TargetPath);
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
            if (spec.TargetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var data = (IShellLinkDataList)link;
                data.GetFlags(out var flags); data.SetFlags(flags | ReferencedLinkFlags);
                data.GetFlags(out flags);
                if ((flags & AllowLinkToLink) == 0) throw new IOException("Windows rejected link-to-link mode: " + flags.ToString("X8"));
                var actualPath = new StringBuilder(BufferSize); link.GetPath(actualPath, BufferSize, IntPtr.Zero, 4);
                if (!actualPath.ToString().Equals(spec.TargetPath, StringComparison.OrdinalIgnoreCase)) throw new IOException("Windows did not preserve the referenced shortcut path.");
            }
            if (spec.TargetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) SaveToStream((IPersistStream)link, path);
            else persist.Save(path, true);
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
        ((IShellLinkDataList)link).GetFlags(out var flags);
        return new ShellLinkInfo(path, target.ToString(), arguments.ToString(), directory.ToString(), DecodeHotkey(hotkey), icon.Length == 0 ? string.Empty : icon + "," + iconIndex.ToString(CultureInfo.InvariantCulture), description.ToString(), windowStyle, (flags & AllowLinkToLink) != 0);
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false, ExactSpelling = true)]
    private static extern void SHParseDisplayName(string path, IntPtr binding, out IntPtr idList, uint attributes, out uint resultAttributes);

    private static void SaveToStream(IPersistStream persist, string path)
    {
        // IPersistFile.Save flattens a reference to another .lnk. Serialize the
        // native Shell object through IPersistStream instead; do not hand-build
        // the .lnk format or copy an unmanaged link into our managed directory.
        CreateStreamOnHGlobal(IntPtr.Zero, true, out var stream);
        var readCount = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            persist.Save(stream, true); stream.Stat(out var stat, 1);
            if (stat.cbSize < 76 || stat.cbSize > 1024 * 1024) throw new InvalidDataException("Referenced shortcut size is invalid.");
            stream.Seek(0, 0, IntPtr.Zero);
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[65536]; long remaining = stat.cbSize;
            while (remaining > 0)
            {
                var count = (int)Math.Min(buffer.Length, remaining);
                stream.Read(buffer, count, readCount); var read = Marshal.ReadInt32(readCount);
                if (read <= 0 || read > count) throw new InvalidDataException("Referenced shortcut stream is incomplete.");
                file.Write(buffer, 0, read); remaining -= read;
            }
            file.Flush(true);
        }
        finally { Marshal.FreeCoTaskMem(readCount); Marshal.FinalReleaseComObject(stream); }
    }

    [DllImport("ole32.dll", PreserveSig = false)]
    private static extern void CreateStreamOnHGlobal(IntPtr global, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease, out IStream stream);

    [ComImport, Guid("00000109-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistStream
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load(IStream stream);
        void Save(IStream stream, [MarshalAs(UnmanagedType.Bool)] bool clearDirty);
        void GetSizeMax(out long size);
    }

    [ComImport, Guid("45E2B4AE-B1C3-11D0-B92F-00A0C90312E1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkDataList
    {
        void AddDataBlock(IntPtr data);
        void CopyDataBlock(uint signature, out IntPtr data);
        void RemoveDataBlock(uint signature);
        void GetFlags(out uint flags);
        void SetFlags(uint flags);
    }

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
