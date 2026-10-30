using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Shell;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

public sealed class WindowsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"shortcut-manager-tests-{Guid.NewGuid():N}");

    public WindowsTests() => Directory.CreateDirectory(root);
    public void Dispose() { try { Directory.Delete(root, true); } catch { } }

    [Fact]
    public void ShellLink_RoundTrips_TargetArgumentsAndHotkey()
    {
        var path = Path.Combine(root, "roundtrip.lnk");
        var service = new ShellLinkService();
        var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        service.Write(path, new(target, "\"https://example.com/\"", Environment.GetFolderPath(Environment.SpecialFolder.Windows), "CTRL+ALT+R", $"{target},0", "test"));
        var result = service.Read(path);
        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(result.TargetPath), ignoreCase: true);
        Assert.Equal("\"https://example.com/\"", result.Arguments);
        Assert.Equal("CTRL+ALT+R", result.Hotkey);
    }

    [Fact]
    public void UrlShortcut_DecodesNumericHotkey()
    {
        var path = Path.Combine(root, "sample.url");
        File.WriteAllText(path, "[InternetShortcut]\r\nURL=https://example.com/\r\nHotKey=1602\r\nIconIndex=2\r\n");
        var result = new UrlShortcutReader().Read(path);
        Assert.Equal("CTRL+ALT+B", result.Hotkey);
        Assert.Equal("https://example.com/", result.Url);
        Assert.Equal(2, result.IconIndex);
    }

    [Fact]
    public void Inventory_DeduplicatesNestedRoots()
    {
        var parent = Path.Combine(root, "parent");
        var child = Path.Combine(parent, "child");
        Directory.CreateDirectory(child);
        var roots = ShortcutInventoryScanner.DeduplicateRoots([child, parent, parent]);
        Assert.Single(roots);
        Assert.Equal(Path.GetFullPath(parent), roots[0]);
    }

    [Fact]
    public async Task Inventory_ParsesUrlAndMarksManaged()
    {
        var managed = Path.Combine(root, "managed");
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "site.url"), "[InternetShortcut]\r\nURL=https://example.com\r\nHotKey=1602\r\n");
        var scanner = new ShortcutInventoryScanner(new FakeShellLinkService(), new UrlShortcutReader());
        var result = await scanner.ScanAsync([managed], managed);
        Assert.Single(result);
        Assert.True(result[0].IsManaged);
        Assert.Equal("CTRL+ALT+B", result[0].Hotkey);
    }

    [Fact]
    public void Ownership_RejectsNonemptyUnmarkedFolder()
    {
        var managed = Path.Combine(root, "managed");
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "someone-else.lnk"), "keep");
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(managed));
        Assert.True(File.Exists(Path.Combine(managed, "someone-else.lnk")));
    }

    [Fact]
    public void Apply_StagesVerifiesBacksUpAndCommits()
    {
        var fixture = CreateFixture();
        var result = fixture.Applier.Apply(SampleConfig(), fixture.Managed);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, result.GeneratedCount);
        Assert.True(File.Exists(Path.Combine(fixture.Managed, ManagedShortcutApplier.OwnershipMarker)));
        Assert.Single(Directory.GetFiles(fixture.Managed, "*.lnk"));
        Assert.True(File.Exists(fixture.Config.Path));
        Assert.NotNull(result.BackupPath);
        Assert.Equal("apply", BackupService.Inspect(result.BackupPath!).Reason);
        Assert.Collection(
            fixture.Notifier.Events,
            item => Assert.Equal(("created", Path.Combine(fixture.Managed, "G - Entry G.lnk")), item),
            item => Assert.Equal(("changed", Path.Combine(fixture.Managed, "G - Entry G.lnk")), item));
        Assert.Contains(Path.GetFullPath(fixture.Managed), fixture.Notifier.Directories);
    }

    [Fact]
    public void Apply_IsIdempotentForSameConfig()
    {
        var fixture = CreateFixture();
        var config = SampleConfig();
        var first = fixture.Applier.Apply(config, fixture.Managed);
        var second = fixture.Applier.Apply(config, fixture.Managed);
        Assert.True(first.Succeeded && second.Succeeded);
        Assert.Equal(first.ConfigSha256, second.ConfigSha256);
        Assert.Single(Directory.GetFiles(fixture.Managed, "*.lnk"));
        Assert.Equal(
            ["created", "changed", "deleted", "created", "changed"],
            fixture.Notifier.Events.Select(item => item.Kind));
    }

    [Fact]
    public void Apply_ForcedCommitFailureRollsBackLinksAndConfig()
    {
        var fixture = CreateFixture(checkpoint => { if (checkpoint == "new-moved") throw new IOException("forced"); });
        Directory.CreateDirectory(fixture.Managed);
        File.WriteAllText(Path.Combine(fixture.Managed, ManagedShortcutApplier.OwnershipMarker), "Shortcut Manager\nschemaVersion=1\nupdatedAt=2026-10-29T00:00:00Z\n");
        var oldLink = Path.Combine(fixture.Managed, "A - Old.lnk");
        File.WriteAllText(oldLink, "old-content");
        fixture.Config.SaveAtomic(SampleConfig() with { Shortcuts = [Entry("A", new WebsiteTarget { Url = "https://example.com" }) with { Name = "Old" }] });
        var priorConfig = File.ReadAllText(fixture.Config.Path);

        var result = fixture.Applier.Apply(SampleConfig(), fixture.Managed);
        Assert.False(result.Succeeded);
        Assert.Contains("ロールバック", result.Message);
        Assert.Equal("old-content", File.ReadAllText(oldLink));
        Assert.Equal(priorConfig, File.ReadAllText(fixture.Config.Path));
        Assert.Single(Directory.GetFiles(fixture.Managed, "*.lnk"));
        Assert.Equal(
            ["deleted", "created", "deleted", "created", "changed"],
            fixture.Notifier.Events.Select(item => item.Kind));
    }

    [Fact]
    public void Apply_BlocksExternalConflictWithoutWriting()
    {
        var fixture = CreateFixture();
        var result = fixture.Applier.Apply(SampleConfig(), fixture.Managed, ["CTRL+ALT+G"]);
        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(fixture.Managed));
    }

    [Fact]
    public void Restore_ValidatesBacksUpAndReusesApplyTransaction()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Applier.Apply(SampleConfig(), fixture.Managed).Succeeded);
        var backupPath = fixture.Backups.Create("manual", fixture.Config.Path, fixture.Managed, fixture.StatePath);
        var changed = SampleConfig() with { Shortcuts = [Entry("H", new WebsiteTarget { Url = "https://example.org/" })] };
        Assert.True(fixture.Applier.Apply(changed, fixture.Managed).Succeeded);

        var restore = new RestoreService(new ConfigValidator(), fixture.Applier);
        var result = restore.Restore(backupPath, fixture.Managed);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("restore", BackupService.Inspect(result.BackupPath!).Reason);
        Assert.Equal("G", fixture.Config.Load().Shortcuts.Single().Hotkey.Key);
        Assert.StartsWith("G - ", Path.GetFileName(Directory.GetFiles(fixture.Managed, "*.lnk").Single()));
    }

    [Fact]
    public void DevelopmentApplyAndRestore_KeepProductFilesUntouchedWithCopiedOverride()
    {
        var fixture = CreateFixture();
        var product = Path.Combine(root, "product");
        Directory.CreateDirectory(product);
        var productConfig = Path.Combine(product, "config.json");
        var productLink = Path.Combine(product, "product.lnk");
        File.WriteAllText(productConfig, "production config sentinel");
        File.WriteAllText(productLink, "production shortcut sentinel");
        var debug = new ApplicationPaths(root, fixture.Config.Path, fixture.StatePath,
            Path.Combine(root, "debug.log"), Path.Combine(root, "backups"), Path.Combine(root, "stage"), fixture.Managed, IsDevelopment: true);
        var copied = SampleConfig() with { Settings = new() { ManagedShortcutDirectory = product } };
        var managed = debug.ResolveManagedDirectory(copied.Settings.ManagedShortcutDirectory);
        Assert.True(fixture.Applier.Apply(copied, managed).Succeeded);
        var backup = fixture.Backups.Create("manual", fixture.Config.Path, managed, fixture.StatePath);
        var edited = copied with { Shortcuts = [Entry("H", new WebsiteTarget { Url = "https://example.org/" })] };
        Assert.True(fixture.Applier.Apply(edited, managed).Succeeded);
        var priorConfig = File.ReadAllBytes(fixture.Config.Path);
        var priorLinkPath = Directory.GetFiles(managed, "*.lnk").Single();
        var priorLink = File.ReadAllBytes(priorLinkPath);
        var restore = new RestoreService(new ConfigValidator(), fixture.Applier);
        var blocked = restore.Restore(backup, managed, ["CTRL+ALT+G"]);
        Assert.False(blocked.Succeeded);
        Assert.Contains("競合", blocked.Message);
        Assert.Equal(priorConfig, File.ReadAllBytes(fixture.Config.Path));
        Assert.Equal(priorLink, File.ReadAllBytes(priorLinkPath));
        Assert.True(restore.Restore(backup, managed).Succeeded);
        Assert.Equal("G", fixture.Config.Load().Shortcuts.Single().Hotkey.Key);
        Assert.Equal("production config sentinel", File.ReadAllText(productConfig));
        Assert.Equal("production shortcut sentinel", File.ReadAllText(productLink));
        Assert.Equal(2, Directory.GetFiles(product).Length);
    }

    [Fact]
    public void LaunchResolver_MapsWebsiteBatAndChrome()
    {
        var chrome = Path.Combine(root, "chrome.exe"); File.WriteAllText(chrome, "fake");
        var bat = Path.Combine(root, "task.cmd"); File.WriteAllText(bat, "@echo off");
        var config = SampleConfig() with { Settings = new() { Chrome = new() { ExecutablePath = chrome, ExtensionId = ExtensionId } } };
        var resolver = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder());
        var website = resolver.Resolve(config, Entry("A", new WebsiteTarget { Url = "https://example.com" }));
        var batch = resolver.Resolve(config, Entry("Z", new BatTarget { Path = bat, WindowMode = BatWindowMode.Minimized }));
        var group = resolver.Resolve(config, Entry("R", new ChromeTabGroupTarget { GroupName = "研究" }));
        Assert.EndsWith("explorer.exe", website.TargetPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/d /s /c", batch.Arguments);
        Assert.Equal(7, batch.WindowStyle);
        Assert.Contains("group=%E7%A0%94%E7%A9%B6", group.Arguments);
    }

    [Fact]
    public async Task ShellReload_RestartsOnlyCurrentSessionShell()
    {
        var host = new FakeExplorerShellProcessHost { Shell = new(42, 7), CurrentSessionId = 7 };
        var result = await new ExplorerShellReloader(host, TimeSpan.Zero).ReloadAsync();
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal([42], host.TerminatedIds);
        Assert.Equal([42], host.WaitedIds);
        Assert.Equal(1, host.StartCount);
        Assert.Equal(43, host.Shell?.Id);
        Assert.Equal(42, result.PreviousProcessId);
        Assert.Equal(43, result.CurrentProcessId);
    }

    [Fact]
    public async Task ShellReload_UsesWindowsAutoRestartWithoutLaunchingAnotherExplorer()
    {
        var host = new FakeExplorerShellProcessHost
        {
            Shell = new(42, 7),
            CurrentSessionId = 7,
            ReplacementOnWait = new(43, 7),
        };

        var result = await new ExplorerShellReloader(host, TimeSpan.FromMilliseconds(50)).ReloadAsync();

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(0, host.StartCount);
        Assert.Equal(43, result.CurrentProcessId);
    }

    [Fact]
    public async Task ShellReload_RejectsAnotherSessionWithoutTerminatingIt()
    {
        var host = new FakeExplorerShellProcessHost { Shell = new(42, 8), CurrentSessionId = 7 };
        var result = await new ExplorerShellReloader(host).ReloadAsync();
        Assert.False(result.Succeeded);
        Assert.Equal(42, result.PreviousProcessId);
        Assert.Null(result.CurrentProcessId);
        Assert.Empty(host.TerminatedIds);
        Assert.Equal(0, host.StartCount);
    }

    [Fact]
    public async Task ShellReload_StopsSafelyWhenShellCannotBeIdentified()
    {
        var host = new FakeExplorerShellProcessHost { CurrentSessionId = 7 };
        var result = await new ExplorerShellReloader(host).ReloadAsync();
        Assert.False(result.Succeeded);
        Assert.Empty(host.TerminatedIds);
        Assert.Equal(0, host.StartCount);
    }

    [Fact]
    public void ShellProcessHost_TerminateIsIdempotentWhenExplorerAlreadyExited()
    {
        var host = new ExplorerShellProcessHost();
        var missingProcess = new ExplorerShellProcess(int.MaxValue, host.CurrentSessionId);

        var error = Record.Exception(() => host.Terminate(missingProcess));

        Assert.Null(error);
    }

    [Fact]
    public async Task ShellProcessHost_WaitIsIdempotentWhenExplorerAlreadyExited()
    {
        var host = new ExplorerShellProcessHost();
        var missingProcess = new ExplorerShellProcess(int.MaxValue, host.CurrentSessionId);

        var error = await Record.ExceptionAsync(() => host.WaitForExitAsync(missingProcess, CancellationToken.None));

        Assert.Null(error);
    }

    [Fact]
    public void RollbackFailureRetainsEveryOriginalAndReportsAllDestinations()
    {
        var failing = false;
        var fixture = CreateFixture(point => {
            if (point == "new-moved" && failing) throw new IOException("original commit failure");
            if (point == "rollback-restore") throw new UnauthorizedAccessException("injected access denied");
        });
        var initial = SampleConfig() with { Shortcuts = [Entry("G", new WebsiteTarget { Url = "https://example.com" }), Entry("H", new WebsiteTarget { Url = "https://example.org" })] };
        Assert.True(fixture.Applier.Apply(initial, fixture.Managed).Succeeded);
        var priorConfig = File.ReadAllBytes(fixture.Config.Path);
        var originals = Directory.GetFiles(fixture.Managed, "*.lnk");
        var oldLink = originals[0];
        var priorLink = File.ReadAllBytes(oldLink);
        failing = true;
        var result = fixture.Applier.Apply(SampleConfig(), fixture.Managed);
        Assert.False(result.Succeeded);
        Assert.Contains("original commit failure", result.Message);
        Assert.Contains("injected access denied", result.Message);
        Assert.Contains(oldLink, result.Message);
        var operation = Directory.GetDirectories(Path.Combine(root, "staging")).Single();
        Assert.Equal(priorLink, File.ReadAllBytes(Path.Combine(operation, "old", Path.GetFileName(oldLink))));
        Assert.Equal(priorConfig, File.ReadAllBytes(Path.Combine(operation, "prior-config.json")));
        Assert.Equal(priorConfig, File.ReadAllBytes(fixture.Config.Path));
        Assert.True(File.Exists(Path.Combine(operation, "recovery.json")));
        foreach (var original in originals)
        {
            Assert.Contains(original, result.Message);
            Assert.True(File.Exists(Path.Combine(operation, "old", Path.GetFileName(original))));
        }
        Assert.Contains("前回の復旧", fixture.Applier.Apply(initial, fixture.Managed).Message);
    }

    [Theory]
    [InlineData("keep.txt")]
    [InlineData("unmanaged.lnk")]
    [InlineData("A - Unmanaged.lnk")]
    public void AllWriteRoutesRejectUnexpectedFilesWithoutChangingState(string name)
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Applier.Apply(SampleConfig(), fixture.Managed).Succeeded);
        var backup = fixture.Backups.Create("manual", fixture.Config.Path, fixture.Managed);
        var original = File.ReadAllBytes(fixture.Config.Path);
        var extra = Path.Combine(fixture.Managed, name);
        File.WriteAllText(extra, "unmanaged sentinel");
        Assert.Throws<InvalidOperationException>(() => fixture.Applier.Apply(SampleConfig(), fixture.Managed));
        var auditor = new ManagedShortcutAuditor(new ConfigValidator(), new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder()), new FakeShellLinkService());
        Assert.Throws<InvalidOperationException>(() => auditor.Audit(SampleConfig(), fixture.Managed));
        Assert.Throws<InvalidOperationException>(() => new RestoreService(new ConfigValidator(), fixture.Applier).Restore(backup, fixture.Managed));
        Assert.Equal("unmanaged sentinel", File.ReadAllText(extra));
        Assert.Equal(original, File.ReadAllBytes(fixture.Config.Path));
    }

    [Fact]
    public void OwnershipRejectsDangerousRootInvalidMarkerAndReparsePoint()
    {
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(Path.GetPathRoot(root)!));
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe("relative"));
        var directory = Path.Combine(root, "invalid-marker");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ManagedShortcutApplier.OwnershipMarker), "owned");
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(directory));
        var target = Directory.CreateDirectory(Path.Combine(root, "target")).FullName;
        var link = Path.Combine(root, "link");
        Directory.CreateSymbolicLink(link, target);
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(link));
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(Path.Combine(link, "child")));
        Assert.Empty(Directory.GetFileSystemEntries(target));
        var missingTarget = Path.Combine(root, "missing-target");
        var dangling = Path.Combine(root, "dangling");
        Directory.CreateSymbolicLink(dangling, missingTarget);
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(dangling));
        Assert.Throws<InvalidOperationException>(() => ManagedShortcutApplier.EnsureOwnershipSafe(Path.Combine(dangling, "child")));
        Assert.False(Directory.Exists(missingTarget));
    }

    [Fact]
    public void RestoreConflictAfterBackupLeavesConfigLinksAndBackupsUnchanged()
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Applier.Apply(SampleConfig() with { Shortcuts = [Entry("G", new WebsiteTarget { Url = "https://example.com" }), Entry("I", new WebsiteTarget { Url = "https://example.org" })] }, fixture.Managed).Succeeded);
        var backup = fixture.Backups.Create("manual", fixture.Config.Path, fixture.Managed);
        Assert.True(fixture.Applier.Apply(SampleConfig() with { Shortcuts = [Entry("H", new WebsiteTarget { Url = "https://example.org" })] }, fixture.Managed).Succeeded);
        var config = File.ReadAllBytes(fixture.Config.Path);
        var link = Directory.GetFiles(fixture.Managed, "*.lnk").Single();
        var bytes = File.ReadAllBytes(link);
        var count = Directory.GetFiles(Path.Combine(root, "backups")).Length;
        var result = new RestoreService(new ConfigValidator(), fixture.Applier).Restore(backup, fixture.Managed, ["CTRL+ALT+G", "CTRL+ALT+I"]);
        Assert.False(result.Succeeded);
        Assert.Contains("CTRL+ALT+G", result.Message);
        Assert.Contains("CTRL+ALT+I", result.Message);
        Assert.Equal(config, File.ReadAllBytes(fixture.Config.Path));
        Assert.Equal(bytes, File.ReadAllBytes(link));
        Assert.Equal(count, Directory.GetFiles(Path.Combine(root, "backups")).Length);
    }

    [Theory]
    [InlineData("broken config")]
    [InlineData("{}")]
    public async Task CorruptConfigBlocksApplyUntilReloadRestoreOrExplicitInitialization(string brokenConfig)
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Applier.Apply(SampleConfig(), fixture.Managed).Succeeded);
        var valid = File.ReadAllBytes(fixture.Config.Path);
        var backup = fixture.Backups.Create("manual", fixture.Config.Path, fixture.Managed);
        var link = Directory.GetFiles(fixture.Managed, "*.lnk").Single();
        var linkBytes = File.ReadAllBytes(link);
        File.WriteAllText(fixture.Config.Path, brokenConfig);
        var paths = new ApplicationPaths(root, fixture.Config.Path, fixture.StatePath, Path.Combine(root, "log.txt"), Path.Combine(root, "backups"), Path.Combine(root, "staging"), fixture.Managed);
        var vm = new ShortcutManager.App.MainViewModel(paths, fixture.Config, new(), new(new ChromeLocator(), new DispatcherUriBuilder()), new(), new(new FakeShellLinkService(), new()), new(), fixture.Backups, fixture.Applier, new(new ConfigValidator(), fixture.Applier), new(paths.LogPath), new ExplorerShellReloader(new FakeExplorerShellProcessHost()));
        Assert.True(vm.RecoveryRequired);
        Assert.False(vm.CanApply);
        Assert.False(await vm.ApplyAsync());
        Assert.Equal(linkBytes, File.ReadAllBytes(link));
        Assert.Equal(brokenConfig, File.ReadAllText(fixture.Config.Path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(fixture.Config.Path)!, "*.broken-*.json"));
        File.WriteAllBytes(fixture.Config.Path, valid);
        vm.Load();
        Assert.False(vm.RecoveryRequired);
        File.WriteAllText(fixture.Config.Path, brokenConfig);
        vm.Load();
        await vm.RestoreAsync(backup);
        Assert.False(vm.RecoveryRequired);
        Assert.Single(vm.Candidate.Shortcuts);
        File.WriteAllText(fixture.Config.Path, brokenConfig);
        vm.Load();
        vm.InitializeRecovery();
        Assert.True(vm.CanApply);
        Assert.Empty(vm.Candidate.Shortcuts);
        Assert.Equal(brokenConfig, File.ReadAllText(fixture.Config.Path));
        Assert.Equal(linkBytes, File.ReadAllBytes(link));
    }

    [Theory]
    [InlineData("config.json")]
    [InlineData("backup.json")]
    public void RestoreRejectsTamperingBeforeAnyWrite(string entryName)
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Applier.Apply(SampleConfig(), fixture.Managed).Succeeded);
        var backup = fixture.Backups.Create("manual", fixture.Config.Path, fixture.Managed);
        using (var archive = System.IO.Compression.ZipFile.Open(backup, System.IO.Compression.ZipArchiveMode.Update))
        {
            archive.GetEntry(entryName)!.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write("{}");
        }
        var config = File.ReadAllBytes(fixture.Config.Path);
        var link = Directory.GetFiles(fixture.Managed, "*.lnk").Single();
        var bytes = File.ReadAllBytes(link);
        Assert.Throws<InvalidDataException>(() => new RestoreService(new ConfigValidator(), fixture.Applier).Restore(backup, fixture.Managed));
        Assert.Equal(config, File.ReadAllBytes(fixture.Config.Path));
        Assert.Equal(bytes, File.ReadAllBytes(link));
    }

    [Fact]
    public void ChromeHotkeyUsesDirectChromeToggleAndKeepsLegacyUriCompatible()
    {
        var chrome = Path.Combine(root, "chrome.exe");
        File.WriteAllText(chrome, "fixture");
        var config = SampleConfig() with { Settings = new() { Chrome = new() { ExecutablePath = chrome, ExtensionId = ExtensionId } } };
        var entry = Entry("F", new ChromeTabGroupTarget { GroupName = "Test \"quoted\" \\" });
        var profile = Path.Combine(root, "profile with spaces");
        var spec = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder(), profile).Resolve(config, entry);
        Assert.Equal(chrome, spec.TargetPath);
        Assert.StartsWith("--user-data-dir=\"", spec.Arguments);
        Assert.Contains("action=toggle-group", spec.Arguments);
        Assert.Contains("%22quoted%22", spec.Arguments);
        var normal = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder()).Resolve(config, entry);
        Assert.Equal(chrome, normal.TargetPath);
        Assert.DoesNotContain("--user-data-dir", normal.Arguments);
        Assert.DoesNotContain("foreground", normal.Arguments);
        Assert.Contains("action=focus-group", new DispatcherUriBuilder().BuildFocusGroupUri(ExtensionId, "a&b").AbsoluteUri);
        Assert.Contains("group=a%26b", new DispatcherUriBuilder().BuildToggleGroupUri(ExtensionId, "a&b").AbsoluteUri);
        Assert.EndsWith("\\\\\"", LaunchSpecResolver.Quote("tail\\"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyRejectsOversizedPriorSnapshotsBeforeCreatingStagingOrWritingLinks(bool oversizedState)
    {
        var fixture = CreateFixture();
        fixture.Config.SaveAtomic(SampleConfig());
        var path = oversizedState ? fixture.StatePath : fixture.Config.Path;
        var length = (oversizedState ? BackupArchive.MaxEntryBytes : BackupArchive.MaxConfigBytes) + 1;
        File.WriteAllBytes(path, new byte[length]);
        Assert.Throws<InvalidDataException>(() => fixture.Applier.Apply(SampleConfig(), fixture.Managed));
        Assert.Equal(length, new FileInfo(path).Length);
        Assert.False(Directory.Exists(fixture.Managed));
        Assert.False(Directory.Exists(Path.Combine(root, "staging")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestorePreservesSupportForUtf8AndUtf16BomConfigs(bool utf16)
    {
        var fixture = CreateFixture();
        Assert.True(fixture.Applier.Apply(SampleConfig(), fixture.Managed).Succeeded);
        var json = File.ReadAllText(fixture.Config.Path);
        File.WriteAllText(fixture.Config.Path, json, utf16 ? System.Text.Encoding.Unicode : new System.Text.UTF8Encoding(true));
        var backup = fixture.Backups.Create("manual", fixture.Config.Path, fixture.Managed);
        Assert.True(fixture.Applier.Apply(SampleConfig() with { Shortcuts = [Entry("H", new WebsiteTarget { Url = "https://example.org" })] }, fixture.Managed).Succeeded);
        var result = new RestoreService(new ConfigValidator(), fixture.Applier).Restore(backup, fixture.Managed);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("G", fixture.Config.Load().Shortcuts.Single().Hotkey.Key);
    }

    private Fixture CreateFixture(Action<string>? checkpoint = null)
    {
        var configPath = Path.Combine(root, "app", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var repo = new ConfigRepository(configPath);
        var managed = Path.Combine(root, "managed");
        var backup = new BackupService(Path.Combine(root, "backups"), "0.1.0");
        var statePath = Path.Combine(root, "app", "state.json");
        var notifier = new TrackingShellChangeNotifier();
        var applier = new ManagedShortcutApplier(repo, new ConfigValidator(), new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder()), new FakeShellLinkService(), backup, Path.Combine(root, "staging"), statePath, checkpoint, notifier);
        return new(repo, managed, applier, backup, statePath, notifier);
    }

    [Fact]
    public void StartupAudit_RepairsMissingChangedAndObsoleteLinksThroughBackupTransaction()
    {
        var links = new ShellLinkService();
        var validator = new ConfigValidator();
        var resolver = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder());
        var repository = new ConfigRepository(Path.Combine(root, "audit", "config.json"));
        var managed = Path.Combine(root, "audit-managed");
        var backups = new BackupService(Path.Combine(root, "audit-backups"), "0.1.0");
        var applier = new ManagedShortcutApplier(repository, validator, resolver, links, backups, Path.Combine(root, "audit-stage"), Path.Combine(root, "audit-state.json"));
        var auditor = new ManagedShortcutAuditor(validator, resolver, links);
        var config = SampleConfig();
        Assert.False(auditor.Audit(config, managed).Matches);
        Assert.True(applier.Apply(config, managed).Succeeded);
        var path = Directory.GetFiles(managed, "*.lnk").Single();
        var original = File.ReadAllBytes(path);
        Assert.True(auditor.Audit(config, managed).Matches);
        Assert.Equal(original, File.ReadAllBytes(path));

        var expected = resolver.Resolve(config, config.Shortcuts.Single());
        links.Write(path, new(expected.TargetPath, "\"https://wrong.example/\"", expected.WorkingDirectory, "CTRL+ALT+H", expected.IconLocation, expected.Description));
        File.Copy(path, Path.Combine(managed, "Z - Obsolete.lnk"));
        Assert.Equal(2, auditor.Audit(config, managed).Differences.Count);
        var repair = applier.Apply(config, managed);
        Assert.True(repair.Succeeded, repair.Message);
        Assert.True(File.Exists(repair.BackupPath));
        Assert.True(auditor.Audit(config, managed).Matches);
        Assert.Single(Directory.GetFiles(managed, "*.lnk"));

        File.Delete(path);
        Assert.False(auditor.Audit(config, managed).Matches);
        Assert.True(applier.Apply(config, managed).Succeeded);
        Assert.True(auditor.Audit(config, managed).Matches);
    }

    [Fact]
    public void StartupAudit_RefusesUnownedAndUnexpectedFilesWithoutWriting()
    {
        var managed = Path.Combine(root, "audit-unsafe");
        Directory.CreateDirectory(managed);
        var userFile = Path.Combine(managed, "keep.txt");
        File.WriteAllText(userFile, "user data");
        var auditor = new ManagedShortcutAuditor(new ConfigValidator(), new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder()), new ShellLinkService());
        Assert.Throws<InvalidOperationException>(() => auditor.Audit(SampleConfig(), managed));
        File.WriteAllText(Path.Combine(managed, ManagedShortcutApplier.OwnershipMarker), "Shortcut Manager\nschemaVersion=1\nupdatedAt=2026-10-29T00:00:00Z\n");
        Assert.Throws<InvalidOperationException>(() => auditor.Audit(SampleConfig(), managed));
        Assert.Equal("user data", File.ReadAllText(userFile));
    }

    [Fact]
    public async Task StartupReconcile_RepairsOnlyDriftAndStopsOnExternalConflict()
    {
        var links = new ShellLinkService();
        var validator = new ConfigValidator();
        var resolver = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder());
        var repository = new ConfigRepository(Path.Combine(root, "startup", "config.json"));
        var managed = Path.Combine(root, "startup-managed");
        var backupDirectory = Path.Combine(root, "startup-backups");
        var backups = new BackupService(backupDirectory, "0.1.0");
        var applier = new ManagedShortcutApplier(repository, validator, resolver, links, backups, Path.Combine(root, "startup-stage"), Path.Combine(root, "startup-state.json"));
        var service = new StartupShortcutReconciler(new ManagedShortcutAuditor(validator, resolver, links), applier);
        var config = SampleConfig();
        Task<IReadOnlyList<ShortcutInventoryItem>> EmptyScan() => Task.FromResult<IReadOnlyList<ShortcutInventoryItem>>([]);
        var repaired = await service.ReconcileAsync(config, managed, EmptyScan);
        Assert.True(repaired.Repaired, repaired.Message);
        Assert.True(File.Exists(repaired.BackupPath));
        var backupCount = Directory.GetFiles(backupDirectory).Length;
        var clean = await service.ReconcileAsync(config, managed, EmptyScan);
        Assert.False(clean.Repaired);
        Assert.Equal(0, clean.Differences);
        Assert.Equal(backupCount, Directory.GetFiles(backupDirectory).Length);
        var link = Directory.GetFiles(managed, "*.lnk").Single();
        File.Delete(link);
        var conflicts = new ShortcutInventoryItem[] { new("external.lnk", ".lnk", "external", "CTRL+ALT+G", "target", "", "", "", false) };
        var blocked = await service.ReconcileAsync(config, managed, () => Task.FromResult<IReadOnlyList<ShortcutInventoryItem>>(conflicts));
        Assert.False(blocked.Repaired);
        Assert.Contains("競合", blocked.Message);
        Assert.False(File.Exists(link));
        Assert.Equal(backupCount, Directory.GetFiles(backupDirectory).Length);
        Assert.True((await service.ReconcileAsync(config, managed, EmptyScan)).Repaired);
    }

    private static ShortcutConfig SampleConfig() => new() { Settings = new() { Chrome = new() { ExtensionId = ExtensionId } }, Shortcuts = [Entry("G", new WebsiteTarget { Url = "https://github.com/" })] };
    private static ShortcutDefinition Entry(string key, ShortcutTarget target) => new() { Name = $"Entry {key}", Hotkey = new() { Key = key }, Target = target };
    private const string ExtensionId = "abcdefghijklmnopabcdefghijklmnop";
    private sealed record Fixture(ConfigRepository Config, string Managed, ManagedShortcutApplier Applier, BackupService Backups, string StatePath, TrackingShellChangeNotifier Notifier);

    private sealed class TrackingShellChangeNotifier : IShellChangeNotifier
    {
        public List<(string Kind, string Path)> Events { get; } = [];
        public List<string> Directories { get; } = [];
        public void NotifyItemCreated(string path) => Events.Add(("created", Path.GetFullPath(path)));
        public void NotifyItemDeleted(string path) => Events.Add(("deleted", Path.GetFullPath(path)));
        public void NotifyItemChanged(string path) => Events.Add(("changed", Path.GetFullPath(path)));
        public void NotifyDirectoryChanged(string path) => Directories.Add(Path.GetFullPath(path));
    }

    private sealed class FakeShellLinkService : IShellLinkService
    {
        private readonly Dictionary<string, ShellLinkSpec> values = new(StringComparer.OrdinalIgnoreCase);
        public void Write(string path, ShellLinkSpec spec) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(spec)); values[path] = spec; }
        public ShellLinkInfo Read(string path)
        {
            if (!values.TryGetValue(path, out var value)) value = System.Text.Json.JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path)) ?? throw new InvalidDataException("Unknown fake shortcut.");
            return new(path, value.TargetPath, value.Arguments, value.WorkingDirectory, value.Hotkey, value.IconLocation, value.Description, value.WindowStyle);
        }
    }

    private sealed class FakeExplorerShellProcessHost : IExplorerShellProcessHost
    {
        public int CurrentSessionId { get; init; }
        public ExplorerShellProcess? Shell { get; set; }
        public List<int> TerminatedIds { get; } = [];
        public List<int> WaitedIds { get; } = [];
        public ExplorerShellProcess? ReplacementOnWait { get; init; }
        public int StartCount { get; private set; }

        public ExplorerShellProcess? GetShellProcess() => Shell;
        public void Terminate(ExplorerShellProcess process) { TerminatedIds.Add(process.Id); Shell = null; }
        public Task WaitForExitAsync(ExplorerShellProcess process, CancellationToken cancellationToken) { WaitedIds.Add(process.Id); Shell = ReplacementOnWait; return Task.CompletedTask; }
        public void StartExplorer() { StartCount++; Shell = new(43, CurrentSessionId); }
    }
}
