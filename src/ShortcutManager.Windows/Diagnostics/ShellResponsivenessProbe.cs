using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.Diagnostics;

public sealed record WindowProbeResult(int ProcessId, string ProcessName, long WindowHandle, double ElapsedMs, int Error, string Status);
public sealed record ShellProbeReport(int Examined, int Responded, int Skipped, bool Complete, double ElapsedMs, IReadOnlyList<WindowProbeResult> Candidates);

// On-demand and read-only. WM_GETHOTKEY is the window query used by Shell;
// this does not enumerate RegisterHotKey registrations or install a hook.
public sealed class ShellResponsivenessProbe
{
    public ShellProbeReport Run(CancellationToken cancellationToken = default)
    {
        var windows = new List<nint>();
        var enumerated = EnumWindows((window, _) => { windows.Add(window); return windows.Count < 1024; }, 0);
        var timer = Stopwatch.StartNew();
        var candidates = new List<WindowProbeResult>();
        var examined = 0; var responded = 0; var skipped = 0;
        foreach (var window in windows)
        {
            if (cancellationToken.IsCancellationRequested || timer.ElapsedMilliseconds >= 4000) break;
            GetWindowThreadProcessId(window, out var processId);
            if (processId == Environment.ProcessId || processId == 0) { examined++; skipped++; continue; }
            var sample = Stopwatch.StartNew();
            Marshal.SetLastPInvokeError(0);
            var ok = SendMessageTimeout(window, 0x0033, 0, 0, 0x0001 | 0x0002 | 0x0020, 50, out _);
            var error = Marshal.GetLastPInvokeError();
            examined++;
            if (ok != 0) { responded++; continue; }
            if (!IsWindow(window)) { skipped++; continue; }
            string name;
            try { using var process = Process.GetProcessById((int)processId); name = process.ProcessName; }
            catch { name = "(exited/inaccessible)"; }
            candidates.Add(new((int)processId, name, window, sample.Elapsed.TotalMilliseconds, error, error == 5 ? "access-denied" : error == 1460 ? "timeout" : "no-response"));
        }
        return new(examined, responded, skipped, enumerated && examined == windows.Count && windows.Count < 1024, timer.Elapsed.TotalMilliseconds, candidates);
    }

    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)] private static extern nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
}
