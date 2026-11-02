using System.Windows;

namespace ShortcutManager.App;

internal static class DialogLayout
{
    public static void Constrain(Window window)
    {
        var area=SystemParameters.WorkArea;
        var width=Math.Max(240,area.Width-32); var height=Math.Max(180,area.Height-32);
        window.MinWidth=Math.Min(window.MinWidth,width); window.MinHeight=Math.Min(window.MinHeight,height);
        window.Width=Math.Min(window.Width,width); window.Height=Math.Min(window.Height,height);
        window.MaxWidth=width; window.MaxHeight=height;
    }
}
