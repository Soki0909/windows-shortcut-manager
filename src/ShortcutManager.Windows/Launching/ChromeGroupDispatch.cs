using System.Diagnostics;
using System.Runtime.InteropServices;
using ShortcutManager.Core.Protocol;

namespace ShortcutManager.Windows.Launching;

/// <summary>One-shot entry point: sample foreground before launching Chrome; no window or hook.</summary>
public static class ChromeGroupDispatch
{
    public static bool WasChromeForeground(string chromePath)
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var executable = process.MainModule?.FileName;
            return executable is not null && Path.GetFullPath(executable).Equals(Path.GetFullPath(chromePath), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; } // Uncertain foreground must open, never hide the requested group.
    }

    public static void Launch(string chromePath, string extensionId, string groupName, bool chromeWasForeground)
    {
        var uri = new DispatcherUriBuilder().BuildToggleGroupUri(extensionId, groupName, chromeWasForeground);
        var start = new ProcessStartInfo(chromePath) { UseShellExecute = false };
        start.ArgumentList.Add(uri.AbsoluteUri);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Chromeを起動できませんでした。");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
