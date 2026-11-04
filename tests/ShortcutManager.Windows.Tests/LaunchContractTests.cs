using System.Text.Json;
using System.Diagnostics;
using Microsoft.Win32;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Diagnostics;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Tests;

public sealed class LaunchContractTests : IDisposable
{
    private readonly string root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") == "github-hosted"
        ? Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? Path.GetTempPath() : Path.GetTempPath(), "shortcut-manager-launch-" + Guid.NewGuid().ToString("N"));
    private readonly LaunchSpecResolver resolver = new(new ChromeLocator(), new DispatcherUriBuilder());
    private readonly string probe;

    public LaunchContractTests()
    {
        Directory.CreateDirectory(root);
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var source = Path.Combine(repository, "tests", "ShortcutManager.LaunchProbe", "bin", configuration, "net10.0-windows");
        var destination = Path.Combine(root, "日本語 アプリ"); Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        probe = Path.Combine(destination, "ShortcutManager.LaunchProbe.exe");
        Assert.True(File.Exists(probe));
    }

    private string ReportPath() => Path.Combine(root, "report-" + Guid.NewGuid().ToString("N") + ".json");
    private LaunchSpec Resolve(ApplicationTarget target) => resolver.Resolve(new(), new() { Name = "fixture", Hotkey = new() { Key = "U" }, Target = target });
    private static async Task<JsonDocument> ReadReport(string path)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (File.Exists(path))
                {
                    var report = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                    try
                    {
                        using var process = Process.GetProcessById(report.RootElement.GetProperty("ProcessId").GetInt32());
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await process.WaitForExitAsync(timeout.Token);
                    }
                    catch (ArgumentException) { } // Probe already exited; never kill by a recycled PID.
                    return report;
                }
            }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(50);
        }
        throw new TimeoutException("Isolated launch probe did not produce its report.");
    }

    [Fact]
    public async Task ExecutableReceivesUnicodeQuotedArgumentsAndExplicitWorkingDirectory()
    {
        var report = ReportPath(); var working = Path.Combine(root, "日本語 作業フォルダ"); Directory.CreateDirectory(working);
        var values = new[] { "日本語 空白", "quote\"inside", @"C:\末尾 空白\", "https://example.invalid/a?x=1&y=2", "" };
        var arguments = "--report " + LaunchSpecResolver.Quote(report) + " " + string.Join(" ", values.Select(LaunchSpecResolver.Quote));
        var spec = Resolve(new() { Value = probe, Arguments = arguments, WorkingDirectory = working });
        new ProcessLauncher().Launch(spec);
        using var result = await ReadReport(report);
        Assert.Equal(values, result.RootElement.GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(working, result.RootElement.GetProperty("WorkingDirectory").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task ShortcutUsesOriginalLinkArgumentsAndWorkingDirectoryWithoutFlatteningOrMutation()
    {
        var report = ReportPath(); var working = Path.Combine(root, "元リンクの 作業フォルダ"); Directory.CreateDirectory(working);
        var source = Path.Combine(root, "元の 日本語リンク.lnk"); var links = new ShellLinkService();
        links.Write(source, new(probe, "--report " + LaunchSpecResolver.Quote(report) + " " + LaunchSpecResolver.Quote("元リンクの引数"), working, "", probe + ",0", "fixture"));
        var before = File.ReadAllBytes(source);
        var spec = Resolve(new() { LaunchMode = ApplicationLaunchMode.Shortcut, Value = source, Arguments = "legacy-ignored-extra" });
        Assert.Equal(LaunchSpecResolver.Quote(source), spec.Arguments);
        new ProcessLauncher().Launch(spec);
        using var result = await ReadReport(report);
        Assert.Equal("元リンクの引数", result.RootElement.GetProperty("Arguments")[0].GetString());
        Assert.Equal(working, result.RootElement.GetProperty("WorkingDirectory").GetString(), ignoreCase: true);
        Assert.Equal(before, File.ReadAllBytes(source)); Assert.Empty(links.Read(source).Hotkey);
    }

    [HostedWindowsFact]
    public async Task UncPathWithUnicodeAndSpacesValidatesAndLaunchesWithoutRewritingIt()
    {
        Assert.True(HostedWindowsFactAttribute.IsHostedWindows);
        var folder = root;
        Directory.CreateDirectory(Path.Combine(root, "共有の 作業フォルダ"));
        var safeFolder = folder.Replace("'", "''", StringComparison.Ordinal);
        // Repeat the real create/launch/remove lifecycle, not a blanket retry
        // of failed assertions. Every cycle must succeed on the same runner.
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var share = "sm-launch-" + Guid.NewGuid().ToString("N"); var owner = Guid.NewGuid().ToString("N");
            var cleanup = FixturePowerShell.RemoveOwnedShare(share, owner, folder);
            await FixturePowerShell.WithCleanup(async () =>
            {
                RunShareScript("Start-Service LanmanServer; if(@(Get-SmbShare -ErrorAction Stop | Where-Object Name -EQ '" + share + "').Count){throw 'Share collision'}; New-SmbShare -Name '" + share + "' -Path '" + safeFolder + "' -Description '" + owner + "' -ReadAccess ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -ErrorAction Stop | Out-Null", $"create cycle {cycle}");
                var uncRoot = @"\\localhost\" + share;
                var uncFolder = uncRoot + @"\共有の 作業フォルダ";
                var uncProbe = uncRoot + @"\日本語 アプリ\" + Path.GetFileName(probe);
                Assert.Equal(File.ReadAllBytes(probe), File.ReadAllBytes(uncProbe));
                var report = ReportPath();
                var target = new ApplicationTarget { Value = uncProbe, WorkingDirectory = uncFolder,
                    Arguments = "--report " + LaunchSpecResolver.Quote(report) + " " + LaunchSpecResolver.Quote("共有からの 日本語引数") };
                var config = new ShortcutConfig { Shortcuts = [new() { Name = "share fixture", Hotkey = new() { Key = "U" }, Target = target }] };
                Assert.True(new ConfigValidator().Validate(config).IsValid);
                var spec = Resolve(target); Assert.Equal(uncProbe, spec.TargetPath); Assert.Equal(uncFolder, spec.WorkingDirectory);
                new ProcessLauncher().Launch(spec);
                using var result = await ReadReport(report);
                Assert.Equal("共有からの 日本語引数", result.RootElement.GetProperty("Arguments")[0].GetString());
                Assert.Equal(uncFolder, result.RootElement.GetProperty("WorkingDirectory").GetString(), ignoreCase: true);
            }, () =>
            {
                RunShareScript(cleanup, $"cleanup cycle {cycle}");
                RunShareScript(cleanup, $"already absent cycle {cycle}");
            });
        }
    }

    private static void RunShareScript(string script, string phase)
    {
        Assert.True(HostedWindowsFactAttribute.IsHostedWindows);
        FixturePowerShell.Run(script, phase);
    }

    [HostedWindowsFact]
    public async Task UriUsesIsolatedRegisteredHandlerAndNeverAppendsLegacyArguments()
    {
        Assert.True(HostedWindowsFactAttribute.IsHostedWindows);
        var scheme = "sm-launch-test-" + Guid.NewGuid().ToString("N");
        var registryPath = @"Software\Classes\" + scheme; var owner = Guid.NewGuid().ToString("N");
        using (var existing = Registry.ClassesRoot.OpenSubKey(scheme)) Assert.Null(existing);
        var report = ReportPath();
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                key.SetValue("TestOwner", owner); key.SetValue("URL Protocol", ""); key.SetValue("", "URL:isolated launch test");
                using var command = key.CreateSubKey(@"shell\open\command");
                command.SetValue("", LaunchSpecResolver.Quote(probe) + " --report " + LaunchSpecResolver.Quote(report) + " \"%1\"");
            }
            Assert.True(new UriRegistrationInspector().Inspect(scheme + ":test").Registered);
            var uri = scheme + ":payload?name=example&value=1";
            var spec = Resolve(new() { LaunchMode = ApplicationLaunchMode.Uri, Value = uri, Arguments = "legacy-ignored-extra" });
            new ProcessLauncher().Launch(spec);
            using var result = await ReadReport(report);
            Assert.Equal(uri, result.RootElement.GetProperty("Arguments")[0].GetString());
            Assert.Equal(1, result.RootElement.GetProperty("Arguments").GetArrayLength());
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath);
            if (key?.GetValue("TestOwner") as string == owner) Registry.CurrentUser.DeleteSubKeyTree(registryPath);
        }
    }

    public void Dispose() => Directory.Delete(root, true);
}

public sealed class HostedWindowsFactAttribute : FactAttribute
{
    public static bool IsHostedWindows => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"
        && Environment.GetEnvironmentVariable("RUNNER_OS") == "Windows"
        && Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") == "github-hosted"
        && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUNNER_TEMP"));
    public HostedWindowsFactAttribute()
    {
        if (!IsHostedWindows)
            Skip = "URI handler registration is restricted to disposable GitHub Windows runners.";
    }
}
