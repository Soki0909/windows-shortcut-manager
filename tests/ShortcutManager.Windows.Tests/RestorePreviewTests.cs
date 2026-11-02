using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class RestorePreviewTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"restore-preview-"+Guid.NewGuid().ToString("N"));
    private readonly ConfigRepository repository;
    private readonly BackupService backups;
    private readonly ManagedShortcutApplier applier;
    private readonly RestoreService restore;
    private string Managed=>Path.Combine(root,"managed");
    private string Backups=>Path.Combine(root,"backups");
    public RestorePreviewTests()
    {
        Directory.CreateDirectory(root);repository=new(Path.Combine(root,"config.json"));backups=new(Backups,"test");
        applier=new(repository,new(),new(new ChromeLocator(),new DispatcherUriBuilder()),new Links(),backups,Path.Combine(root,"stage"),Path.Combine(root,"state.json"));restore=new(new(),applier);
    }
    private static ShortcutDefinition Entry(string key="U")=>new(){Name="Entry "+key,Hotkey=new(){Key=key},Target=new WebsiteTarget {Url="https://example.com"}};
    private string Archive(ShortcutConfig config,string reason="manual") {repository.SaveAtomic(config);return backups.Create(reason,repository.Path,Managed);}

    [Fact]
    public void PreviewShowsEntryAndSettingsDifferencesWithoutWriting()
    {
        var old=Entry();var removed=Entry("J");var archive=Archive(new(){Shortcuts=[old,removed]});
        var current=new ShortcutConfig {Shortcuts=[old with {Name="Changed",Hotkey=new(){Key="K"}},Entry("F")]};repository.SaveAtomic(current);
        var bytes=File.ReadAllBytes(repository.Path);var count=Directory.GetFiles(Backups).Length;
        var preview=restore.Prepare(archive,current,managedDirectory:Managed);
        Assert.True(preview.CanRestore);Assert.Contains(preview.Changes,change=>change.Change=="削除");Assert.Contains(preview.Changes,change=>change.Change=="追加");Assert.Contains(preview.Changes,change=>change.Change=="変更");Assert.Contains(preview.Changes,change=>change.Change=="設定");
        Assert.Contains("CTRL+ALT+K",preview.Description);Assert.Contains("CTRL+ALT+U",preview.Description);
        Assert.Equal(bytes,File.ReadAllBytes(repository.Path));Assert.Equal(count,Directory.GetFiles(Backups).Length);Assert.False(Directory.Exists(Managed));
    }

    [Fact]
    public void PartialModeRetainsDefinitionsButDisablesOnlyUnavailableOrConflictingEntries()
    {
        var missing=Entry() with {Target=new BatTarget {Path=Path.Combine(root,"missing.cmd")}};var conflict=Entry("J");
        var archive=Archive(new(){Shortcuts=[missing,conflict,Entry("K")]});
        var entire=restore.Prepare(archive,new(),["CTRL+ALT+J"],managedDirectory:Managed);
        Assert.False(entire.CanRestore);Assert.Contains(entire.Issues,issue=>issue.Path==((BatTarget)missing.Target).Path);Assert.Single(entire.ExternalConflicts);
        var partial=restore.Prepare(archive,new(),["CTRL+ALT+J"],RestoreMode.DisableUnavailable,Managed);
        Assert.True(partial.CanRestore);Assert.Equal(3,partial.Config.Shortcuts.Count);Assert.Equal(2,partial.DisabledEntries.Count);Assert.Equal(2,partial.DisabledReasons.Count);Assert.Single(partial.Config.Shortcuts,entry=>entry.Enabled);
        Assert.True(restore.Restore(archive,Managed,["CTRL+ALT+J"],RestoreMode.DisableUnavailable,partial.ArchiveSha256,ConfigJson.Serialize(partial.Config)).Succeeded);
        Assert.Equal(2,repository.Load().Shortcuts.Count(entry=>!entry.Enabled));
    }

    [Fact]
    public void PartialModeDoesNotHideInvalidKeyConfiguration()
    {
        var archive=Archive(new(){Shortcuts=[Entry() with {Hotkey=new(){Key="1"}}]});
        var preview=restore.Prepare(archive,new(),mode:RestoreMode.DisableUnavailable,managedDirectory:Managed);
        Assert.False(preview.CanRestore);Assert.Empty(preview.DisabledEntries);Assert.Contains(preview.Issues,issue=>issue.Code=="invalid-key");
    }

    [Fact]
    public void ChangedArchiveOrNewPartialDecisionStopsBeforeBackupOrWrite()
    {
        var entry=Entry();var archive=Archive(new(){Shortcuts=[entry]});
        var preview=restore.Prepare(archive,new(),mode:RestoreMode.DisableUnavailable,managedDirectory:Managed);
        var current=File.ReadAllBytes(repository.Path);var count=Directory.GetFiles(Backups).Length;
        var changed=restore.Restore(archive,Managed,["CTRL+ALT+U"],RestoreMode.DisableUnavailable,preview.ArchiveSha256,ConfigJson.Serialize(preview.Config));
        Assert.False(changed.Succeeded);Assert.Equal(current,File.ReadAllBytes(repository.Path));Assert.Equal(count,Directory.GetFiles(Backups).Length);
        var other=Archive(new(){Shortcuts=[Entry("J")]});File.Copy(other,archive,true);var latest=File.ReadAllBytes(repository.Path);count=Directory.GetFiles(Backups).Length;
        Assert.False(restore.Restore(archive,Managed,expectedArchiveSha256:preview.ArchiveSha256).Succeeded);Assert.Equal(latest,File.ReadAllBytes(repository.Path));Assert.Equal(count,Directory.GetFiles(Backups).Length);
    }

    [Fact]
    public void RestoredSettingsKeepCurrentManagedDirectoryAndNeverTouchOriginalDirectory()
    {
        var foreign=Path.Combine(root,"foreign");Directory.CreateDirectory(foreign);var keep=Path.Combine(foreign,"keep.txt");File.WriteAllText(keep,"untouched");
        var archive=Archive(new(){Settings=new(){ManagedShortcutDirectory=foreign},Shortcuts=[Entry()]});
        var preview=restore.Prepare(archive,new(),managedDirectory:Managed);
        Assert.Equal(foreign,preview.OriginalManagedDirectory);Assert.Equal(Managed,preview.TargetManagedDirectory);
        var result=restore.Restore(archive,Managed,expectedArchiveSha256:preview.ArchiveSha256,expectedConfigJson:ConfigJson.Serialize(preview.Config));
        Assert.True(result.Succeeded);Assert.Equal(Managed,repository.Load().Settings.ManagedShortcutDirectory);Assert.Equal("untouched",File.ReadAllText(keep));Assert.Single(Directory.GetFiles(foreign));Assert.Equal("restore",BackupService.Inspect(result.BackupPath!).Reason);
    }

    [Fact]
    public void HistoryKeepsCorruptAndManualEntriesWithoutTreatingClaimedAutomaticAsVerified()
    {
        var manual=Archive(new());var unverified=Archive(new(),"apply");File.Delete(unverified+".automatic.json");
        Assert.True(applier.Apply(new(){Shortcuts=[Entry()]},Managed).Succeeded);
        File.WriteAllText(Path.Combine(Backups,"broken.zip"),"broken");var before=Directory.GetFiles(Backups).Select(File.ReadAllBytes).Select(Convert.ToHexString).ToArray();
        var history=BackupHistory.Read(Backups);
        Assert.Equal(4,history.Count);Assert.Single(history,entry=>entry.Error is not null);Assert.Single(history,entry=>entry.VerifiedAutomatic);
        Assert.False(history.Single(entry=>entry.Path==manual).VerifiedAutomatic);Assert.False(history.Single(entry=>entry.Path==unverified).VerifiedAutomatic);
        Assert.Equal(before,Directory.GetFiles(Backups).Select(File.ReadAllBytes).Select(Convert.ToHexString).ToArray());
    }

    public void Dispose()=>Directory.Delete(root,true);
    private sealed class Links:IShellLinkService
    {
        public void Write(string path,ShellLinkSpec spec){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(spec));}
        public ShellLinkInfo Read(string path){var spec=JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!;return new(path,spec.TargetPath,spec.Arguments,spec.WorkingDirectory,spec.Hotkey,spec.IconLocation,spec.Description,spec.WindowStyle);}
    }
}
