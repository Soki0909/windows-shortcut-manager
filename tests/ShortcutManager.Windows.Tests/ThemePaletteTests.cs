using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.Windows.Tests;

public sealed class ThemePaletteTests
{
    public static readonly ThemeSystemColors Dark=new(0xFF000000,0xFFFFFFFF,0xFF202020,0xFFFFFFFF,0xFFFFFF00,0xFF000000,0xFFC0C0C0,0xFF101010,0xFFFFFFFF);
    public static readonly ThemeSystemColors Light=new(0xFFFFFFFF,0xFF000000,0xFFF0F0F0,0xFF000000,0xFF000080,0xFFFFFFFF,0xFF606060,0xFFE0E0E0,0xFF000000);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HighContrastKeepsSystemPairsForStatusErrorsControlsAndSelection(bool dark)
    {
        var system=dark?Dark:Light;var palette=ThemePalette.Create(true,system);
        Assert.Equal(system.Window,palette["KeyErrorBrush"]);Assert.Equal(system.WindowText,palette["DangerBrush"]);
        Assert.Equal(system.Control,palette["HeaderBrush"]);Assert.Equal(system.ControlText,palette["HeaderTextBrush"]);
        Assert.Equal(system.Highlight,palette["PrimaryHoverBrush"]);Assert.Equal(system.HighlightText,palette["PrimaryTextBrush"]);
        Assert.Equal(system.GrayText,palette["DisabledTextBrush"]);
        foreach(var (text,background) in new[] {("TextBrush","SurfaceBrush"),("DangerBrush","KeyErrorBrush"),("HeaderTextBrush","HeaderBrush"),("PrimaryTextBrush","PrimaryHoverBrush"),("MenuTextBrush","MenuBrush"),("SelectionTextBrush","SelectionBrush"),("DisabledTextBrush","DisabledBrush")})
            Assert.True(Contrast(palette[text],palette[background])>=4.5,text+" contrast");
    }
    [Fact]
    public void NormalPrimaryHoverAndWarningTextRemainReadable()
    {
        var palette=ThemePalette.Create(false,Dark);
        foreach(var (text,background) in new[] {("PrimaryTextBrush","PrimaryBrush"),("PrimaryTextBrush","PrimaryHoverBrush"),("HoverTextBrush","HoverBrush"),("WarningBrush","SurfaceBrush"),("DangerBrush","SurfaceBrush")})
            Assert.True(Contrast(palette[text],palette[background])>=4.5,text+" contrast");
    }
    [Fact]
    public void ThemeSwitchDoesNotDropKeysOrMutatePreviousSnapshot()
    {
        var normal=ThemePalette.Create(false,Dark);var before=normal.ToDictionary();var contrast=ThemePalette.Create(true,Light);
        Assert.Equal(normal.Keys.Order(),contrast.Keys.Order());Assert.Equal(before,normal);
        Assert.All(contrast.Values,value=>Assert.Equal(0xFFu,value>>24));
    }
    public static double Contrast(uint a,uint b)
    {
        static double Luminance(uint color)
        {
            static double Linear(double value)=>value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
            return .2126*Linear(((color>>16)&255)/255d)+.7152*Linear(((color>>8)&255)/255d)+.0722*Linear((color&255)/255d);
        }
        var first=Luminance(a);var second=Luminance(b);return (Math.Max(first,second)+.05)/(Math.Min(first,second)+.05);
    }
}
