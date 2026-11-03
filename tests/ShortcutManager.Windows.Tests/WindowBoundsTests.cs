using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.Windows.Tests;

public sealed class WindowBoundsTests
{
    [Fact]
    public void ValidUserPositionAndSizeArePreserved()
    {
        var original=new WindowBounds(-1600,100,800,600);
        Assert.Equal(original,WindowBounds.Fit(original,new(-1920,0,1920,1080)));
    }
    [Fact]
    public void SmallWorkAreaShrinksTheWholeWindowIncludingChrome()
    {
        Assert.Equal(new(8, 8, 784, 584), WindowBounds.Fit(new(0, 0, 1180, 800), new(0, 0, 800, 600)));
    }
    [Fact]
    public void SecondaryMonitorWithNegativeCoordinatesKeepsWindowOnThatMonitor()
    {
        Assert.Equal(new(-1912, -192, 1180, 800), WindowBounds.Fit(new(-2500, -500, 1180, 800), new(-1920, -200, 1920, 1040)));
    }
    [Theory]
    [InlineData(96, 1920, 1080)]
    [InlineData(120, 1536, 864)]
    [InlineData(144, 1280, 720)]
    [InlineData(192, 960, 540)]
    public void MonitorPixelSizeIsConvertedAtItsOwnDpi(uint dpi, double width, double height)
    {
        Assert.Equal((width, height), new WindowBounds(1920, 0, 1920, 1080).LogicalSize(dpi));
    }
    [Fact]
    public void RemovingMonitorMovesOffscreenWindowIntoRemainingWorkArea()
    {
        Assert.Equal(new(72, 72, 1200, 640), WindowBounds.Fit(new(4000, 1800, 1200, 640), new(0, 0, 1280, 720)));
    }
    [Fact]
    public void TinyWorkAreasAndInvalidInputsHaveExplicitBounds()
    {
        Assert.Equal(new(0, 0, 1, 1), WindowBounds.Fit(new(100, 100, 100, 100), new(0, 0, 1, 1)));
        Assert.Throws<ArgumentException>(() => WindowBounds.Fit(new(), new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowBounds(0, 0, 100, 100).LogicalSize(0));
    }
}
