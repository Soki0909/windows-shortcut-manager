using ShortcutManager.Core.Models;
using ShortcutManager.Core.Validation;

namespace ShortcutManager.Core.Tests;

public sealed class TargetPathValidationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),$"path-validation-{Guid.NewGuid():N}");
    public TargetPathValidationTests() => Directory.CreateDirectory(root);
    private static ShortcutConfig Config(ShortcutTarget target) => new() {Shortcuts=[new() {Name="Path fixture",Hotkey=new() {Key="U"},Target=target}]};

    [Fact]
    public void RelativeFileAndWorkingDirectoryAreRejected()
    {
        var result=new ConfigValidator().Validate(Config(new ApplicationTarget {Value="relative.exe",WorkingDirectory="relative-folder"}),false);
        Assert.Contains(result.Issues,item=>item.Code=="relative-target-path"); Assert.Contains(result.Issues,item=>item.Code=="relative-working-directory");
    }

    [Fact]
    public void DirectoryTargetAndFileAsWorkingDirectoryAreDistinguished()
    {
        var file=Path.Combine(root,"file.exe"); File.WriteAllText(file,"fixture");
        var result=new ConfigValidator().Validate(Config(new ApplicationTarget {Value=root,WorkingDirectory=file}));
        Assert.Contains(result.Issues,item=>item.Code=="target-is-directory"); Assert.Contains(result.Issues,item=>item.Code=="missing-working-directory");
    }

    [Fact]
    public void JapaneseAndSpacesAreAcceptedWithoutChangingPath()
    {
        var folder=Path.Combine(root,"日本語の 作業フォルダ"); Directory.CreateDirectory(folder); var file=Path.Combine(folder,"app fixture.exe"); File.WriteAllText(file,"fixture");
        Assert.True(new ConfigValidator().Validate(Config(new ApplicationTarget {Value=file,WorkingDirectory=folder})).IsValid);
        var config=Config(new BatTarget {Path=Path.Combine(folder,"missing.cmd"),WorkingDirectory=folder});
        Assert.Contains(new ConfigValidator().Validate(config).Issues,issue=>issue.Code=="missing-bat");
    }

    [Fact]
    public void UncSyntaxIsAcceptedWithoutProbingNetworkWhenFilesystemChecksAreOff()
    {
        if(!OperatingSystem.IsWindows()) return;
        var config=Config(new ApplicationTarget {Value="\\\\server\\share\\日本語 app.exe",WorkingDirectory="\\\\server\\share\\作業 フォルダ"});
        Assert.True(new ConfigValidator().Validate(config,false).IsValid);
    }

    public void Dispose() => Directory.Delete(root,true);
}
