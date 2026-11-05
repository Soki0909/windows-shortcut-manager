using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using ShortcutManager.App;
using ShortcutManager.Testing;

// Test-only process: no App.Run/OnStartup, config, hooks, OS theme changes or UI input.
internal static class Program
{
    private static string progressPath=string.Empty;
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length is <2 or >3 || args[0]!="--progress" || args.Length==3 && args[2]!="--stall-after-app")return 2;
        progressPath=Path.GetFullPath(args[1]);
        var directory=Path.GetDirectoryName(progressPath)!;
        if(Path.GetFileName(progressPath)!="progress.txt" || !Path.GetFileName(directory).StartsWith("shortcut-manager-theme-",StringComparison.Ordinal)
            || !Directory.Exists(directory) || File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))return 2;
        using(var initial=new FileStream(progressPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read)) { }
        try {CheckResources(args.Length==3);Phase("completed");return 0;}
        catch(Exception error) {Console.Error.WriteLine(error);return 1;}
    }
    private static void Phase(string phase)
    {
        File.WriteAllText(progressPath,phase);
        Console.WriteLine(phase);Console.Out.Flush();
    }
    private static void Require(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);}
    private static void CheckResources(bool stall)
    {
        Phase("app.initialize");
        var app=new App();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
            if(stall){Phase("app.forced-stall");Thread.Sleep(Timeout.Infinite);}
            Phase("resources.parse");
            // Only replace the hover input condition. Use the actual App styles,
            // template/resource precedence and dynamic updates, without UI input.
            var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
            var document=XDocument.Load(Path.Combine(root,"src","ShortcutManager.App","App.xaml"));
            XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
            var body=document.Root!.Element(ns+"Application.Resources")!.Elements().Single();
            var resources=new XElement(ns+"ResourceDictionary",new XAttribute(XNamespace.Xmlns+"x",x),body.Elements());
            foreach(var trigger in resources.Descendants().Where(element=>element.Attribute("Property")?.Value=="IsMouseOver")) {trigger.SetAttributeValue("Property","Tag");trigger.SetAttributeValue("Value","hover");}
            app.Resources=(ResourceDictionary)XamlReader.Parse(resources.ToString());
            Phase("controls.create");
            var primary=new Button {Content="反映",Tag="hover",Style=(Style)app.FindResource("PrimaryButtonStyle"),Width=180,Height=44};
            var normal=new Button {Content="確認",Tag="hover",Width=180,Height=44};
            var disabled=new Button {Content="無効",IsEnabled=false,Style=(Style)app.FindResource("PrimaryButtonStyle"),Width=180,Height=44};
            var error=ThemeResources.ErrorText();error.Text="入力を確認してください";
            var panel=new StackPanel();panel.Children.Add(primary);panel.Children.Add(normal);panel.Children.Add(disabled);panel.Children.Add(error);
            var window=new Window {Content=panel};
            var settings=new SettingsWindow(null,"",@"C:\Example\config.json",@"C:\Example\Managed");
            var index=0;
            foreach(var (contrast,system) in new[] {(false,ThemeFixture.Dark),(true,ThemeFixture.Dark),(true,ThemeFixture.Light),(false,ThemeFixture.Dark)})
            {
                Phase($"palette.{index++}.apply-layout");
                ThemeResources.Apply(app.Resources,contrast,system);panel.Measure(new Size(500,400));panel.Arrange(new Rect(0,0,500,400));panel.UpdateLayout();
                foreach(var button in new[] {primary,normal,disabled})
                {
                    button.ApplyTemplate();var chrome=(Border)button.Template.FindName("Chrome",button);
                    var foreground=Value((SolidColorBrush)button.Foreground);var background=Value((SolidColorBrush)chrome.Background);
                    Require(ThemeFixture.Contrast(foreground,background)>=4.5,"Effective button foreground/background must remain readable");
                }
                Require(((SolidColorBrush)app.Resources["DangerBrush"]).Color==((SolidColorBrush)error.Foreground).Color,"Existing error foreground did not update");
                Require(((SolidColorBrush)app.Resources["TextBrush"]).Color==((SolidColorBrush)settings.Foreground).Color,"Derived Window foreground did not update");
                Require((contrast?1d:.65d)==(double)app.Resources["DisabledOpacity"],"Disabled opacity mismatch");
                if(contrast)Require(1d==((Border)disabled.Template.FindName("Chrome",disabled)).Opacity,"High contrast disabled control opacity mismatch");
            }
            Phase("controller.create");
            using(var controller=new ThemeResources(app))
            {
                ThemeResources.Apply(app.Resources,true,ThemeFixture.Dark);
                controller.QueueRefresh();controller.QueueRefresh();
                Phase("controller.refresh-dispatcher");
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));
                Require((SystemParameters.HighContrast?SystemColors.WindowColor:Colors.White)==((SolidColorBrush)app.Resources["SurfaceBrush"]).Color,"Queued refresh did not follow current OS colors");
                controller.Dispose();ThemeResources.Apply(app.Resources,true,ThemeFixture.Light);controller.QueueRefresh();
                Phase("controller.disposed-dispatcher");
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ContextIdle,new Action(()=>{}));
                Require(0xFFFFFFFFu==Value((SolidColorBrush)app.Resources["SurfaceBrush"]),"Disposed controller still updated resources");
            }
            Phase("controls.close");settings.Close();window.Close();
        }
        finally {Phase("app.shutdown");app.Shutdown();Phase("app.stopped");}
    }
    private static uint Value(SolidColorBrush brush) {var color=brush.Color;return (uint)(color.A<<24|color.R<<16|color.G<<8|color.B);}
}
