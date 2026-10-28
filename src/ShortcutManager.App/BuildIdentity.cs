using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShortcutManager.App;

internal static class BuildIdentity
{
#if DEBUG
    public static bool IsDevelopment => true;
#else
    public static bool IsDevelopment => false;
#endif
    public static string DisplayName => IsDevelopment ? "Shortcut Manager [DEBUG]" : "Shortcut Manager";
    public static string Version => typeof(App).Assembly.GetName().Version!.ToString(3);

    public static void ConfigureTaskbar()
    {
        // Keep the installed product's Shell identity unchanged. The development
        // process gets its own group, even when both editions are open.
        if (IsDevelopment) Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID("ShortcutManager.Desktop.Debug"));
    }

    public static ImageSource CreateDevelopmentIcon()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new ImageDrawing(new BitmapImage(new Uri("pack://application:,,,/Assets/shortcut_manager_logo_64.png")), new Rect(0, 0, 32, 32)));
        drawing.Children.Add(new GeometryDrawing(Brushes.Orange, new Pen(Brushes.White, 1), new EllipseGeometry(new Point(25, 25), 7, 7)));
        var label = new FormattedText("D", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, Brushes.Black, 1);
        drawing.Children.Add(new GeometryDrawing(Brushes.Black, null, label.BuildGeometry(new Point(21, 17))));
        var icon = new DrawingImage(drawing);
        icon.Freeze();
        return icon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
