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
        File.WriteAllText(Path.Combine(fixture.Managed, ManagedShortcutApplier.OwnershipMarker), "owned");
        var oldLink = Path.Combine(fixture.Managed, "old.lnk");
        File.WriteAllText(oldLink, "old-content");
        File.WriteAllText(fixture.Config.Path, "old-config");

        var result = fixture.Applier.Apply(SampleConfig(), fixture.Managed);
        Assert.False(result.Succeeded);
        Assert.Contains("ロールバック", result.Message);
        Assert.Equal("old-content", File.ReadAllText(oldLink));
        Assert.Equal("old-config", File.ReadAllText(fixture.Config.Path));
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

        var restore = new RestoreService(fixture.Config, new ConfigValidator(), fixture.Backups, fixture.Applier, fixture.StatePath);
        var result = restore.Restore(backupPath, fixture.Managed);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("G", fixture.Config.Load().Shortcuts.Single().Hotkey.Key);
        Assert.StartsWith("G - ", Path.GetFileName(Directory.GetFiles(fixture.Managed, "*.lnk").Single()));
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
        var result = await new ExplorerShellReloader(host).ReloadAsync();
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal([42], host.TerminatedIds);
        Assert.Equal([42], host.WaitedIds);
        Assert.Equal(1, host.StartCount);
        Assert.Equal(43, host.Shell?.Id);
    }

    [Fact]
    public async Task ShellReload_RejectsAnotherSessionWithoutTerminatingIt()
    {
        var host = new FakeExplorerShellProcessHost { Shell = new(42, 8), CurrentSessionId = 7 };
        var result = await new ExplorerShellReloader(host).ReloadAsync();
        Assert.False(result.Succeeded);
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
        public void Write(string path, ShellLinkSpec spec) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "fake-link"); values[path] = spec; }
        public ShellLinkInfo Read(string path)
        {
            if (!values.TryGetValue(path, out var value)) throw new InvalidDataException("Unknown fake shortcut.");
            return new(path, value.TargetPath, value.Arguments, value.WorkingDirectory, value.Hotkey, value.IconLocation, value.Description, value.WindowStyle);
        }
    }

    private sealed class FakeExplorerShellProcessHost : IExplorerShellProcessHost
    {
        public int CurrentSessionId { get; init; }
        public ExplorerShellProcess? Shell { get; set; }
        public List<int> TerminatedIds { get; } = [];
        public List<int> WaitedIds { get; } = [];
        public int StartCount { get; private set; }

        public ExplorerShellProcess? GetShellProcess() => Shell;
        public void Terminate(ExplorerShellProcess process) { TerminatedIds.Add(process.Id); Shell = null; }
        public Task WaitForExitAsync(ExplorerShellProcess process, CancellationToken cancellationToken) { WaitedIds.Add(process.Id); return Task.CompletedTask; }
        public void StartExplorer() { StartCount++; Shell = new(43, CurrentSessionId); }
    }
}
