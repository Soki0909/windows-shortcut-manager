using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

public sealed class InventoryMigrationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"inventory-migration-"+Guid.NewGuid().ToString("N"));
    public InventoryMigrationTests()=>Directory.CreateDirectory(root);
    private ShortcutInventoryItem Item(string type=".lnk",string hotkey="CTRL+ALT+U")=>new(Path.Combine(root,"source"+type),type,"Source",hotkey,Path.Combine(root,"app.exe"),"--preserved-in-source",root,"",false);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task SourceLinkAndAllItsLaunchConditionsArePreserved(int windowStyle)
    {
        var item=Item();var links=new ShellLinkService();
        links.Write(item.SourcePath,new(item.Target,item.Arguments,item.WorkingDirectory,"CTRL+ALT+U",item.Target+",0", "source metadata",windowStyle));
        var bytes=File.ReadAllBytes(item.SourcePath);
        var scanned=Assert.Single((await new ShortcutInventoryScanner(links,new()).ScanAsync([root],Path.Combine(root,"managed"))));
        var proposal=new ShortcutImporter().CreateProposal(scanned,"J");
        var target=Assert.IsType<ApplicationTarget>(proposal.Target);
        Assert.Equal(ApplicationLaunchMode.Shortcut,target.LaunchMode);Assert.Equal(item.SourcePath,target.Value);Assert.Empty(target.Arguments);
        var spec=new LaunchSpecResolver(new ChromeLocator(),new DispatcherUriBuilder()).Resolve(new(),proposal);
        Assert.EndsWith("explorer.exe",spec.TargetPath,StringComparison.OrdinalIgnoreCase);Assert.Equal(LaunchSpecResolver.Quote(item.SourcePath),spec.Arguments);
        Assert.Equal(bytes,File.ReadAllBytes(item.SourcePath));Assert.Equal(windowStyle,links.Read(item.SourcePath).WindowStyle);
        Assert.Equal(item.Hotkey,links.Read(item.SourcePath).Hotkey);
    }

    [Fact]
    public void ExplicitChoicesExcludeExternalAndCandidateKeysButExplainDisabledDefinitions()
    {
        var config=new ShortcutConfig {Shortcuts=[new() {Name="Active",Hotkey=new() {Key="J"}},new() {Name="Disabled",Enabled=false,Hotkey=new(){Key="K"}}]};
        var choices=ImportKeyChoices.Build(config,[Item()]);
        Assert.Equal("QWERTYUIOPASDFGHJKLZXCVBNM",string.Concat(choices.Select(choice=>choice.Key)));
        Assert.False(choices.Single(choice=>choice.Key=="U").Available);Assert.False(choices.Single(choice=>choice.Key=="J").Available);
        Assert.True(choices.Single(choice=>choice.Key=="K").Available);Assert.Contains("無効",choices.Single(choice=>choice.Key=="K").Label);
        Assert.Contains(Item().SourcePath,choices.Single(choice=>choice.Key=="U").Label);
        Assert.False(ImportKeyChoices.CanImport(Item() with {ParseError="denied"}));
        Assert.Throws<InvalidOperationException>(()=>new ShortcutImporter().CreateProposal(Item() with {IsManaged=true},"J"));
    }

    [Fact]
    public async Task ScanKeepsReadableItemsAndReportsFailedDirectoryMissingRootAndBadLinks()
    {
        var denied=Directory.CreateDirectory(Path.Combine(root,"denied")).FullName;
        File.WriteAllText(Path.Combine(root,"good.url"),"[InternetShortcut]\nURL=https://example.com\nHotKey=1602\n");
        File.WriteAllText(Path.Combine(root,"bad.lnk"),"not a link");
        var scanner=new ShortcutInventoryScanner(new RejectLinks(),new(),path=>path==denied ? throw new UnauthorizedAccessException("fixture denied") : Directory.GetFileSystemEntries(path));
        var report=await scanner.ScanReportAsync([root,denied,root+"-missing"],Path.Combine(root,"managed"));
        Assert.Equal(2,report.Items.Count);Assert.Equal(1,report.ReadFailureCount);Assert.False(report.IsComplete);
        Assert.Contains(report.Issues,issue=>issue.Path==denied);Assert.Contains(report.Issues,issue=>issue.Path.EndsWith("missing"));
        Assert.Contains("全件とは限りません",report.Summary);Assert.Equal(2,report.Roots.Count);
    }

    [Fact]
    public async Task CancellationStopsScanWithoutChangingFiles()
    {
        var path=Path.Combine(root,"keep.txt");File.WriteAllText(path,"original");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new ShortcutInventoryScanner(new RejectLinks(),new()).ScanReportAsync([root],root,cancel.Token));
        Assert.Equal("original",File.ReadAllText(path));
    }

    [Fact]
    public async Task ReparseDirectoriesAndRootsInsideThemAreNotTraversed()
    {
        var scope=Directory.CreateDirectory(Path.Combine(root,"scope")).FullName;
        var outside=Directory.CreateDirectory(Path.Combine(root,"outside","child")).FullName;
        File.WriteAllText(Path.Combine(outside,"hidden.url"),"[InternetShortcut]\nURL=https://example.com\n");
        var link=Path.Combine(scope,"link");Directory.CreateSymbolicLink(link,Path.GetDirectoryName(outside)!);
        var scanner=new ShortcutInventoryScanner(new RejectLinks(),new());
        var report=await scanner.ScanReportAsync([scope],root);
        Assert.Empty(report.Items);Assert.Contains(report.Issues,issue=>issue.Reason=="reparse-point");
        var nested=await scanner.ScanReportAsync([Path.Combine(link,"child")],root);
        Assert.Empty(nested.Items);Assert.Contains(nested.Issues,issue=>issue.Reason=="reparse-point");
        Assert.True(File.Exists(Path.Combine(outside,"hidden.url")));
    }

    [Fact]
    public void SearchIncludesSourceErrorsKeysAndImportability()
    {
        Assert.True(InventorySearch.Matches(Item(),"SOURCE",true,true));Assert.False(InventorySearch.Matches(Item(),"missing",false,false));
        Assert.True(InventorySearch.Matches(Item() with {ParseError="denied"},"denied",false,false));
        Assert.False(InventorySearch.Matches(Item() with {ParseError="denied"},"",false,true));
        Assert.False(InventorySearch.Matches(Item(hotkey:""),"",true,false));
    }

    [Fact]
    public void ReorderedConfigLoadsFromRepositoryWithoutRewriting()
    {
        var json="{\"schemaVersion\":1,\"dispatchProtocolVersion\":1,\"settings\":{},\"shortcuts\":[{\"name\":\"Order\",\"hotkey\":{\"key\":\"U\"},\"target\":{\"url\":\"https://example.com\",\"kind\":\"website\"}}]}";
        var repository=new ConfigRepository(Path.Combine(root,"config.json"));File.WriteAllText(repository.Path,json);
        Assert.IsType<WebsiteTarget>(repository.Load().Shortcuts.Single().Target);Assert.Equal(json,File.ReadAllText(repository.Path));
    }

    public void Dispose()=>Directory.Delete(root,true);
    private sealed class RejectLinks:IShellLinkService
    {
        public ShellLinkInfo Read(string path)=>throw new IOException("fixture malformed link");
        public void Write(string path,ShellLinkSpec spec)=>throw new InvalidOperationException("Scanner must never write");
    }
}
