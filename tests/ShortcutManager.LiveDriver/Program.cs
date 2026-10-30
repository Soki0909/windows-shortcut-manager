using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ShortcutManager.Windows.Shell;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);

// Disposable Windows E2E input driver. Never shipped with the application.
if (args is ["latency-target", var latencyRoot])
{
    var thread = new Thread(() => LatencySession.Target(latencyRoot)); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
}
else if (args is ["latency-session", var sessionRoot])
{
    var thread = new Thread(() => LatencySession.Session(sessionRoot)); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
}
else if (args is ["probe-shell", var report])
{
    File.WriteAllText(report, JsonSerializer.Serialize(new ShortcutManager.Windows.Diagnostics.ShellResponsivenessProbe().Run(), new JsonSerializerOptions { WriteIndented = true }));
}
else if (args is ["window"])
{
    var thread = new Thread(() => {
        using var form = new System.Windows.Forms.Form { Text = "Shortcut Manager isolated live test", Width = 520, Height = 200 };
        form.Controls.Add(new System.Windows.Forms.Label { Text = "テスト専用の非Chrome画面です。案内に従って Ctrl+Alt+U を押してください。", Dock = System.Windows.Forms.DockStyle.Fill, Padding = new System.Windows.Forms.Padding(24) });
        System.Windows.Forms.Application.Run(form);
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
}
else if (args is ["reload-shell"] && Environment.GetEnvironmentVariable("TEST_ALLOW_EXPLORER_RESTART") == "1")
{
    var result = await new ExplorerShellReloader().ReloadAsync();
    Console.WriteLine(JsonSerializer.Serialize(result));
    if (!result.Succeeded) throw new InvalidOperationException(result.Message);
}
else if (args is ["focus", var pid])
{
    using var process = Process.GetProcessById(int.Parse(pid));
    var deadline = DateTime.UtcNow.AddSeconds(15);
    do { process.Refresh(); if (process.MainWindowHandle != IntPtr.Zero) break; Thread.Sleep(100); } while (!process.HasExited && DateTime.UtcNow < deadline);
    if (process.MainWindowHandle == IntPtr.Zero) throw new InvalidOperationException("Test process has no window.");
    Native.ShowWindow(process.MainWindowHandle, 9);
    if (!Native.SetForegroundWindow(process.MainWindowHandle))
    {
        // A nonresident input test must itself receive input before Windows permits activation.
        Native.Send(0x12, false); Native.Send(0x12, true);
        Native.SetForegroundWindow(process.MainWindowHandle);
    }
    var foregroundThread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
    var currentThread = Native.GetCurrentThreadId();
    if (foregroundThread != currentThread) Native.AttachThreadInput(currentThread, foregroundThread, true);
    try { Native.SetForegroundWindow(process.MainWindowHandle); }
    finally { if (foregroundThread != currentThread) Native.AttachThreadInput(currentThread, foregroundThread, false); }
    var focusDeadline = DateTime.UtcNow.AddSeconds(2);
    while (Native.GetForegroundWindow() != process.MainWindowHandle && DateTime.UtcNow < focusDeadline) Thread.Sleep(25);
    if (Native.GetForegroundWindow() != process.MainWindowHandle) throw new InvalidOperationException("Test foreground activation was refused.");
}
else if (args is ["hotkey", var key, var chromePath])
{
    if (key.Length != 1 || key[0] is < 'A' or > 'Z') throw new ArgumentException("A-Z only.");
    var foreground = Native.WasChromeForeground(chromePath);
    Native.Send(0x11, false); Native.Send(0x12, false); Native.Send(key[0], false);
    Thread.Sleep(60);
    var keysDown = new[] { 0x11, 0x12, (int)key[0] }.All(value => (Native.GetAsyncKeyState(value) & 0x8000) != 0);
    Native.Send(key[0], true); Native.Send(0x12, true); Native.Send(0x11, true);
    Console.WriteLine(JsonSerializer.Serialize(new { chromeWasForeground = foreground, keysDown }));
}
else if (args is ["launch-link", var link])
{
    using var launched = Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
}
else if (args is ["probe-hotkey", var probeKey])
{
    if (probeKey.Length != 1 || probeKey[0] is < 'A' or > 'Z') throw new ArgumentException("A-Z only.");
    var available = Native.RegisterHotKey(IntPtr.Zero, 0x110, 3, probeKey[0]);
    var error = Marshal.GetLastWin32Error();
    if (available) Native.UnregisterHotKey(IntPtr.Zero, 0x110);
    Console.WriteLine(JsonSerializer.Serialize(new { available, error }));
}
else if (args is ["desktop-directory"])
{
    Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
}
else if (args is ["foreground", var inspectedChrome]) Console.WriteLine(JsonSerializer.Serialize(new { chromeWasForeground = Native.WasChromeForeground(inspectedChrome) }));
else if (args is ["notify-created", var created]) new ShellChangeNotifier().NotifyItemCreated(created);
else if (args is ["notify-deleted", var deleted]) new ShellChangeNotifier().NotifyItemDeleted(deleted);
else throw new ArgumentException("focus <owned-test-pid> | hotkey <unused-A-Z> <test-chrome-path>");

internal static class Native
{
    internal static bool WasChromeForeground(string chromePath)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        try { using var process = Process.GetProcessById((int)pid); return string.Equals(process.MainModule?.FileName, chromePath, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] internal static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint key, uint mode);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData
    {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key; public ushort Scan; public uint Flags; public uint Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X; public int Y; public uint Data; public uint Flags; public uint Time; public UIntPtr Extra; }
    internal static void Send(int key, bool up)
    {
        var input = new Input { Type = 1, Data = new InputData { Keyboard = new Keyboard { Scan = (ushort)MapVirtualKey((uint)key, 0), Flags = 8u | (up ? 2u : 0u) } } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new InvalidOperationException($"SendInput failed: {Marshal.GetLastWin32Error()}");
    }
}
