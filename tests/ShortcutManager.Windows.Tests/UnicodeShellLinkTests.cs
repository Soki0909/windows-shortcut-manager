using System.Runtime.InteropServices;
using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Tests;

public sealed class UnicodeShellLinkTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"unicode-link-"+Guid.NewGuid().ToString("N"));
    public UnicodeShellLinkTests()=>Directory.CreateDirectory(root);

    [Theory]
    [InlineData("監査のみ")]
    [InlineData("🧪🚀")]
    [InlineData("測試éΩ🧪")]
    public void EveryPersistedFieldRoundTripsThroughUnicodePaths(string name)
    {
        var directory=Directory.CreateDirectory(Path.Combine(root,name+" folder")).FullName;
        var target=Path.Combine(directory,"対象🧪.exe");File.WriteAllText(target,"fixture");
        var path=Path.Combine(directory,name+".lnk");var service=new ShellLinkService();
        var spec=new ShellLinkSpec(target,"--title \"日本語🚀\"",directory,"CTRL+ALT+U",target+",-7","説明🧪",7);
        service.Write(path,spec);var before=File.ReadAllBytes(path);var modified=File.GetLastWriteTimeUtc(path);
        var actual=service.Read(path);
        Assert.Equal(target,actual.TargetPath);Assert.Equal(spec.Arguments,actual.Arguments);Assert.Equal(directory,actual.WorkingDirectory);Assert.Equal(spec.Hotkey,actual.Hotkey);Assert.Equal(spec.IconLocation,actual.IconLocation);Assert.Equal(spec.Description,actual.Description);Assert.Equal(7,actual.WindowStyle);
        Assert.Equal(before,File.ReadAllBytes(path));Assert.Equal(modified,File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void ExistingWScriptLinkRenamedOutsideCodePageIsReadWithoutChangingIt()
    {
        var path=Path.Combine(root,"ascii.lnk");var renamed=Path.Combine(root,"リンク🧪.lnk");
        var shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;object? link=null;
        var target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
        try {link=((dynamic)shell).CreateShortcut(path);((dynamic)link).TargetPath=target;((dynamic)link).Arguments="--legacy";((dynamic)link).Hotkey="CTRL+ALT+R";((dynamic)link).Save();}
        finally {if(link is not null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
        File.Move(path,renamed);var bytes=File.ReadAllBytes(renamed);var actual=new ShellLinkService().Read(renamed);
        Assert.Equal(target,actual.TargetPath,ignoreCase:true);Assert.Equal("--legacy",actual.Arguments);Assert.Equal("CTRL+ALT+R",actual.Hotkey);Assert.Equal(bytes,File.ReadAllBytes(renamed));
    }

    [Fact]
    public void NativeAsciiLinkRemainsReadableByWScriptAndEmptyHotkeyStaysEmpty()
    {
        var path=Path.Combine(root,"plain.lnk");var target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
        var service=new ShellLinkService();service.Write(path,new(target,"--example",root,"",target+",0","fixture",7));
        Assert.Empty(service.Read(path).Hotkey);
        var shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;object? link=null;
        try {link=((dynamic)shell).CreateShortcut(path);Assert.Equal(target,(string)((dynamic)link).TargetPath,ignoreCase:true);Assert.Equal("--example",(string)((dynamic)link).Arguments);Assert.Equal(7,(int)((dynamic)link).WindowStyle);Assert.Equal("",(string)((dynamic)link).Hotkey);}
        finally {if(link is not null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
    }

    [Fact]
    public void MalformedLinkIsAnErrorInsteadOfAValidEmptyTarget()
    {
        var path=Path.Combine(root,"broken.lnk");File.WriteAllText(path,"broken");Assert.ThrowsAny<COMException>(()=>new ShellLinkService().Read(path));
    }
    public void Dispose()=>Directory.Delete(root,true);
}
