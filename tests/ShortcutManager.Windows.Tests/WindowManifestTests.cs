using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace ShortcutManager.Windows.Tests;

public sealed class WindowManifestTests
{
    [Fact]
    public void BuiltAppEmbedsPerMonitorDpiAndNeverRequestsElevationOrUiAccess()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var name=configuration=="Debug"?"ShortcutManager.Debug.exe":"ShortcutManager.exe";
        var path=Path.Combine(root,"src","ShortcutManager.App","bin",configuration,"net10.0-windows",name);
        var module=LoadLibraryEx(path,IntPtr.Zero,0x0002|0x0020);Assert.NotEqual(IntPtr.Zero,module);
        try
        {
            var resource=FindResource(module,new IntPtr(1),new IntPtr(24));Assert.NotEqual(IntPtr.Zero,resource);
            var size=SizeofResource(module,resource);Assert.InRange(size,1u,1024u*1024u);
            var pointer=LockResource(LoadResource(module,resource));Assert.NotEqual(IntPtr.Zero,pointer);
            var bytes=new byte[(int)size];Marshal.Copy(pointer,bytes,0,bytes.Length);
            var document=XDocument.Parse(Encoding.UTF8.GetString(bytes).TrimEnd('\0'));
            Assert.Equal("PerMonitorV2, PerMonitor",document.Descendants().Single(element=>element.Name.LocalName=="dpiAwareness").Value);
            var privilege=document.Descendants().Single(element=>element.Name.LocalName=="requestedExecutionLevel");
            Assert.Equal("asInvoker",privilege.Attribute("level")?.Value);Assert.Equal("false",privilege.Attribute("uiAccess")?.Value);
        }
        finally {FreeLibrary(module);}
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="FindResourceW")] private static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)] private static extern bool FreeLibrary(IntPtr module);
}
