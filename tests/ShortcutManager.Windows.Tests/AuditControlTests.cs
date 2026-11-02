using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class AuditControlTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"audit-control-"+Guid.NewGuid().ToString("N"));
    private readonly Links links=new();
    private readonly ConfigRepository repository;
    private readonly ManagedShortcutApplier applier;
    private readonly ManagedShortcutAuditor auditor;
    private readonly StartupShortcutReconciler service;
    private readonly AuditControlStore controls;
    private bool failStage;
    private bool failAfterCommit;
    private int stageCalls;
    private string Managed=>Path.Combine(root,"managed");
    private string Backups=>Path.Combine(root,"backups");
    private string State=>Path.Combine(root,"state.json");
    private string ControlDirectory=>Path.Combine(root,"audit-data");
    private readonly ShortcutConfig config=new(){Shortcuts=[new(){Name="Audit",Hotkey=new(){Key="U"},Target=new WebsiteTarget{Url="https://example.com"}}]};
    public AuditControlTests()
    {
        Directory.CreateDirectory(root);repository=new(Path.Combine(root,"config.json"));var resolver=new LaunchSpecResolver(new ChromeLocator(),new DispatcherUriBuilder());
        applier=new(repository,new(),resolver,links,new(Backups,"test"),Path.Combine(root,"stage"),State,point=> {if(point=="staged"){stageCalls++;if(failStage)throw new IOException("fixture stage failure");}if(point=="committed" && failAfterCommit)links.FailRead=true;});
        auditor=new(new(),resolver,links);service=new(auditor,applier);controls=new(ControlDirectory);
        Assert.True(applier.Apply(config,Managed).Succeeded);
    }
    private static Task<IReadOnlyList<ShortcutInventoryItem>> EmptyScan()=>Task.FromResult<IReadOnlyList<ShortcutInventoryItem>>([]);
    private string Drift(string arguments="external change")
    {
        var path=Directory.GetFiles(Managed,"*.lnk").Single();var spec=JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!;
        File.WriteAllText(path,JsonSerializer.Serialize(spec with {Arguments=arguments,WindowStyle=7}));return path;
    }

    [Fact]
    public async Task AuditOnlyPreservesConfigLinkStateAndBackupsAndRecordsFieldDifferences()
    {
        var path=Drift();var before=File.ReadAllBytes(path);var original=File.ReadAllBytes(repository.Path);var state=File.ReadAllBytes(State);var count=Directory.GetFiles(Backups).Length;
        var result=await service.ReconcileAsync(config,Managed,EmptyScan,auditOnly:true,controls:controls);
        Assert.False(result.Repaired);Assert.False(result.LinksMatch);Assert.Contains(result.Before,item=>item.Field=="引数" && item.Actual=="external change");Assert.Contains(result.Before,item=>item.Field=="表示方法" && item.Actual=="7");
        Assert.Equal(before,File.ReadAllBytes(path));Assert.Equal(original,File.ReadAllBytes(repository.Path));Assert.Equal(state,File.ReadAllBytes(State));Assert.Equal(count,Directory.GetFiles(Backups).Length);
        var history=Assert.Single(controls.ReadHistory());Assert.Equal("監査のみ・保留",history.Record!.Outcome);Assert.Null(history.Record.BackupPath);
    }

    [Fact]
    public async Task FailedAutomaticRepairIsNotRepeatedUntilExplicitRepair()
    {
        Drift();failStage=true;var failed=await service.ReconcileAsync(config,Managed,EmptyScan,controls:controls);Assert.False(failed.Repaired);
        var calls=stageCalls;var count=Directory.GetFiles(Backups).Length;
        var repeated=await service.ReconcileAsync(config,Managed,EmptyScan,controls:controls);Assert.False(repeated.Repaired);Assert.Contains("再試行",repeated.Message);Assert.Equal(calls,stageCalls);Assert.Equal(count,Directory.GetFiles(Backups).Length);
        failStage=false;var explicitResult=await service.ReconcileAsync(config,Managed,EmptyScan,auditOnly:true,explicitRepair:true,controls:controls);
        Assert.True(explicitResult.Repaired);Assert.True(explicitResult.LinksMatch);Assert.Empty(explicitResult.After);Assert.True(File.Exists(explicitResult.BackupPath));
        Assert.Contains(controls.ReadHistory(),item=>item.Record?.Outcome=="修復済み" && item.Record.Before.Count==2 && item.Record.After.Count==0);
    }

    [Fact]
    public async Task DifferentActualFieldValuesAreANewAttemptAndIncompleteScanIsHeld()
    {
        Drift();failStage=true;await service.ReconcileAsync(config,Managed,EmptyScan,controls:controls);failStage=false;
        Drift("different external change");var count=Directory.GetFiles(Backups).Length;
        var held=await service.ReconcileAsync(config,Managed,EmptyScan,controls:controls,scanComplete:()=>false);Assert.False(held.Repaired);Assert.Contains("未走査",held.Message);Assert.Equal(count,Directory.GetFiles(Backups).Length);
        var result=await service.ReconcileAsync(config,Managed,EmptyScan,controls:controls);Assert.True(result.Repaired);
    }

    [Fact]
    public void MissingCorruptAndIncompletePoliciesFailSafelyAndHistoryErrorsAreIsolated()
    {
        Assert.False(controls.ReadPolicy().AuditOnly);controls.SavePolicy(true);Assert.True(new AuditControlStore(ControlDirectory).ReadPolicy().AuditOnly);
        var policyPath=Path.Combine(ControlDirectory,"audit-settings.json");File.WriteAllText(policyPath,"{\"FormatVersion\":1}");Assert.True(controls.ReadPolicy().AuditOnly);Assert.NotNull(controls.ReadPolicy().Warning);
        controls.Record(new(1,DateTimeOffset.Now,"監査のみ","差分なし",[],[],null,"ok"));
        File.WriteAllText(Path.Combine(ControlDirectory,"audit-history","broken.json"),"{\"FormatVersion\":1,\"Before\":null}");
        var history=controls.ReadHistory();Assert.Equal(2,history.Count);Assert.Single(history,item=>item.Error is not null);Assert.Single(history,item=>item.Record is not null);
        controls.SavePolicy(false);Assert.False(controls.ReadPolicy().AuditOnly);
    }

    [Fact]
    public async Task UnrecordableAutomaticIntentDoesNotWriteButExplicitRepairCanProceed()
    {
        Drift();File.WriteAllText(ControlDirectory,"blocked directory");var before=File.ReadAllBytes(repository.Path);var count=Directory.GetFiles(Backups).Length;
        var result=await service.ReconcileAsync(config,Managed,EmptyScan,controls:controls);Assert.False(result.Repaired);Assert.Equal(before,File.ReadAllBytes(repository.Path));Assert.Equal(count,Directory.GetFiles(Backups).Length);
        var explicitResult=await service.ReconcileAsync(config,Managed,EmptyScan,explicitRepair:true,controls:controls);Assert.True(explicitResult.Repaired);Assert.Contains("履歴を保存できません",explicitResult.Message);
    }

    [Fact]
    public async Task PostCommitAuditFailureDoesNotReportSuccessfulRepairAsUncommitted()
    {
        Drift();failAfterCommit=true;
        var result=await service.ReconcileAsync(config,Managed,EmptyScan,explicitRepair:true,controls:controls);
        Assert.True(result.Repaired);Assert.False(result.LinksMatch);Assert.True(File.Exists(result.BackupPath));Assert.Contains("トランザクションは完了",result.Message);Assert.Contains("修復後",result.Message);Assert.Contains(result.After,item=>item.Field=="読取り");
    }

    [Fact]
    public async Task SourceChangeDuringScanStopsBeforeRepairAndBackup()
    {
        Drift();var count=Directory.GetFiles(Backups).Length;var calls=stageCalls;
        var result=await service.ReconcileAsync(config,Managed,EmptyScan,explicitRepair:true,controls:controls,sourceUnchanged:()=>false);
        Assert.False(result.Repaired);Assert.True(result.SourceChanged);Assert.Equal(calls,stageCalls);Assert.Equal(count,Directory.GetFiles(Backups).Length);
    }

    [Fact]
    public async Task MissingExternalTargetNeverStartsRepairOrRepeatedBackups()
    {
        var file=Path.Combine(root,"external.cmd");File.WriteAllText(file,"@echo off");
        var batch=config with {Shortcuts=[config.Shortcuts.Single() with {Target=new BatTarget {Path=file}}]};Assert.True(applier.Apply(batch,Managed).Succeeded);File.Delete(file);
        var count=Directory.GetFiles(Backups).Length;var calls=stageCalls;
        for(var attempt=0;attempt<2;attempt++){var result=await service.ReconcileAsync(batch,Managed,EmptyScan,controls:controls);Assert.False(result.Repaired);Assert.Contains("見つからない",result.Message);}
        Assert.Equal(count,Directory.GetFiles(Backups).Length);Assert.Equal(calls,stageCalls);
    }

    [Fact]
    public void DetailedComparisonUsesTheSameNormalizationAsPersistedContract()
    {
        var actual=links.Read(Directory.GetFiles(Managed,"*.lnk").Single());
        var expected=new LaunchSpecResolver(new ChromeLocator(),new DispatcherUriBuilder()).Resolve(config,config.Shortcuts.Single());
        var equivalent=actual with {TargetPath=actual.TargetPath.ToUpperInvariant(),WorkingDirectory=actual.WorkingDirectory.ToUpperInvariant(),Hotkey="alt + ctrl + u",IconLocation=actual.IconLocation.Replace(",0",string.Empty,StringComparison.Ordinal)};
        Assert.True(ShellLinkComparison.Matches(equivalent,expected));Assert.Empty(ShellLinkComparison.Differences(equivalent,expected));
    }

    public void Dispose()=>Directory.Delete(root,true);
    private sealed class Links:IShellLinkService
    {
        public bool FailRead;
        public void Write(string path,ShellLinkSpec spec){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(spec));}
        public ShellLinkInfo Read(string path){if(FailRead)throw new IOException("fixture post-commit read failure");var spec=JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!;return new(path,spec.TargetPath,spec.Arguments,spec.WorkingDirectory,spec.Hotkey,spec.IconLocation,spec.Description,spec.WindowStyle);}
    }
}
