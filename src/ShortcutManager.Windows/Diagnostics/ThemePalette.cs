namespace ShortcutManager.Windows.Diagnostics;

public sealed record ThemeSystemColors(uint Window, uint WindowText, uint Control, uint ControlText,
    uint Highlight, uint HighlightText, uint GrayText, uint Menu, uint MenuText);

public static class ThemePalette
{
    public static IReadOnlyDictionary<string,uint> Create(bool highContrast, ThemeSystemColors system)
    {
        var colors = new Dictionary<string,uint>(StringComparer.Ordinal)
        {
            ["AppBackgroundBrush"]=0xFFF4F6FA, ["SurfaceBrush"]=0xFFFFFFFF, ["SurfaceMutedBrush"]=0xFFF8FAFC,
            ["PrimaryBrush"]=0xFF4F46E5, ["PrimaryHoverBrush"]=0xFF4338CA, ["PrimaryTextBrush"]=0xFFFFFFFF,
            ["TextBrush"]=0xFF172033, ["MutedTextBrush"]=0xFF667085, ["BorderBrush"]=0xFFE3E8F0,
            ["SuccessBrush"]=0xFF168766, ["WarningBrush"]=0xFF975000, ["DangerBrush"]=0xFFC83C4A,
            ["HoverBrush"]=0xFFF1F3F9, ["HoverTextBrush"]=0xFF172033, ["HoverBorderBrush"]=0xFFC9D0DC,
            ["SelectionBrush"]=0xFFC7D2FE, ["SelectionTextBrush"]=0xFF172033,
            ["GridLineBrush"]=0xFFEDF0F5, ["AlternateRowBrush"]=0xFFFAFBFD,
            ["HeaderBrush"]=0xFFF8FAFC, ["HeaderTextBrush"]=0xFF667085,
            ["StatusPanelBrush"]=0xFFEEF1F7, ["ProgressTrackBrush"]=0xFFDDE2EC, ["DebugNoticeBrush"]=0xFF9A3412,
            ["KeyDefaultBrush"]=0xFFFFFFFF, ["KeyDefaultBorderBrush"]=0xFFDCE2EB,
            ["KeyReadyBrush"]=0xFFECF8F3, ["KeyReadyBorderBrush"]=0xFFA9DCCB,
            ["KeyDisabledBrush"]=0xFFF3F4F6, ["KeyDisabledBorderBrush"]=0xFFD5D9E0,
            ["KeyErrorBrush"]=0xFFFFF0F1, ["KeyErrorBorderBrush"]=0xFFF2B8BE,
            ["KeyChangedBrush"]=0xFFF0EFFF, ["KeyChangedBorderBrush"]=0xFFC9C5FF,
            ["DisabledBrush"]=0xFFF3F4F6, ["DisabledTextBrush"]=0xFF667085, ["DisabledBorderBrush"]=0xFFD5D9E0,
            ["NativeControlBrush"]=0xFFFFFFFF, ["NativeControlTextBrush"]=0xFF172033,
            ["MenuBrush"]=0xFFF8FAFC, ["MenuTextBrush"]=0xFF172033
        };
        if (!highContrast) return colors;
        foreach(var key in new[] {"AppBackgroundBrush","SurfaceBrush","SurfaceMutedBrush","GridLineBrush","AlternateRowBrush",
            "StatusPanelBrush","ProgressTrackBrush","KeyDefaultBrush","KeyReadyBrush","KeyDisabledBrush","KeyErrorBrush","KeyChangedBrush","DisabledBrush"}) colors[key]=system.Window;
        foreach(var key in new[] {"TextBrush","MutedTextBrush","BorderBrush","SuccessBrush","WarningBrush","DangerBrush","DebugNoticeBrush",
            "KeyDefaultBorderBrush","KeyReadyBorderBrush","KeyDisabledBorderBrush","KeyErrorBorderBrush","KeyChangedBorderBrush"}) colors[key]=system.WindowText;
        colors["GridLineBrush"]=system.WindowText;
        colors["PrimaryBrush"]=colors["PrimaryHoverBrush"]=colors["HoverBrush"]=colors["SelectionBrush"]=system.Highlight;
        colors["PrimaryTextBrush"]=colors["HoverTextBrush"]=colors["HoverBorderBrush"]=colors["SelectionTextBrush"]=system.HighlightText;
        colors["DisabledTextBrush"]=colors["DisabledBorderBrush"]=system.GrayText;
        colors["HeaderBrush"]=colors["NativeControlBrush"]=system.Control;
        colors["HeaderTextBrush"]=colors["NativeControlTextBrush"]=system.ControlText;
        colors["MenuBrush"]=system.Menu; colors["MenuTextBrush"]=system.MenuText;
        return colors;
    }
}
