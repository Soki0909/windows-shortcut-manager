using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Shell;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class EditorStateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"editor-state-{Guid.NewGuid():N}");
    private readonly TestLinks links = new();
    private readonly ResultReloader reloader = new();
    private readonly ConfigRepository repository;
    private readonly BackupService backups;
    private readonly LaunchSpecResolver resolver = new(new ChromeLocator(), new DispatcherUriBuilder());
    private readonly ManagedShortcutApplier applier;
    private string Managed => Path.Combine(root, "managed");
    private string State => Path.Combine(root, "state.json");

    public EditorStateTests()
    {
        Directory.CreateDirectory(root);
        repository = new(Path.Combine(root,"config.json"));
        backups = new(Path.Combine(root,"backups"),"test");
        applier = new(repository,new(),resolver,links,backups,Path.Combine(root,"stage"),State);
    }
    private App.MainViewModel Model() => new(new(root,repository.Path,State,Path.Combine(root,"log.txt"),Path.Combine(root,"backups"),Path.Combine(root,"stage"),Managed), repository,new(),resolver,new(),new(links,new()),new(),backups,applier,new(new(),applier),new(Path.Combine(root,"log.txt")),reloader);
    private static ShortcutDefinition Entry(string key = "U") => new() { Name="Fixture " + key, Hotkey=new() {Key=key},Target=new WebsiteTarget {Url="https://example.com"} };
    private void Save(params ShortcutDefinition[] entries) => Assert.True(applier.Apply(new() {Shortcuts=[..entries]},Managed).Succeeded);

    [Fact]
    public async Task NoChangeApplyIsDisabledAndDoesNotCreateBackupOrClaimPhysicalConfirmation()
    {
        var vm = Model();
        Assert.False(vm.CanApply); Assert.False(vm.CanEditSelected);
        Assert.False(await vm.ApplyAsync());
        var entry = Entry(); vm.Upsert(entry); vm.Selected=vm.Shortcuts.Single();
        Assert.True(vm.CanApply); Assert.True(vm.CanEditSelected); Assert.Equal(1,vm.PendingChangeCount);
        Assert.True(await vm.ApplyAsync());
        Assert.False(vm.IsDirty); Assert.False(vm.CanApply); Assert.True(vm.ShellReflectionPending);
        Assert.DoesNotContain("確認済み",vm.Shortcuts.Single().StateLabel);
        var count=Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").Length;
        Assert.False(await vm.ApplyAsync());
        Assert.Equal(count,Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").Length);
    }

    [Fact]
    public void PerEntryChangesRevertsAndSelectionAreIndependent()
    {
        var first=Entry(); var second=Entry("J"); Save(first,second); var vm=Model();
        vm.Selected=vm.Shortcuts.Single(row=>row.Definition.Id==first.Id);
        vm.Upsert(first with { Name="Changed" });
        Assert.Equal(1,vm.PendingChangeCount);
        Assert.Equal("未保存",vm.Shortcuts.Single(row=>row.Definition.Id==first.Id).StateLabel);
        Assert.NotEqual("未保存",vm.Shortcuts.Single(row=>row.Definition.Id==second.Id).StateLabel);
        Assert.Equal(first.Id,vm.Selected!.Definition.Id);
        Assert.True(vm.TopRowKeys.Single(tile=>tile.Key=="U").IsSelected);
        vm.Upsert(first);
        Assert.False(vm.IsDirty); Assert.False(vm.CanApply); Assert.Equal(0,vm.PendingChangeCount);
        vm.UpdateSettings(vm.Candidate.Settings.Chrome.ExecutablePath,vm.Candidate.Settings.Chrome.ExtensionId);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task PersistedAuditKeepsEditedCandidateAndWritesNoConfigLinksStateOrBackup()
    {
        var entry=Entry(); Save(entry); var vm=Model(); vm.Upsert(entry with {Name="Unsaved"});
        var candidate=ConfigJson.Serialize(vm.Candidate); var config=File.ReadAllBytes(repository.Path); var state=File.ReadAllBytes(State);
        var path=Directory.GetFiles(Managed,"*.lnk").Single(); var bytes=File.ReadAllBytes(path); var count=Directory.GetFiles(Path.Combine(root,"backups")).Length;
        await vm.AuditSavedAsync(new(new(),resolver,links));
        Assert.Equal(candidate,ConfigJson.Serialize(vm.Candidate)); Assert.True(vm.IsDirty);
        Assert.Equal(config,File.ReadAllBytes(repository.Path)); Assert.Equal(state,File.ReadAllBytes(State)); Assert.Equal(bytes,File.ReadAllBytes(path));
        Assert.Equal(count,Directory.GetFiles(Path.Combine(root,"backups")).Length);
        Assert.Contains("保存済みのみ監査",vm.AuditNotice); Assert.False(vm.CanConfirmHotkey);
    }

    [Fact]
    public async Task DeclinedOrFailedReloadStaysPendingAndSuccessfulReloadDoesNotImplyPhysicalProof()
    {
        var vm=Model(); vm.Upsert(Entry()); Assert.True(await vm.ApplyAsync()); vm.Selected=vm.Shortcuts.Single();
        Assert.Equal(0,reloader.Calls); // The GUI can decline without calling the reloader.
        await vm.ReloadShellAsync(); Assert.True(vm.ShellReflectionPending);
        reloader.Succeeded=true; await vm.ReloadShellAsync(); Assert.False(vm.ShellReflectionPending);
        Assert.Contains("再起動済み",vm.ShellNotice); Assert.DoesNotContain("確認済み",vm.Shortcuts.Single().StateLabel);
        Assert.True(vm.CanConfirmHotkey); vm.ConfirmSelectedHotkey();
        Assert.Contains("今回の申告",vm.Shortcuts.Single().StateLabel);
        var changed=vm.Selected!.Definition with {Name="Changed again"}; vm.Upsert(changed);
        Assert.False(vm.CanConfirmHotkey); Assert.Equal("未保存",vm.Shortcuts.Single().StateLabel);
        Assert.True(await vm.ApplyAsync()); Assert.DoesNotContain("確認済み",vm.Shortcuts.Single().StateLabel);
    }

    [Fact]
    public async Task ExternalConfigChangeStopsStaleApplyWithoutLosingCandidate()
    {
        var entry=Entry(); Save(entry); var vm=Model(); vm.Upsert(entry with {Name="Candidate edit"});
        var candidate=ConfigJson.Serialize(vm.Candidate);
        repository.SaveAtomic(new() {Shortcuts=[entry with {Name="External edit"}]}); var before=File.ReadAllBytes(repository.Path);
        Assert.False(await vm.ApplyAsync()); Assert.False(vm.CanApply);
        Assert.Equal(before,File.ReadAllBytes(repository.Path)); Assert.Equal(candidate,ConfigJson.Serialize(vm.Candidate));
        Assert.Contains("外部",vm.EditingNotice);
        vm.Load(); Assert.False(vm.IsDirty); Assert.Equal("External edit",vm.Shortcuts.Single().Name);
    }

    [Fact]
    public async Task LinkDriftAuditDoesNotRepairAndClearsPriorPhysicalDeclaration()
    {
        var entry=Entry(); Save(entry); var vm=Model(); await vm.AuditSavedAsync(new(new(),resolver,links)); vm.Selected=vm.Shortcuts.Single(); vm.ConfirmSelectedHotkey();
        var path=Directory.GetFiles(Managed,"*.lnk").Single(); var original=JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!;
        File.WriteAllText(path,JsonSerializer.Serialize(original with {Arguments="drift"})); var drift=File.ReadAllBytes(path);
        await vm.AuditSavedAsync(new(new(),resolver,links));
        Assert.Equal(drift,File.ReadAllBytes(path)); Assert.False(vm.CanConfirmHotkey); Assert.DoesNotContain("確認済み",vm.Shortcuts.Single().StateLabel);
    }

    [Fact]
    public void DuplicateKeyRequiresExplicitEntrySelectionAndHighlightsTheSameKey()
    {
        var disabled=Entry() with {Name="Disabled alternative",Enabled=false}; var enabled=Entry(); Save(disabled,enabled); var vm=Model();
        var tile=vm.TopRowKeys.Single(item=>item.Key=="U");
        Assert.Equal(2,tile.EntryCount); Assert.Contains("2件",tile.Name);
        Assert.False(vm.SelectKey("U")); Assert.Null(vm.Selected);
        vm.Selected=vm.Shortcuts.Single(row=>row.Definition.Id==disabled.Id);
        Assert.True(tile.IsSelected); Assert.True(vm.SelectKey("U")); Assert.Equal(disabled.Id,vm.Selected.Definition.Id);
        vm.Upsert(disabled with {Name="Edited alternative"});
        Assert.Equal(disabled.Id,vm.Selected!.Definition.Id);
        Assert.True(vm.TopRowKeys.Single(item=>item.Key=="U").IsSelected);
        Assert.True(vm.SelectKey("F")); Assert.Null(vm.Selected); // Empty key must not keep the previous edit target.
    }

    public void Dispose() => Directory.Delete(root,true);
    private sealed class ResultReloader : IExplorerShellReloader
    {
        public bool Succeeded; public int Calls;
        public Task<ExplorerShellReloadResult> ReloadAsync(CancellationToken cancellationToken=default) { Calls++; return Task.FromResult(new ExplorerShellReloadResult(Succeeded,"fixture")); }
    }
    private sealed class TestLinks : IShellLinkService
    {
        public void Write(string path,ShellLinkSpec spec) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,JsonSerializer.Serialize(spec)); }
        public ShellLinkInfo Read(string path) { var value=JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!; return new(path,value.TargetPath,value.Arguments,value.WorkingDirectory,value.Hotkey,value.IconLocation,value.Description,value.WindowStyle); }
    }
}
