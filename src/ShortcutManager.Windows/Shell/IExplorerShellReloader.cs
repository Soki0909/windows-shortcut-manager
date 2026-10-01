namespace ShortcutManager.Windows.Shell;

public sealed record ExplorerShellReloadResult(bool Succeeded, string Message);

public interface IExplorerShellReloader
{
    Task<ExplorerShellReloadResult> ReloadAsync(CancellationToken cancellationToken = default);
}

public sealed record ExplorerShellProcess(int Id, int SessionId);

public interface IExplorerShellProcessHost
{
    int CurrentSessionId { get; }
    ExplorerShellProcess? GetShellProcess();
    void Terminate(ExplorerShellProcess process);
    Task WaitForExitAsync(ExplorerShellProcess process, CancellationToken cancellationToken);
    void StartExplorer();
}
