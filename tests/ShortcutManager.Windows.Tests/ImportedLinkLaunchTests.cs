using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Validation;
using ShortcutManager.Core.Utilities;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class ImportedLinkLaunchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shortcut-manager-launch-" + Guid.NewGuid().ToString("N"));
    private readonly ShellLinkService links = new();
    private readonly string probe;

    public ImportedLinkLaunchTests()
    {
        Directory.CreateDirectory(root);
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var source = Path.Combine(repository, "tests", "ShortcutManager.LaunchProbe", "bin", configuration, "net10.0-windows");
        var destination = Directory.CreateDirectory(Path.Combine(root, "識別付き アプリ")).FullName;
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        probe = Path.Combine(destination, "ShortcutManager.LaunchProbe.exe");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(7, true)]
    public async Task ImportedLaunchMatchesDirectSourceAndPreservesShowCommandAndIdentity(int showCommand, bool identified)
    {
        var source = Path.Combine(root, "元の 起動条件.lnk");
        var working = Directory.CreateDirectory(Path.Combine(root, "元の 作業フォルダ")).FullName;
        var report = Path.Combine(root, "report-" + Guid.NewGuid().ToString("N") + ".json");
        var args = "--report " + LaunchSpecResolver.Quote(report) + " " + LaunchSpecResolver.Quote("元リンクの 特殊引数");
        links.Write(source, new(probe, args, working, "CTRL+ALT+Q", probe + ",0", "original fixture", showCommand));
        var appId = "ShortcutManager.IsolatedFixture." + Guid.NewGuid().ToString("N");
        if (identified) LinkAppIdentity.Write(source, appId);
        var bytes = File.ReadAllBytes(source); var modified = File.GetLastWriteTimeUtc(source);
        Process.Start(new ProcessStartInfo { FileName = source, UseShellExecute = true });
        using var baseline = await ReadReport(report);
        File.Move(report, report + ".baseline");
        var scanned = Assert.Single(await new ShortcutInventoryScanner(links, new()).ScanAsync([root], Path.Combine(root, "managed")), item => !item.IsManaged);
        var proposal = new ShortcutImporter().CreateProposal(scanned, "J");
        var target = Assert.IsType<ApplicationTarget>(proposal.Target);
        Assert.Equal(ApplicationLaunchMode.Shortcut, target.LaunchMode);
        Assert.Equal(source, target.Value); Assert.Empty(target.Arguments); Assert.Empty(target.WorkingDirectory);
        var config = new ShortcutConfig { Shortcuts = [proposal] };
        Assert.True(new ConfigValidator().Validate(config).IsValid);
        var resolver = new LaunchSpecResolver(new ChromeLocator(), new DispatcherUriBuilder());
        var spec = resolver.Resolve(config, proposal);
        new ProcessLauncher().Launch(spec);
        using var result = await ReadReport(report);
        Assert.Equal("元リンクの 特殊引数", result.RootElement.GetProperty("Arguments")[0].GetString());
        Assert.Equal(working, result.RootElement.GetProperty("WorkingDirectory").GetString(), ignoreCase: true);
        Assert.Equal(baseline.RootElement.GetProperty("Arguments").GetRawText(), result.RootElement.GetProperty("Arguments").GetRawText());
        Assert.Equal(baseline.RootElement.GetProperty("WorkingDirectory").GetString(), result.RootElement.GetProperty("WorkingDirectory").GetString(), ignoreCase: true);
        Assert.Equal(baseline.RootElement.GetProperty("ShowCommandSpecified").GetBoolean(), result.RootElement.GetProperty("ShowCommandSpecified").GetBoolean());
        Assert.Equal(baseline.RootElement.GetProperty("ShowCommand").GetInt32(), result.RootElement.GetProperty("ShowCommand").GetInt32());
        File.Move(report, report + ".imported");
        var repository = new ConfigRepository(Path.Combine(root, "data", "config.json"));
        var applier = new ManagedShortcutApplier(repository, new(), resolver, links,
            new BackupService(Path.Combine(root, "backups"), "0.4.0"), Path.Combine(root, "staging"), Path.Combine(root, "data", "state.json"));
        var managed = Path.Combine(root, "managed");
        var applied = applier.Apply(config, managed, ["CTRL+ALT+Q"]);
        Assert.True(applied.Succeeded, applied.Message); Assert.True(File.Exists(applied.BackupPath));
        var wrapper = Path.Combine(managed, ManagedFilename.For(proposal));
        Assert.True((BitConverter.ToUInt32(File.ReadAllBytes(wrapper), 20) & 0x00800000) != 0);
        var generated = links.Read(wrapper); Assert.Equal(source, generated.TargetPath); Assert.Empty(generated.Arguments); Assert.Equal("CTRL+ALT+J", generated.Hotkey);
        Process.Start(new ProcessStartInfo { FileName = wrapper, UseShellExecute = true });
        using var nested = await ReadReport(report);
        Assert.Equal(baseline.RootElement.GetProperty("Arguments").GetRawText(), nested.RootElement.GetProperty("Arguments").GetRawText());
        Assert.Equal(working, nested.RootElement.GetProperty("WorkingDirectory").GetString(), ignoreCase: true);
        Assert.Equal(baseline.RootElement.GetProperty("ShowCommandSpecified").GetBoolean(), nested.RootElement.GetProperty("ShowCommandSpecified").GetBoolean());
        Assert.Equal(baseline.RootElement.GetProperty("ShowCommand").GetInt32(), nested.RootElement.GetProperty("ShowCommand").GetInt32());
        Assert.True(new ManagedShortcutAuditor(new(), resolver, links).Audit(config, managed).Matches);
        Assert.Equal(bytes, File.ReadAllBytes(source)); Assert.Equal(modified, File.GetLastWriteTimeUtc(source));
        var saved = links.Read(source); Assert.Equal(showCommand, saved.WindowStyle); Assert.Equal("CTRL+ALT+Q", saved.Hotkey);
        if (identified) Assert.Equal(appId, LinkAppIdentity.Read(source));
    }

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
                    catch (ArgumentException) { }
                    return report;
                }
            }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(50);
        }
        throw new TimeoutException("Imported link did not launch the isolated probe.");
    }

    public void Dispose() => Directory.Delete(root, true);

    // The product deliberately does not reimplement the Shell property store.
    // This fixture adds/reads an opaque AppUserModelID to prove that import and
    // launch leave the original identity and bytes intact.
    private static class LinkAppIdentity
    {
        private static readonly PropertyKey AppId = new(new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        internal static void Write(string path, string appId)
        {
            object native = new ShellLinkNative();
            var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(appId) };
            try
            {
                var persist = (IPersistFile)native; persist.Load(path, 2);
                ((IPropertyStore)native).SetValue(AppId, value);
                ((IPropertyStore)native).Commit(); persist.Save(path, true);
            }
            finally { PropVariantClear(ref value); Marshal.FinalReleaseComObject(native); }
        }
        internal static string Read(string path)
        {
            object native = new ShellLinkNative(); var value = new PropVariant();
            try
            {
                ((IPersistFile)native).Load(path, 0);
                ((IPropertyStore)native).GetValue(AppId, out value);
                Assert.Equal(31, value.Type);
                return Marshal.PtrToStringUni(value.Pointer)!;
            }
            finally { PropVariantClear(ref value); Marshal.FinalReleaseComObject(native); }
        }
        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PropertyKey(Guid format, uint id) { public readonly Guid Format = format; public readonly uint Id = id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLinkNative { }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint count);
            void GetAt(uint index, out PropertyKey key);
            void GetValue(in PropertyKey key, out PropVariant value);
            void SetValue(in PropertyKey key, in PropVariant value);
            void Commit();
        }
    }
}
