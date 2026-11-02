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

    [Fact]
    public void PreviewResolvesWithoutLaunchingOrSavingAndRejectsChangedDefinition()
    {
        var file=Path.Combine(root,"preview.exe"); File.WriteAllText(file,"not executable");
        var entry=Entry() with {Target=new ApplicationTarget {Value=file,Arguments="--example"}};
        var vm=Model(); vm.Upsert(entry);
        var before=ConfigJson.Serialize(vm.Candidate);
        var spec=vm.PrepareLaunch(entry);
        Assert.NotNull(spec); Assert.Equal(file,spec.TargetPath); Assert.Equal("--example",spec.Arguments);
        Assert.False(File.Exists(repository.Path)); Assert.Equal(before,ConfigJson.Serialize(vm.Candidate));
        Assert.False(vm.ExecutePreview(entry,spec with {Arguments="changed"}));
        vm.Upsert(entry with {Name="Changed after preview"});
        Assert.Null(vm.PrepareLaunch(entry)); Assert.False(vm.ExecutePreview(entry,spec));
        Assert.False(File.Exists(repository.Path)); Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DisabledDraftMustHaveARealTargetBeforePreview()
    {
        var entry=Entry() with {Enabled=false,Target=new ApplicationTarget {Value=Path.Combine(root,"missing.exe")}};
        var vm=Model(); vm.Upsert(entry);
        Assert.Null(vm.PrepareLaunch(entry)); Assert.False(File.Exists(repository.Path));
        Assert.Contains("見つからない",vm.Status);
    }

    [Fact]
    public async Task ImportRequiresExplicitFreeKeyAndApplyRechecksCustomSourceConflicts()
    {
        var source=Path.Combine(root,"original.url");File.WriteAllText(source,"[InternetShortcut]\nURL=https://example.com\nHotKey=1621\n");
        var bytes=File.ReadAllBytes(source);var vm=Model();var item=Assert.Single(await vm.ScanAsync([root]));
        Assert.False(vm.Import(item));Assert.Empty(vm.Shortcuts);Assert.False(vm.Import(item,"U"));
        Assert.True(vm.Import(item,"J"));Assert.False(File.Exists(repository.Path));
        var definition=vm.Candidate.Shortcuts.Single();vm.Upsert(definition with {Hotkey=new() {Key="U"}});
        Assert.False(await vm.ApplyAsync());Assert.Contains(vm.ConflictingItems,conflict=>conflict.SourcePath==source);
        Assert.Equal(bytes,File.ReadAllBytes(source));Assert.False(File.Exists(repository.Path));
    }

    [Fact]
    public void ImportDoesNotExceedCapacityEvenWhenDisabledEntriesLeaveKeysAvailable()
    {
        var vm=Model();foreach(var key in Enumerable.Range('A',26))vm.Upsert(Entry(((char)key).ToString()) with {Enabled=false});
        var item=new Windows.Inventory.ShortcutInventoryItem(Path.Combine(root,"source.url"),".url","Source","","https://example.com","","","",false);
        Assert.False(vm.Import(item,"U"));Assert.Equal(26,vm.Shortcuts.Count);Assert.Contains("最大26",vm.Status);
    }

    [Fact]
    public async Task RestorePreviewKeepsCandidateAndStopsWhenCurrentConfigChangesAfterConfirmation()
    {
        var entry=Entry();Save(entry);var archive=backups.Create("manual",repository.Path,Managed,State);var vm=Model();vm.Upsert(entry with {Name="Unsaved"});
        var candidate=ConfigJson.Serialize(vm.Candidate);var before=File.ReadAllBytes(repository.Path);var state=File.ReadAllBytes(State);var count=Directory.GetFiles(Path.Combine(root,"backups")).Length;
        var prepared=await vm.PrepareRestoreAsync(archive);
        Assert.Equal(candidate,ConfigJson.Serialize(vm.Candidate));Assert.Equal(before,File.ReadAllBytes(repository.Path));Assert.Equal(state,File.ReadAllBytes(State));Assert.Equal(count,Directory.GetFiles(Path.Combine(root,"backups")).Length);
        repository.SaveAtomic(new(){Shortcuts=[entry with {Name="External"}]});var external=File.ReadAllBytes(repository.Path);
        Assert.False(await vm.RestoreAsync(archive,prepared));Assert.Equal(external,File.ReadAllBytes(repository.Path));Assert.Equal(candidate,ConfigJson.Serialize(vm.Candidate));Assert.Equal(count,Directory.GetFiles(Path.Combine(root,"backups")).Length);
    }

    [Fact]
    public async Task UndoUsesOnlyThisSessionsLastApplyAndCreatesRestoreBackup()
    {
        var entry=Entry();Save(entry);var vm=Model();Assert.False(vm.CanUndoLastApply);
        vm.Upsert(entry with {Name="Changed"});Assert.True(await vm.ApplyAsync());Assert.True(vm.CanUndoLastApply);
        var prepared=await vm.PrepareUndoRestoreAsync();Assert.Equal(entry.Name,prepared.Preview.Config.Shortcuts.Single().Name);
        var count=Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").Length;
        Assert.True(await vm.RestoreAsync(prepared.BackupPath,prepared));Assert.Equal(entry.Name,vm.Shortcuts.Single().Name);Assert.False(vm.CanUndoLastApply);
        Assert.Equal(count+1,Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").Length);Assert.True(vm.ShellReflectionPending);
    }

    [Fact]
    public async Task UndoRejectsChangedBackupAndExternalCurrentState()
    {
        var entry=Entry();Save(entry);var vm=Model();vm.Upsert(entry with {Name="Changed"});Assert.True(await vm.ApplyAsync());
        var path=vm.LastApplyBackupPath!;var replacement=backups.Create("manual",repository.Path,Managed,State);File.Copy(replacement,path,true);
        var before=File.ReadAllBytes(repository.Path);var count=Directory.GetFiles(Path.Combine(root,"backups")).Length;
        await Assert.ThrowsAsync<InvalidDataException>(()=>vm.PrepareUndoRestoreAsync());Assert.Equal(before,File.ReadAllBytes(repository.Path));Assert.Equal(count,Directory.GetFiles(Path.Combine(root,"backups")).Length);
        repository.SaveAtomic(new(){Shortcuts=[entry with {Name="External"}]});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>vm.PrepareUndoRestoreAsync());Assert.Contains("外部",vm.EditingNotice);Assert.False(vm.CanUndoLastApply);
    }

    [Fact]
    public async Task ExplicitRepairUsesSavedDefinitionAndKeepsUnsavedCandidateAndAuditPolicySeparate()
    {
        var entry=Entry();Save(entry);var vm=Model();var original=File.ReadAllBytes(repository.Path);vm.AuditOnly=true;
        Assert.Equal(original,File.ReadAllBytes(repository.Path));Assert.True(Model().AuditOnly);
        vm.Upsert(entry with {Name="Unsaved edit"});var candidate=ConfigJson.Serialize(vm.Candidate);
        var path=Directory.GetFiles(Managed,"*.lnk").Single();var spec=JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!;File.WriteAllText(path,JsonSerializer.Serialize(spec with {Arguments="drift"}));
        var count=Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").Length;
        Assert.True(await vm.AuditStartupAsync(new(new(),resolver,links),explicitRepair:true));
        Assert.Equal(candidate,ConfigJson.Serialize(vm.Candidate));Assert.True(vm.IsDirty);Assert.Equal(entry.Name,repository.Load().Shortcuts.Single().Name);Assert.Equal(spec.Arguments,links.Read(path).Arguments);Assert.Equal(spec.Description,links.Read(path).Description);Assert.True(vm.ShellReflectionPending);
        Assert.Equal(count+1,Directory.GetFiles(Path.Combine(root,"backups"),"*.zip").Length);
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
