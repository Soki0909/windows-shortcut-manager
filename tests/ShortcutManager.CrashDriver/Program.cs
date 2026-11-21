using System.Diagnostics;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Removal;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

var root = Path.GetFullPath(args[0]);
if (!Path.GetFileName(root).StartsWith("shortcut-manager-tests-", StringComparison.Ordinal)) throw new ArgumentException("Isolated fixture required.");
if (args is [_, "removal", var asPoint] && asPoint is "file-staged" or "file-restored" or "file-deleted")
{
    if (!Path.GetFileName(root).StartsWith("shortcut-manager-tests-removal-", StringComparison.Ordinal))
        throw new ArgumentException("Isolated removal fixture required.");
    var edition = RemovalPaths("data", "managed", false);
    var other = RemovalPaths("debug-data", "debug-managed", true);
    var service = new LocalDataRemovalStaging(edition, other, point =>
    {
        if (point != asPoint) return;
        File.WriteAllText(Path.Combine(root, "crash-confirmed-" + point), point);
        Process.GetCurrentProcess().Kill();
    });
    if (asPoint == "file-staged")
    {
        var plan = new LocalDataRemovalPreview(edition, other).Inspect(false);
        var archive = new LocalDataRemovalArchive(edition, other).Create(plan, [], Path.Combine(root, "driver-before.zip"), false);
        service.Stage(plan, [], archive, false);
    }
    else if (asPoint == "file-restored") service.Cancel();
    else service.Commit();
    return;

    ApplicationPaths RemovalPaths(string dataName, string managedName, bool debug)
    {
        var data = Path.Combine(root, dataName);
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"),
            Path.Combine(data, "logs", "shortcut-manager.log"), Path.Combine(data, "backups"), Path.Combine(data, "staging"),
            Path.Combine(root, managedName), debug);
    }
}
if (args[1] == "hung-window")
{
    var thread = new Thread(() => {
        using var form = new ProbeWindow { ShowInTaskbar = false, Opacity = 0 };
        form.Shown += (_, _) => File.WriteAllText(Path.Combine(root, "window-ready"), "ready");
        System.Windows.Forms.Application.Run(form);
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); return;
}
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

internal sealed class ProbeWindow : System.Windows.Forms.Form
{
    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        if (message.Msg == 0x0033) Thread.Sleep(1000);
        base.WndProc(ref message);
    }
}
