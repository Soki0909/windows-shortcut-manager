using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Removal;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class LocalDataRemovalPreviewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"removal-preview-{Guid.NewGuid():N}");
    private readonly ApplicationPaths paths;
    private readonly ApplicationPaths other;
    private readonly LocalDataRemovalPreview preview;

    public LocalDataRemovalPreviewTests()
    {
        paths = Edition("data", "managed", false);
        other = Edition("debug-data", "debug-managed", true);
        preview = new(paths, other);
    }

    [Fact]
    public void FirstRunDoesNotCreateAnyPaths()
    {
        var result = preview.Inspect(false);

        Assert.Empty(result.Blockers);
        Assert.Empty(result.Items);
        Assert.False(result.CanReviewCandidates);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void PreviewFindsVerifiedCandidatesAndPreservesExternalAndUnknownFiles()
    {
        var config = new ShortcutConfig();
        var backups = new BackupService(paths.BackupDirectory, "test");
        var applier = new ManagedShortcutApplier(new(paths.ConfigPath), new(), new LaunchSpecResolver(new ChromeLocator(), new()),
            new ShellLinkService(), backups, paths.StagingDirectory, paths.StatePath);
        Assert.True(applier.Apply(config, paths.ManagedShortcutDirectory).Succeeded);
        var auto = Assert.Single(Directory.GetFiles(paths.BackupDirectory, "*.zip"));
        var ordinary = backups.Create("manual", paths.ConfigPath, paths.ManagedShortcutDirectory, paths.StatePath);
        var legacy = backups.Create("apply", paths.ConfigPath, paths.ManagedShortcutDirectory, paths.StatePath);
        File.Delete(legacy + ".automatic.json");
        var external = backups.Create("manual", paths.ConfigPath, paths.ManagedShortcutDirectory, destination: Path.Combine(root, "external.zip"));
        var brokenBackup = Write(Path.Combine(paths.BackupDirectory, "broken.zip"), "corrupt");
        var orphan = Write(Path.Combine(paths.BackupDirectory, "missing.zip.automatic.json"), "{}");
        var controls = new AuditControlStore(paths.DataDirectory);
        controls.SavePolicy(false);
        controls.MarkAttempt(new string('A', 64));
        for (var index = 0; index < 51; index++)
            controls.Record(new(1, DateTimeOffset.Now, "監査のみ", "差分なし", [], [], null, "fixture"));
        var brokenHistory = Write(Path.Combine(paths.DataDirectory, "audit-history", "ordinary.json"), "{}");
        var brokenLog = Write(paths.LogPath + ".1", "unrelated text");
        new FileLogger(paths.LogPath).Info("fixture");
        var unknown = Write(Path.Combine(paths.DataDirectory, "unmanaged.cmd"), "@echo off");
        var otherConfig = Write(other.ConfigPath, "other edition");
        var before = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));

        var result = preview.Inspect(false);

        Assert.True(result.CanReviewCandidates, string.Join(Environment.NewLine, result.Blockers));
        Assert.Equal(0, result.SavedAssignments);
        Assert.Equal(51, result.Items.Count(item => item.Kind == "監査履歴" && item.Disposition == RemovalDisposition.Candidate));
        foreach (var path in new[] { paths.ConfigPath, paths.StatePath, paths.LogPath, auto, auto + ".automatic.json" })
        {
            var item = Item(result, path);
            Assert.Equal(RemovalDisposition.Candidate, item.Disposition);
            Assert.Equal(before[path], item.Sha256);
            Assert.Equal(new FileInfo(path).Length, item.Bytes);
        }
        Assert.Equal(auto, Item(result, auto).AutomaticArchive);
        Assert.Equal(auto, Item(result, auto + ".automatic.json").AutomaticArchive);
        foreach (var path in new[] { ordinary, legacy })
            Assert.Equal(RemovalDisposition.RequiresIndividualSelection, Item(result, path).Disposition);
        foreach (var path in new[] { unknown, orphan, brokenBackup, brokenHistory, brokenLog, Path.Combine(paths.ManagedShortcutDirectory, ManagedShortcutApplier.OwnershipMarker) })
            Assert.Equal(RemovalDisposition.Keep, Item(result, path).Disposition);
        Assert.DoesNotContain(result.Items, item => item.Path == external || item.Path == otherConfig);
        Assert.Equal(before.Count, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count());
        foreach (var (path, hash) in before)
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    [Fact]
    public void EarlierManagedDestinationWithAnyRemainingFileBlocks()
    {
        var previous = Path.Combine(root, "previous");
        WriteSavedState(paths, previous);
        var link = Write(Path.Combine(previous, "Q - original.lnk"), "owned only by fixture");
        WriteMarker(previous);

        var result = preview.Inspect(false);

        Assert.False(result.CanReviewCandidates);
        Assert.Contains(previous, result.ManagedDirectories);
        Assert.Contains(paths.ManagedShortcutDirectory, result.ManagedDirectories);
        Assert.Equal(RemovalDisposition.Keep, Item(result, link).Disposition);
        Assert.Contains(result.Blockers, reason => reason.Contains(previous, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing-marker")]
    [InlineData("invalid-marker")]
    public void SuspiciousManagedDirectoryBlocks(string kind)
    {
        WriteSavedState(paths, paths.ManagedShortcutDirectory);
        if (kind == "missing-marker") Write(Path.Combine(paths.ManagedShortcutDirectory, "R - unknown.lnk"), "original");
        else Write(Path.Combine(paths.ManagedShortcutDirectory, ManagedShortcutApplier.OwnershipMarker), "invalid");

        var result = preview.Inspect(false);

        Assert.False(result.CanReviewCandidates);
        Assert.Contains(result.Blockers, reason => reason.Contains("ownership marker", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Items, item => item.Kind == "管理フォルダ" && item.Disposition != RemovalDisposition.Keep);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DebugIgnoresTheOtherEditionsConfiguredDestinationButBlocksAStatePointingThere(bool customProductionDirectory)
    {
        var productionDirectory = customProductionDirectory ? Path.Combine(root, "custom-production") : paths.ManagedShortcutDirectory;
        var config = new ShortcutConfig { Settings = new() { ManagedShortcutDirectory = productionDirectory } };
        WriteSavedState(other, other.ManagedShortcutDirectory, config);
        var productionLink = Write(Path.Combine(productionDirectory, "Z - production.lnk"), "keep original");
        var debug = new LocalDataRemovalPreview(other, paths);

        var isolated = debug.Inspect(false);

        Assert.True(isolated.CanReviewCandidates, string.Join(Environment.NewLine, isolated.Blockers));
        Assert.True(isolated.IsDevelopment);
        Assert.DoesNotContain(isolated.ManagedDirectories, directory => directory == productionDirectory);
        Assert.DoesNotContain(isolated.Items, item => item.Path == productionLink);

        WriteSavedState(other, productionDirectory, config);
        var blocked = debug.Inspect(false);
        Assert.False(blocked.CanReviewCandidates);
        Assert.Contains(blocked.Blockers, reason => reason.Contains("他エディション", StringComparison.Ordinal));
        Assert.DoesNotContain(blocked.ManagedDirectories, directory => directory == productionDirectory);
        Assert.DoesNotContain(blocked.Items, item => item.Path == productionLink);
        Assert.Equal("keep original", File.ReadAllText(productionLink));
    }

    [Theory]
    [InlineData("state-missing")]
    [InlineData("state-invalid")]
    [InlineData("edited-after-apply")]
    [InlineData("enabled-assignment")]
    [InlineData("unsaved")]
    [InlineData("journal")]
    [InlineData("legacy-recovery")]
    public void IncompleteOrUnsafeStateBlocks(string scenario)
    {
        var config = new ShortcutConfig();
        if (scenario == "enabled-assignment")
            config = config with { Shortcuts = [new() { Name = "fixture", Hotkey = new() { Key = "Z" }, Target = new WebsiteTarget { Url = "https://example.com" } }] };
        WriteSavedState(paths, paths.ManagedShortcutDirectory, config);
        switch (scenario)
        {
            case "state-missing": File.Delete(paths.StatePath); break;
            case "state-invalid": File.WriteAllText(paths.StatePath, "{}"); break;
            case "edited-after-apply": File.AppendAllText(paths.ConfigPath, Environment.NewLine); break;
            case "journal": Write(Path.Combine(paths.StagingDirectory, Guid.NewGuid().ToString("N"), "journal.json"), "{}"); break;
            case "legacy-recovery":
                var stage = Path.Combine(paths.StagingDirectory, Guid.NewGuid().ToString("N"));
                Write(Path.Combine(stage, "recovery.json"), "{}");
                File.WriteAllBytes(Path.Combine(stage, "complete"), [1]);
                break;
        }

        var result = preview.Inspect(scenario == "unsaved");

        Assert.False(result.CanReviewCandidates);
        Assert.NotEmpty(result.Blockers);
    }

    [Fact]
    public void MultipleHardLinksAndDirectoryAliasesStayOutsideCandidates()
    {
        WriteSavedState(paths, paths.ManagedShortcutDirectory);
        var external = Write(Path.Combine(root, "external.log"), $"{DateTimeOffset.Now:O} [INFO] fixture");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.LogPath)!);
        Assert.True(CreateHardLink(paths.LogPath, external, IntPtr.Zero), Marshal.GetLastWin32Error().ToString());
        var result = preview.Inspect(false);

        Assert.Equal(RemovalDisposition.Keep, Item(result, paths.LogPath).Disposition);
        Assert.DoesNotContain(result.Items, item => item.Path == external);

        var alias = Path.Combine(root, "alias");
        Directory.CreateDirectory(other.ManagedShortcutDirectory);
        Directory.CreateSymbolicLink(alias, other.ManagedShortcutDirectory);
        try
        {
            WriteSavedState(paths, Path.Combine(alias, "nested"));
            var redirected = preview.Inspect(false);
            Assert.False(redirected.CanReviewCandidates);
            Assert.Contains(redirected.Blockers, reason => reason.Contains(alias, StringComparison.Ordinal));
        }
        finally { Directory.Delete(alias); }
    }

    private ApplicationPaths Edition(string dataName, string managedName, bool debug)
    {
        var data = Path.Combine(root, dataName);
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"), Path.Combine(data, "logs", "shortcut-manager.log"),
            Path.Combine(data, "backups"), Path.Combine(data, "staging"), Path.Combine(root, managedName), debug);
    }

    private static void WriteSavedState(ApplicationPaths edition, string managed, ShortcutConfig? config = null)
    {
        Write(edition.ConfigPath, ConfigJson.Serialize(config ?? new ShortcutConfig()));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(edition.ConfigPath)));
        Write(edition.StatePath, JsonSerializer.Serialize(new { lastSuccessfulApply = DateTimeOffset.Now, configSha256 = hash, managedDirectory = managed }));
    }

    private static void WriteMarker(string directory) =>
        Write(Path.Combine(directory, ManagedShortcutApplier.OwnershipMarker), $"Shortcut Manager{Environment.NewLine}schemaVersion=1{Environment.NewLine}updatedAt={DateTimeOffset.Now:O}{Environment.NewLine}");

    private static string Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static RemovalItem Item(LocalDataRemovalPlan result, string path) => Assert.Single(result.Items, item => item.Path == path);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existingName, IntPtr attributes);
}
