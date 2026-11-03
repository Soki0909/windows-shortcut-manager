using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.App;

public sealed class ThemeResources : IDisposable
{
    private readonly Application application;
    private int queued;
    private bool disposed;
    public ThemeResources(Application application)
    {
        this.application=application; Refresh(); SystemParameters.StaticPropertyChanged+=SettingsChanged;
    }
    private void SettingsChanged(object? sender,PropertyChangedEventArgs args)=>QueueRefresh();
    public void QueueRefresh()
    {
        if(disposed || application.Dispatcher.HasShutdownStarted || Interlocked.Exchange(ref queued,1)!=0)return;
        application.Dispatcher.BeginInvoke(new Action(()=> {Interlocked.Exchange(ref queued,0);if(!disposed)Refresh();}));
    }
    private void Refresh() => Apply(application.Resources,SystemParameters.HighContrast,SystemPalette());
    public static ThemeSystemColors SystemPalette() => new(
        Value(SystemColors.WindowColor),Value(SystemColors.WindowTextColor),Value(SystemColors.ControlColor),Value(SystemColors.ControlTextColor),
        Value(SystemColors.HighlightColor),Value(SystemColors.HighlightTextColor),Value(SystemColors.GrayTextColor),Value(SystemColors.MenuColor),Value(SystemColors.MenuTextColor));
    private static uint Value(Color color)=>(uint)(color.A<<24|color.R<<16|color.G<<8|color.B);
    public static TextBlock ErrorText(Thickness? margin=null,double fontSize=13)
    {
        var text=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=margin??new Thickness(0),FontSize=fontSize};
        text.SetResourceReference(TextBlock.ForegroundProperty,"DangerBrush");return text;
    }
    public static void Apply(ResourceDictionary resources,bool highContrast,ThemeSystemColors system)
    {
        foreach(var item in ThemePalette.Create(highContrast,system))
        {
            var color=item.Value;
            var brush=new SolidColorBrush(Color.FromArgb((byte)(color>>24),(byte)(color>>16),(byte)(color>>8),(byte)color));brush.Freeze();
            resources[item.Key]=brush;
        }
        resources["DisabledOpacity"]=highContrast?1d:.65d;
        resources["PressedOpacity"]=highContrast?1d:.78d;
        resources["SecondaryTextOpacity"]=highContrast?1d:.75d;
    }
    public void Dispose() {disposed=true;SystemParameters.StaticPropertyChanged-=SettingsChanged;}
}

public sealed class ThemeResourceDictionary : ResourceDictionary
{
    public ThemeResourceDictionary()=>ThemeResources.Apply(this,SystemParameters.HighContrast,ThemeResources.SystemPalette());
}
