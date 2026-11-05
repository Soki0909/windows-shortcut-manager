using System.Text.Json;
using System.Runtime.InteropServices;

// No window, console, HotKey, registry access or persistent process. Only emit a
// new report inside the launch test's nonce directory, then exit immediately.
if (args.Length < 2 || args[0] != "--report") return 2;
var report = Path.GetFullPath(args[1]);
if (!Path.GetFileName(Path.GetDirectoryName(report)!).StartsWith("shortcut-manager-launch-", StringComparison.Ordinal)
    || !Path.GetFileName(report).StartsWith("report-", StringComparison.Ordinal)
    || !report.EndsWith(".json", StringComparison.Ordinal)) return 2;
Native.GetStartupInfo(out var startup);
using (var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    JsonSerializer.Serialize(stream, new { ProcessId = Environment.ProcessId, Arguments = args.Skip(2).ToArray(), WorkingDirectory = Environment.CurrentDirectory,
        ShowCommandSpecified = (startup.Flags & 1) != 0, ShowCommand = startup.ShowWindow });
return 0;

internal static class Native
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStartupInfoW")]
    internal static extern void GetStartupInfo(out StartupInfo info);
    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, FillAttribute, Flags;
        public ushort ShowWindow, ReservedBytes;
        public IntPtr ReservedData, StandardInput, StandardOutput, StandardError;
    }
}
