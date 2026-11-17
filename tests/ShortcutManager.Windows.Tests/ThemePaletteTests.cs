using ShortcutManager.Windows.Diagnostics;
using ShortcutManager.Testing;

namespace ShortcutManager.Windows.Tests;

public sealed class ThemePaletteTests
{
    public static readonly ThemeSystemColors Dark=ThemeFixture.Dark;
    public static readonly ThemeSystemColors Light=ThemeFixture.Light;
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
    public static double Contrast(uint a,uint b)=>ThemeFixture.Contrast(a,b);
}
