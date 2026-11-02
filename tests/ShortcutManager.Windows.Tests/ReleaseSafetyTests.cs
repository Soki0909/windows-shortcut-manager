using System.Text.Json;
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

[CollectionDefinition("Apply transactions", DisableParallelization = true)]
public sealed class ApplyTransactionCollection { }

[Collection("Apply transactions")]
public sealed class ReleaseSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"shortcut-release-tests-{Guid.NewGuid():N}");
    private readonly TestLinks links = new();
    private readonly LaunchSpecResolver resolver = new(new ChromeLocator(), new DispatcherUriBuilder());
    private readonly ConfigRepository repository;
    private readonly BackupService backups;
    private readonly ManagedShortcutApplier applier;
    private string Managed => Path.Combine(root, "managed");
    private string State => Path.Combine(root, "state.json");

    public ReleaseSafetyTests()
    {
        Directory.CreateDirectory(root);
        repository = new(Path.Combine(root, "config.json"));
        backups = new(Path.Combine(root, "backups"), "test");
        applier = new(repository, new(), resolver, links, backups, Path.Combine(root, "stage"), State);
    }

    public void Dispose() { try { Directory.Delete(root, true); } catch { } }
    private static ShortcutConfig Sample() => new() { Shortcuts = [new() { Name = "Site", Hotkey = new() { Key = "U" }, Target = new WebsiteTarget { Url = "https://example.com" } }] };

    public static IEnumerable<object[]> AttributeCases() => new[] { "target", "arguments", "directory", "hotkey", "icon", "description", "style" }
        .SelectMany(attribute => new[] { new object[] { attribute, false }, new object[] { attribute, true } });

    [Theory]
    [MemberData(nameof(AttributeCases))]
    public void EveryLaunchAttributeIsVerifiedBeforeAndAfterCommit(string attribute, bool afterCommit)
    {
        var config = Sample();
        Assert.True(applier.Apply(config, Managed).Succeeded);
        var beforeConfig = File.ReadAllBytes(repository.Path);
        var linkPath = Directory.GetFiles(Managed, "*.lnk").Single();
        var beforeLink = File.ReadAllBytes(linkPath);
        var beforeState = File.ReadAllBytes(State);
        links.Change = (path, value) => (path.Contains(Path.DirectorySeparatorChar + "stage" + Path.DirectorySeparatorChar) != afterCommit)
            ? attribute switch
            {
                "target" => value with { TargetPath = Path.Combine(root, "wrong.exe") },
                "arguments" => value with { Arguments = "wrong" },
                "directory" => value with { WorkingDirectory = root },
                "hotkey" => value with { Hotkey = "CTRL+ALT+Z" },
                "icon" => value with { IconLocation = "wrong,1" },
                "description" => value with { Description = "wrong" },
                _ => value with { WindowStyle = 7 }
            } : value;
        var result = applier.Apply(config, Managed);
        Assert.False(result.Succeeded);
        Assert.Contains(afterCommit ? "Committed" : "Staged", result.Message);
        Assert.Equal(beforeConfig, File.ReadAllBytes(repository.Path));
        Assert.Equal(beforeLink, File.ReadAllBytes(linkPath));
        Assert.Equal(beforeState, File.ReadAllBytes(State));
        links.Change = null;
        Assert.True(new ManagedShortcutAuditor(new(), resolver, links).Audit(config, Managed).Matches);
    }

    [Fact]
    public void NormalizedComValuesPassBothApplyAndNextAudit()
    {
        links.Change = (_, value) => value with { Hotkey = "alt + ctrl + u", TargetPath = value.TargetPath.ToUpperInvariant(), IconLocation = value.IconLocation.EndsWith(",0") ? value.IconLocation[..^2] : value.IconLocation };
        var config = Sample();
        var result = applier.Apply(config, Managed);
        Assert.True(result.Succeeded, result.Message);
        Assert.True(new ManagedShortcutAuditor(new(), resolver, links).Audit(config, Managed).Matches);
    }

    [Fact]
    public async Task RestoreKeepsResultAndPendingAcrossReloadAndFailedShellRestart()
    {
        Assert.True(applier.Apply(Sample(), Managed).Succeeded);
        var backup = backups.Create("manual", repository.Path, Managed, State);
        var reloader = new ResultReloader();
        var paths = new ApplicationPaths(root, repository.Path, State, Path.Combine(root, "log.txt"), Path.Combine(root, "backups"), Path.Combine(root, "stage"), Managed);
        var vm = new ShortcutManager.App.MainViewModel(paths, repository, new(), resolver, new(), new(links, new()), new(), backups, applier, new(new(), applier), new(paths.LogPath), reloader);
        Assert.True(await vm.RestoreAsync(backup));
        Assert.Contains("復元が完了", vm.Status);
        Assert.Contains("復元しました: 1 件", vm.RestoreNotice);
        Assert.Contains("復元前バックアップ:", vm.RestoreNotice);
        Assert.True(vm.ShellReflectionPending);
        Assert.Equal(0, reloader.Calls); // Declining the GUI offer does not invoke the reloader.
        vm.Load();
        Assert.Contains("復元しました: 1 件", vm.RestoreNotice);
        await vm.ReloadShellAsync();
        Assert.True(vm.ShellReflectionPending);
        reloader.Succeeded = true;
        await vm.ReloadShellAsync();
        Assert.False(vm.ShellReflectionPending);
        Assert.Contains("復元しました: 1 件", vm.RestoreNotice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingExternalBatOrSourceLinkIsExplainedAndRestoreLeavesCurrentState(bool sourceLink)
    {
        var external = Path.Combine(root, sourceLink ? "external.lnk" : "external.cmd");
        File.WriteAllText(external, "external fixture");
        var config = Sample();
        config.Shortcuts[0] = config.Shortcuts[0] with { Target = sourceLink ? new ApplicationTarget { Value = external, LaunchMode = ApplicationLaunchMode.Shortcut } : new BatTarget { Path = external } };
        repository.SaveAtomic(config);
        var backup = backups.Create("manual", repository.Path, Managed, State);
        File.Move(external, external + ".moved");
        var current = Sample();
        Assert.True(applier.Apply(current, Managed).Succeeded);
        var before = File.ReadAllBytes(repository.Path);
        var service = new RestoreService(new(), applier);
        Assert.Contains(external, service.Preview(backup));
        Assert.Contains("含まないもの", service.Preview(backup));
        var result = service.Restore(backup, Managed);
        Assert.False(result.Succeeded);
        Assert.Contains(external, result.Message);
        Assert.Equal(before, File.ReadAllBytes(repository.Path));
        Assert.True(File.Exists(external + ".moved"));
    }

    [Theory]
    [InlineData("exit-timeout")]
    [InlineData("exit-cancel")]
    [InlineData("return-cancel")]
    [InlineData("return-timeout")]
    [InlineData("terminate-error")]
    public async Task ShellFailuresRecoverIndependentlyOfCallerCancellation(string failure)
    {
        using var cancel = new CancellationTokenSource();
        var host = new RecoveryHost(failure, cancel);
        var result = await new ExplorerShellReloader(host, TimeSpan.Zero, TimeSpan.FromMilliseconds(10), TimeSpan.Zero).ReloadAsync(cancel.Token);
        Assert.False(result.Succeeded);
        Assert.Equal(43, result.CurrentProcessId);
        Assert.Contains("Shell表示は確認", result.Message);
        Assert.Equal(new[] { 42 }, host.Terminated);
    }

    [Fact]
    public async Task PreCanceledReloadDoesNotStopExplorer()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var host = new RecoveryHost("", cancel);
        var result = await new ExplorerShellReloader(host).ReloadAsync(cancel.Token);
        Assert.False(result.Succeeded);
        Assert.Empty(host.Terminated);
        Assert.Equal(0, host.Starts);
    }

    [Theory]
    [InlineData("recovery-error")]
    [InlineData("recovery-timeout")]
    [InlineData("other-session")]
    public async Task FailedRecoveryReportsManualStepsAndDoesNotTouchOtherSessions(string failure)
    {
        using var cancel = new CancellationTokenSource();
        var host = new RecoveryHost(failure, cancel);
        var result = await new ExplorerShellReloader(host, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero).ReloadAsync();
        Assert.False(result.Succeeded);
        Assert.Null(result.CurrentProcessId);
        Assert.Contains("Ctrl+Shift+Esc", result.Message);
        Assert.Equal(new[] { 42 }, host.Terminated);
        if (failure == "other-session") Assert.Equal(0, host.Starts);
    }

    private sealed class TestLinks : IShellLinkService
    {
        public Func<string, ShellLinkInfo, ShellLinkInfo>? Change;
        public void Write(string path, ShellLinkSpec spec) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(spec)); }
        public ShellLinkInfo Read(string path)
        {
            var value = JsonSerializer.Deserialize<ShellLinkSpec>(File.ReadAllText(path))!;
            var read = new ShellLinkInfo(path, value.TargetPath, value.Arguments, value.WorkingDirectory, value.Hotkey, value.IconLocation, value.Description, value.WindowStyle);
            return Change?.Invoke(path, read) ?? read;
        }
    }

    private sealed class ResultReloader : IExplorerShellReloader
    {
        public bool Succeeded;
        public int Calls;
        public Task<ExplorerShellReloadResult> ReloadAsync(CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(new ExplorerShellReloadResult(Succeeded, "test")); }
    }

    private sealed class RecoveryHost(string failure, CancellationTokenSource cancel) : IExplorerShellProcessHost
    {
        public int CurrentSessionId => 7;
        public ExplorerShellProcess? Shell = new(42, 7);
        public List<int> Terminated = [];
        public int Starts;
        public ExplorerShellProcess? GetShellProcess() => Shell;
        public void Terminate(ExplorerShellProcess process)
        {
            Terminated.Add(process.Id); Shell = null;
            if (failure is "terminate-error" or "recovery-error" or "recovery-timeout") throw new IOException("fixture termination failure");
            if (failure == "other-session") { Shell = new(90, 8); throw new IOException("fixture session changed"); }
        }
        public async Task WaitForExitAsync(ExplorerShellProcess process, CancellationToken cancellationToken)
        {
            if (failure == "exit-cancel") cancel.Cancel();
            if (failure is "exit-cancel" or "exit-timeout") await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        public void StartExplorer()
        {
            Starts++;
            if (failure == "recovery-error") throw new IOException("fixture start failure");
            if (failure == "recovery-timeout") return;
            if (failure == "return-cancel" && Starts == 1) { cancel.Cancel(); return; }
            if (failure == "return-timeout" && Starts == 1) return;
            Shell = new(43, CurrentSessionId);
        }
    }
}
