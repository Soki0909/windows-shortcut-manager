using System.Diagnostics;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

var root = Path.GetFullPath(args[0]);
if (!Path.GetFileName(root).StartsWith("shortcut-manager-tests-", StringComparison.Ordinal)) throw new ArgumentException("Isolated fixture required.");
if (args[1] == "lease")
{
    using var lease = new InstanceLease(Path.Combine(root, "lease-config.json"));
    File.WriteAllText(Path.Combine(root, "lease-result"), lease.Acquired ? "acquired" : "blocked");
    return;
}
var repository = new ConfigRepository(Path.Combine(root, "config.json"));
var applier = new ManagedShortcutApplier(repository, new ConfigValidator(), new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder()), new ShellLinkService(), new BackupService(Path.Combine(root, "backups"), "test"), Path.Combine(root, "staging"), Path.Combine(root, "state.json"), point =>
{
    if (point == args[1]) Process.GetCurrentProcess().Kill();
});
var config = new ShortcutConfig { Shortcuts = [new() { Name = "Crash candidate", Hotkey = new() { Key = "H" }, Target = new WebsiteTarget { Url = "https://example.org" } }] };
if (args[1] == "recovery-item-restored") { applier.RecoverPending(); return; }
if (!applier.Apply(config, Path.Combine(root, "managed")).Succeeded) Environment.Exit(2);
