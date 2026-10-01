using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.Shell;

public sealed class ExplorerShellReloader(IExplorerShellProcessHost processHost) : IExplorerShellReloader
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);

    public ExplorerShellReloader() : this(new ExplorerShellProcessHost()) { }

    public async Task<ExplorerShellReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
    {
        var shell = processHost.GetShellProcess();
        if (shell is null)
            return new(false, "Windows Shell Explorer を特定できなかったため、再読み込みを中止しました。");
        if (shell.SessionId != processHost.CurrentSessionId)
            return new(false, "別セッションの Explorer は変更しません。現在のWindowsセッションで再実行してください。");

        try
        {
            processHost.Terminate(shell);
            using (var exitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                exitTimeout.CancelAfter(ExitTimeout);
                await processHost.WaitForExitAsync(shell, exitTimeout.Token);
            }

            processHost.StartExplorer();
            using var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startTimeout.CancelAfter(StartTimeout);
            while (true)
            {
                var restarted = processHost.GetShellProcess();
                if (restarted is { SessionId: var sessionId } && sessionId == processHost.CurrentSessionId)
                    return new(true, "Windows Shellを再読み込みし、HotKeyを再起動なしで反映しました。");
                await Task.Delay(150, startTimeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Windows Shellの再読み込みが時間内に完了しませんでした。タスク マネージャーからWindows Explorerを再起動してください。");
        }
        catch (Exception error)
        {
            try
            {
                if (processHost.GetShellProcess() is null) processHost.StartExplorer();
            }
            catch { }
            return new(false, $"Windows Shellの再読み込みに失敗しました: {error.Message}");
        }
    }
}

public sealed class ExplorerShellProcessHost : IExplorerShellProcessHost
{
    public int CurrentSessionId => Process.GetCurrentProcess().SessionId;

    public ExplorerShellProcess? GetShellProcess()
    {
        var shellWindow = GetShellWindow();
        if (shellWindow == IntPtr.Zero) return null;
        GetWindowThreadProcessId(shellWindow, out var processId);
        if (processId == 0) return null;
        using var process = Process.GetProcessById((int)processId);
        return new(process.Id, process.SessionId);
    }

    public void Terminate(ExplorerShellProcess process)
    {
        using var target = Process.GetProcessById(process.Id);
        if (target.SessionId != CurrentSessionId) throw new InvalidOperationException("Explorer session changed before reload.");
        target.Kill(entireProcessTree: false);
    }

    public async Task WaitForExitAsync(ExplorerShellProcess process, CancellationToken cancellationToken)
    {
        try
        {
            using var target = Process.GetProcessById(process.Id);
            await target.WaitForExitAsync(cancellationToken);
        }
        catch (ArgumentException) { }
    }

    public void StartExplorer()
    {
        if (GetShellProcess() is not null) return;
        var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Process.Start(new ProcessStartInfo(explorerPath) { UseShellExecute = false });
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
