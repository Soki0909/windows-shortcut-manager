using System.Diagnostics;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class GuiWorkflowTests
{
    [Fact]
    public async Task FirstRunApplyAndEditSurviveProcessRestartAndReadOnlyAudit()
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var executable = Path.Combine(repository, "tests", "ShortcutManager.GuiProbe", "bin", configuration,
            "net10.0-windows", "ShortcutManager.GuiProbe.exe");
        Assert.True(File.Exists(executable), "Build the WPF workflow probe before running this test.");

        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var root = Path.Combine(temp, "shortcut-manager-gui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, ".gui-probe");
        using (File.Open(marker, FileMode.CreateNew, FileAccess.Write)) { }
        await RunProbeAsync(executable, "edit", root);
        await RunProbeAsync(executable, "audit", root);
        if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(root)), temp, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(marker) && !File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            Directory.Delete(root, recursive: true);
    }

    private static async Task RunProbeAsync(string executable, string scenario, string root)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(scenario);
        start.ArgumentList.Add(root);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("WPF workflow probe did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            if (!process.WaitForExit(5000)) throw new TimeoutException($"WPF workflow probe '{scenario}' did not stop. Fixture: {root}");
            throw new TimeoutException($"WPF workflow probe '{scenario}' timed out. Fixture: {root}\n{await output}\n{await errors}");
        }
        var stdout = await output;
        var stderr = await errors;
        Assert.True(process.ExitCode == 0 && stdout.Contains($"completed {scenario}", StringComparison.Ordinal),
            $"WPF workflow probe '{scenario}' exited with {process.ExitCode}. Fixture: {root}\n{stdout}\n{stderr}");
    }
}
