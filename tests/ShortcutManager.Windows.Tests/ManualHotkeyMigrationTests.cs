using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Tests;

public sealed class ManualHotkeyMigrationTests : IDisposable
{
    private const string Key = "CTRL+ALT+Q";
    private const string Url = "https://example.invalid/fixture";
    private readonly string root = Path.Combine(Path.GetTempPath(), "shortcut-manager-migration-" + Guid.NewGuid().ToString("N"));
    private readonly ShellLinkService links = new();
    private readonly ShortcutInventoryScanner scanner;
    private string Sources => Path.Combine(root, "sources");
    private string Data => Path.Combine(root, "data");
    private string Managed => Path.Combine(root, "managed");
    private string OtherEdition => Path.Combine(root, "other-edition");
    private readonly ManualHotkeyMigration migration;

    public ManualHotkeyMigrationTests()
    {
        Directory.CreateDirectory(Sources);
        scanner = new(links, new());
        migration = new(Data, [Managed, OtherEdition], links, new());
    }

    [Theory]
    [InlineData(".url")]
    [InlineData(".lnk")]
    public async Task VerifiedCopyPreservesOriginalAndAllowsReuseAfterManualRelease(string extension)
    {
        var path = Path.Combine(Sources, "元 のファイル" + extension);
        WriteFixture(path);
        var original = Assert.Single((await ScanAsync()).Items);
        var originalBytes = File.ReadAllBytes(path);
        Assert.True(migration.CanGuide(original));
        var backup = migration.CreateCopy(original);
        migration.CheckBeforeProperties(backup);
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal(Key, (await ScanAsync()).Items.Single().Hotkey);

        using (var archive = ZipFile.OpenRead(backup.BackupPath))
        {
            using var entry = archive.GetEntry("original/" + Path.GetFileName(path))!.Open();
            Assert.Equal(Convert.ToHexString(SHA256.HashData(originalBytes)), Convert.ToHexString(SHA256.HashData(entry)));
            using var metadata = JsonDocument.Parse(archive.GetEntry("migration-source.json")!.Open());
            Assert.Equal(path, metadata.RootElement.GetProperty("OriginalPath").GetString());
            Assert.Equal(Key, metadata.RootElement.GetProperty("OriginalHotkey").GetString());
            Assert.Equal(backup.Sha256, metadata.RootElement.GetProperty("Sha256").GetString());
        }

        var stillAssigned = await ScanAsync();
        Assert.Throws<InvalidDataException>(() => migration.VerifyRelease(original, stillAssigned));
        WriteFixture(path, release: true); // Simulates the user's edit to an isolated fixture.
        var released = await ScanAsync();
        migration.VerifyRelease(original, released);
        Assert.True(ImportKeyChoices.Build(new(), released.Items).Single(choice => choice.Key == "Q").Available);
        Assert.Throws<InvalidDataException>(() => migration.CheckBeforeProperties(backup));

        // Simulate the user's later recovery using the saved file.
        using var saved = ZipFile.OpenRead(backup.BackupPath);
        using var restored = saved.GetEntry("original/" + Path.GetFileName(path))!.Open();
        using (var fixture = File.Create(path)) restored.CopyTo(fixture);
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal(Key, (await ScanAsync()).Items.Single().Hotkey);
    }

    [Fact]
    public async Task RefusesMalformedHotkeyRemainingConflictOrIncompleteScan()
    {
        var source = Path.Combine(Sources, "source.url");
        var other = Path.Combine(Sources, "other.url");
        WriteFixture(source);
        var original = (await ScanAsync()).Items.Single();
        File.WriteAllText(source, $"[InternetShortcut]\nURL={Url}\nHotKey=invalid\n");
        var malformed = await ScanAsync();
        Assert.Empty(malformed.Items.Single().Hotkey);
        Assert.Throws<InvalidDataException>(() => migration.VerifyRelease(original, malformed));

        WriteFixture(source, release: true);
        WriteFixture(other);
        var conflict = await ScanAsync();
        Assert.Contains(other, Assert.Throws<InvalidDataException>(() => migration.VerifyRelease(original, conflict)).Message);
        File.Delete(other);
        var incomplete = await scanner.ScanReportAsync([Sources, Path.Combine(root, "missing")], Managed);
        Assert.False(incomplete.IsComplete);
        Assert.Throws<InvalidDataException>(() => migration.VerifyRelease(original, incomplete));
    }

    [Fact]
    public async Task RefusesStaleSourceOrAnUnavailableBackupAndDoesNotTouchOtherEdition()
    {
        var source = Path.Combine(Sources, "source.url");
        WriteFixture(source);
        var original = (await ScanAsync()).Items.Single();
        WriteFixture(source, release: true);
        Assert.Throws<InvalidDataException>(() => migration.CreateCopy(original));
        WriteFixture(source);
        File.WriteAllText(Data, "fixture unavailable backup directory");
        Assert.ThrowsAny<IOException>(() => migration.CreateCopy(original));
        Assert.Equal(Key, (await ScanAsync()).Items.Single().Hotkey);

        Directory.CreateDirectory(OtherEdition);
        var protectedFile = Path.Combine(OtherEdition, "other.url");
        WriteFixture(protectedFile);
        var protectedSource = Assert.Single((await scanner.ScanReportAsync([OtherEdition], Managed)).Items);
        Assert.False(protectedSource.IsManaged);
        Assert.False(migration.CanGuide(protectedSource));
        Assert.Throws<InvalidOperationException>(() => migration.CreateCopy(protectedSource));
    }

    [Fact]
    public async Task RefusesAnOwnershipMarkedCustomDirectoryEvenWithoutLoadingTheOtherEdition()
    {
        var customDirectory = Directory.CreateDirectory(Path.Combine(Sources, "custom")).FullName;
        File.WriteAllText(Path.Combine(customDirectory, ManagedShortcutApplier.OwnershipMarker), "fixture marker");
        var path = Path.Combine(customDirectory, "source.url");
        WriteFixture(path);
        var original = Assert.Single((await ScanAsync()).Items);
        Assert.Throws<IOException>(() => migration.CreateCopy(original));
        Assert.False(Directory.Exists(Data));
    }

    private Task<InventoryScanReport> ScanAsync() => scanner.ScanReportAsync([Sources], Managed);

    private void WriteFixture(string path, bool release = false)
    {
        if (path.EndsWith(".lnk", StringComparison.Ordinal))
            links.Write(path, new(Environment.ProcessPath!, "--fixture-only", root, release ? "" : Key, "", "Fixture", 7));
        else File.WriteAllText(path, $"[InternetShortcut]\nURL={Url}\nHotKey={(release ? 0 : 1617)}\n");
    }

    public void Dispose()
    {
        if (Path.GetDirectoryName(root) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) &&
            !File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            Directory.Delete(root, recursive: true);
    }
}
