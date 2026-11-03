using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.App;

internal static class DialogLayout
{
    public static void Constrain(Window window)
    {
        if(window.ReadLocalValue(FrameworkElement.StyleProperty)==DependencyProperty.UnsetValue)
            window.SetResourceReference(FrameworkElement.StyleProperty,typeof(Window));
        var minimumWidth = window.MinWidth; var minimumHeight = window.MinHeight;
        var requestedWidth = window.Width; var requestedHeight = window.Height;
        var maximumWidth = window.MaxWidth; var maximumHeight = window.MaxHeight;
        HwndSource? source = null; var adjusting = false; var queued = false;

        void Limits(WindowBounds area, uint dpi, bool initial)
        {
            var size = area.LogicalSize(dpi);
            window.MinWidth = Math.Min(minimumWidth, Math.Max(1, size.Width - 16));
            window.MinHeight = Math.Min(minimumHeight, Math.Max(1, size.Height - 16));
            window.MaxWidth = Math.Min(maximumWidth, size.Width);
            window.MaxHeight = Math.Min(maximumHeight, size.Height);
            var width = initial ? requestedWidth : window.Width; var height = initial ? requestedHeight : window.Height;
            if (double.IsFinite(width)) window.Width = Math.Clamp(width, window.MinWidth, window.MaxWidth);
            if (double.IsFinite(height)) window.Height = Math.Clamp(height, window.MinHeight, window.MaxHeight);
        }
        void Adjust(bool initial, bool position)
        {
            if (adjusting) return;
            var handle = new WindowInteropHelper(window).Handle;
            var reference = initial && window.Owner is { } owner ? new WindowInteropHelper(owner).Handle : handle;
            if (reference == IntPtr.Zero) reference = handle;
            if (reference == IntPtr.Zero) return;
            var monitor = MonitorFromWindow(reference, 2); var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return;
            var dpi = GetDpiForWindow(reference); if (dpi == 0) dpi = 96;
            var area = new WindowBounds(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
            adjusting = true;
            try
            {
                Limits(area, dpi, initial);
                if (!position || window.WindowState != WindowState.Normal || !GetWindowRect(handle, out var current)) return;
                var bounds = new WindowBounds(current.Left, current.Top, current.Right - current.Left, current.Bottom - current.Top);
                var fitted = WindowBounds.Fit(bounds, area);
                if (bounds != fitted) SetWindowPos(handle, IntPtr.Zero, fitted.Left, fitted.Top, fitted.Width, fitted.Height, 0x0004 | 0x0010 | 0x0200);
            }
            finally { adjusting = false; }
        }
        void QueueAdjustment()
        {
            if (queued || !window.IsLoaded) return;
            queued = true;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { queued = false; if (window.IsLoaded) Adjust(false, true); }));
        }
        IntPtr Hook(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Let WPF handle DPI first; clamping every drag motion would prevent crossing monitors.
            if (message is 0x02E0 or 0x007E or 0x0232) QueueAdjustment();
            if (message==0x0015 && Application.Current is App application) application.RefreshTheme();
            return IntPtr.Zero;
        }
        var fallback = SystemParameters.WorkArea;
        Limits(new(0, 0, Math.Max(1, (int)fallback.Width), Math.Max(1, (int)fallback.Height)), 96, true);
        window.SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); source?.AddHook(Hook); Adjust(true, false); };
        window.Loaded += (_, _) => Adjust(false, true);
        window.Closed += (_, _) => { if (source is not null && !source.IsDisposed) source.RemoveHook(Hook); };
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
