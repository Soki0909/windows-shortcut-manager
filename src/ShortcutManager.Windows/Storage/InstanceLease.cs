using System.Security.Cryptography;
using System.Text;

namespace ShortcutManager.Windows.Storage;

// Held on the UI thread until process exit, including command mode. Debug and
// product have different config paths and can run independently.
public sealed class InstanceLease : IDisposable
{
    private readonly Mutex mutex;
    public bool Acquired { get; }
    public InstanceLease(string configPath)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(configPath).ToUpperInvariant())));
        mutex = new(false, "Local\\ShortcutManager.Instance." + key);
        try { Acquired = mutex.WaitOne(TimeSpan.Zero); }
        catch (AbandonedMutexException) { Acquired = true; }
    }
    public void Dispose() { if (Acquired) mutex.ReleaseMutex(); mutex.Dispose(); }
}
