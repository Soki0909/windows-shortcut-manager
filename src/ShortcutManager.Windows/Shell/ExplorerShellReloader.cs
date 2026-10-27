using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.Shell;

public sealed class ExplorerShellReloader : IExplorerShellReloader
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultAutoRestartGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);
    private readonly IExplorerShellProcessHost processHost;
    private readonly TimeSpan autoRestartGrace;

    public ExplorerShellReloader() : this(new ExplorerShellProcessHost(), DefaultAutoRestartGrace) { }

    public ExplorerShellReloader(IExplorerShellProcessHost processHost, TimeSpan? autoRestartGrace = null)
    {
        this.processHost = processHost;
        this.autoRestartGrace = autoRestartGrace ?? DefaultAutoRestartGrace;
    }

    public async Task<ExplorerShellReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
    {
        var shell = processHost.GetShellProcess();
        if (shell is null)
            return new(false, "Windows Shell Explorer を特定できなかったため、再読み込みを中止しました。");
        if (shell.SessionId != processHost.CurrentSessionId)
            return new(false, "別セッションの Explorer は変更しません。現在のWindowsセッションで再実行してください。", shell.Id);

        try
        {
            processHost.Terminate(shell);
            using (var exitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                exitTimeout.CancelAfter(ExitTimeout);
                await processHost.WaitForExitAsync(shell, exitTimeout.Token);
            }

            var restarted = await WaitForReplacementAsync(shell, autoRestartGrace, cancellationToken);
            if (restarted is null)
            {
                processHost.StartExplorer();
                restarted = await WaitForReplacementAsync(shell, StartTimeout, cancellationToken);
            }
            if (restarted is null)
                return new(false, "Windows Shellの再読み込みが時間内に完了しませんでした。タスク マネージャーからWindows Explorerを再起動してください。", shell.Id);

            return new(true, "Windows Shellを再読み込みしました。新規・変更したHotKeyを確認してください。", shell.Id, restarted.Id);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Windows Shellの再読み込みが時間内に完了しませんでした。タスク マネージャーからWindows Explorerを再起動してください。", shell.Id);
        }
        catch (Exception error)
        {
            try
            {
                if (processHost.GetShellProcess() is null) processHost.StartExplorer();
            }
            catch { }
            return new(false, $"Windows Shellの再読み込みに失敗しました: {error.Message}", shell.Id);
        }
    }

    private async Task<ExplorerShellProcess?> WaitForReplacementAsync(ExplorerShellProcess previous, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = processHost.GetShellProcess();
            if (current is { SessionId: var sessionId }
                && sessionId == processHost.CurrentSessionId
                && current.Id != previous.Id)
                return current;
            if (DateTime.UtcNow >= deadline) return null;
            await Task.Delay(150, cancellationToken);
        }
        while (true);
    }
}

public sealed class ExplorerShellProcessHost : IExplorerShellProcessHost
{
    public int CurrentSessionId => Process.GetCurrentProcess().SessionId;

    public ExplorerShellProcess? GetShellProcess()
    {
        try
        {
            var shellWindow = GetShellWindow();
            if (shellWindow == IntPtr.Zero) return null;
            GetWindowThreadProcessId(shellWindow, out var processId);
            if (processId == 0) return null;
            using var process = Process.GetProcessById((int)processId);
            return new(process.Id, process.SessionId);
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public void Terminate(ExplorerShellProcess process)
    {
        try
        {
            using var target = Process.GetProcessById(process.Id);
            int targetSessionId;
            try { targetSessionId = target.SessionId; }
            catch (InvalidOperationException) { return; }
            if (targetSessionId != CurrentSessionId) throw new InvalidOperationException("Explorer session changed before reload.");
            try { target.Kill(entireProcessTree: false); }
            catch (InvalidOperationException) { }
        }
        catch (ArgumentException)
        {
            // Explorer can exit and be replaced between GetShellWindow and opening the process.
            // The caller still waits for a different current-session Shell PID before success.
        }
    }

    public async Task WaitForExitAsync(ExplorerShellProcess process, CancellationToken cancellationToken)
    {
        try
        {
            using var target = Process.GetProcessById(process.Id);
            await target.WaitForExitAsync(cancellationToken);
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
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
