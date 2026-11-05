using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.Testing;

internal static class ThemeFixture
{
    internal static readonly ThemeSystemColors Dark=new(0xFF000000,0xFFFFFFFF,0xFF202020,0xFFFFFFFF,0xFFFFFF00,0xFF000000,0xFFC0C0C0,0xFF101010,0xFFFFFFFF);
    internal static readonly ThemeSystemColors Light=new(0xFFFFFFFF,0xFF000000,0xFFF0F0F0,0xFF000000,0xFF000080,0xFFFFFFFF,0xFF606060,0xFFE0E0E0,0xFF000000);

    internal static double Contrast(uint a,uint b)
    {
        static double Luminance(uint color)
        {
            static double Linear(double value)=>value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
            return .2126*Linear(((color>>16)&255)/255d)+.7152*Linear(((color>>8)&255)/255d)+.0722*Linear((color&255)/255d);
        }
        var first=Luminance(a);var second=Luminance(b);return (Math.Max(first,second)+.05)/(Math.Min(first,second)+.05);
    }
}
