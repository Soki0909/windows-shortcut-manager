namespace ShortcutManager.Windows.Diagnostics;

public readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
{
    public static WindowBounds Fit(WindowBounds window, WindowBounds workArea, int margin = 8)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0) throw new ArgumentException("Work area must have positive dimensions.", nameof(workArea));
        var inset = Math.Clamp(margin, 0, Math.Max(0, (Math.Min(workArea.Width, workArea.Height) - 1) / 2));
        var width = Math.Clamp(window.Width, 1, workArea.Width - inset * 2);
        var height = Math.Clamp(window.Height, 1, workArea.Height - inset * 2);
        return new(Math.Clamp(window.Left, workArea.Left + inset, workArea.Left + workArea.Width - inset - width),
            Math.Clamp(window.Top, workArea.Top + inset, workArea.Top + workArea.Height - inset - height), width, height);
    }

    public (double Width, double Height) LogicalSize(uint dpi) => dpi == 0
        ? throw new ArgumentOutOfRangeException(nameof(dpi)) : (Width * 96d / dpi, Height * 96d / dpi);
}
