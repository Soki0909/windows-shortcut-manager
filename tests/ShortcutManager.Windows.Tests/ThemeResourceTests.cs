using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Xml.Linq;
using ShortcutManager.App;

namespace ShortcutManager.Windows.Tests;

public sealed class ThemeResourceTests
{
    [Fact]
    public void ActualWpfStylesKeepReadableHoverAndUpdateExistingControlsAcrossThemes()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try { CheckResources(); } catch(Exception error) {failure=error;}
        }) {IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)),"WPF probe timed out");
        if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static void CheckResources()
    {
        var app=new ShortcutManager.App.App();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
            // State-only fixture: use public Tag rather than changing read-only
            // IsMouseOver or sending input to the user's desktop. Everything else
            // (style/template/resource lookup and precedence) is the actual XAML.
            var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
            var document=XDocument.Load(Path.Combine(root,"src","ShortcutManager.App","App.xaml"));
            XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
            var body=document.Root!.Element(ns+"Application.Resources")!.Elements().Single();
            var resources=new XElement(ns+"ResourceDictionary",new XAttribute(XNamespace.Xmlns+"x",x),body.Elements());
            foreach(var trigger in resources.Descendants().Where(element=>element.Attribute("Property")?.Value=="IsMouseOver")) {trigger.SetAttributeValue("Property","Tag");trigger.SetAttributeValue("Value","hover");}
            app.Resources=(ResourceDictionary)XamlReader.Parse(resources.ToString());
            var primary=new Button {Content="反映",Tag="hover",Style=(Style)app.FindResource("PrimaryButtonStyle"),Width=180,Height=44};
            var normal=new Button {Content="確認",Tag="hover",Width=180,Height=44};
            var disabled=new Button {Content="無効",IsEnabled=false,Style=(Style)app.FindResource("PrimaryButtonStyle"),Width=180,Height=44};
            var error=ThemeResources.ErrorText();error.Text="入力を確認してください";
            var panel=new StackPanel();panel.Children.Add(primary);panel.Children.Add(normal);panel.Children.Add(disabled);panel.Children.Add(error);
            var window=new Window {Content=panel};
            var settings=new SettingsWindow(null,"",@"C:\Example\config.json",@"C:\Example\Managed");
            foreach(var (contrast,system) in new[] {(false,ThemePaletteTests.Dark),(true,ThemePaletteTests.Dark),(true,ThemePaletteTests.Light),(false,ThemePaletteTests.Dark)})
            {
                ThemeResources.Apply(app.Resources,contrast,system);panel.Measure(new Size(500,400));panel.Arrange(new Rect(0,0,500,400));panel.UpdateLayout();
                foreach(var button in new[] {primary,normal,disabled})
                {
                    button.ApplyTemplate();var chrome=(Border)button.Template.FindName("Chrome",button);
                    var foreground=Value((SolidColorBrush)button.Foreground);var background=Value((SolidColorBrush)chrome.Background);
                    Assert.True(ThemePaletteTests.Contrast(foreground,background)>=4.5,"Effective button foreground/background must remain readable");
                }
                Assert.Equal(((SolidColorBrush)app.Resources["DangerBrush"]).Color,((SolidColorBrush)error.Foreground).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["TextBrush"]).Color,((SolidColorBrush)settings.Foreground).Color);
                Assert.Equal(contrast?1d:.65d,(double)app.Resources["DisabledOpacity"]);
                if(contrast)Assert.Equal(1d,((Border)disabled.Template.FindName("Chrome",disabled)).Opacity);
            }
            using(var controller=new ThemeResources(app))
            {
                ThemeResources.Apply(app.Resources,true,ThemePaletteTests.Dark);
                controller.QueueRefresh();controller.QueueRefresh();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{}));
                Assert.Equal(SystemParameters.HighContrast?SystemColors.WindowColor:Colors.White,((SolidColorBrush)app.Resources["SurfaceBrush"]).Color);
                controller.Dispose();ThemeResources.Apply(app.Resources,true,ThemePaletteTests.Light);controller.QueueRefresh();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{}));
                Assert.Equal(0xFFFFFFFFu,Value((SolidColorBrush)app.Resources["SurfaceBrush"]));
            }
            settings.Close();window.Close();
        }
        finally {app.Shutdown();}
    }
    private static uint Value(SolidColorBrush brush) {var color=brush.Color;return (uint)(color.A<<24|color.R<<16|color.G<<8|color.B);}
}
