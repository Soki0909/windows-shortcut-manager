using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Removal;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Tests;

[Collection("Apply transactions")]
public sealed class LocalDataRemovalStagingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"shortcut-manager-tests-removal-{Guid.NewGuid():N}");
    private readonly ApplicationPaths edition;
    private readonly ApplicationPaths other;
    private readonly LocalDataRemovalPreview preview;
    private readonly LocalDataRemovalPlan approved;
    private readonly VerifiedRemovalArchive archive;
    private readonly Dictionary<string, string> sourceHashes;
    private readonly string unknown;

    public LocalDataRemovalStagingTests()
    {
        edition = Edition("data", "managed", false);
        other = Edition("debug-data", "debug-managed", true);
        Directory.CreateDirectory(edition.DataDirectory);
        File.WriteAllText(edition.ConfigPath, ConfigJson.Serialize(new ShortcutConfig()));
        File.WriteAllText(edition.StatePath, JsonSerializer.Serialize(new
        {
            lastSuccessfulApply = DateTimeOffset.Now,
            configSha256 = Hash(edition.ConfigPath),
            managedDirectory = edition.ManagedShortcutDirectory
        }));
        new FileLogger(edition.LogPath).Info("staging test");
        unknown = Path.Combine(edition.DataDirectory, "user.bat");
        File.WriteAllText(unknown, "preserve");
        preview = new(edition, other);
        approved = preview.Inspect(false);
        sourceHashes = new[] { edition.ConfigPath, edition.StatePath, edition.LogPath }.ToDictionary(path => path, Hash);
        archive = new LocalDataRemovalArchive(edition, other).Create(approved, [], Path.Combine(root, "before.zip"), false);
    }

    [Fact]
    public void StagesAllFilesThenWaitsForExplicitDecisionAndRestoresOnCancel()
    {
        Assert.Equal(3, Service().Stage(approved, [], archive, false));
        Assert.All(sourceHashes, pair => Assert.False(File.Exists(pair.Key)));
        Assert.Contains(preview.Inspect(false).Blockers, reason => reason.Contains("未完了の削除準備", StringComparison.Ordinal));
        Assert.Equal(RemovalRecoveryOutcome.AwaitingDecision, Service().RecoverInterrupted());
        Assert.All(sourceHashes, pair => Assert.False(File.Exists(pair.Key)));
        Assert.Equal(RemovalRecoveryOutcome.Restored, Service().Cancel());
        AssertRestored();
        Assert.Equal(RemovalRecoveryOutcome.NothingPending, Service().Cancel());
        Assert.True(preview.Inspect(false).CanReviewCandidates);
    }

    [Theory]
    [InlineData("journal-prepared", 1)]
    [InlineData("file-staged", 1)]
    [InlineData("file-staged", 2)]
    [InlineData("file-staged", 3)]
    public void ResumesInterruptedStagingFromDurableIntent(string point, int occurrence)
    {
        var interruption = InterruptAt(point, occurrence);
        Assert.Throws<SimulatedExit>(() => interruption.Stage(approved, [], archive, false));

        Assert.Equal(RemovalRecoveryOutcome.Restored, Service().RecoverInterrupted());
        AssertRestored();
        Assert.Equal(RemovalRecoveryOutcome.NothingPending, Service().RecoverInterrupted());
    }

    [Theory]
    [InlineData("cancel-prepared", 1)]
    [InlineData("file-restored", 1)]
    [InlineData("file-restored", 2)]
    [InlineData("file-restored", 3)]
    public void ResumesInterruptedCancellationWithoutAnotherConfirmation(string point, int occurrence)
    {
        Service().Stage(approved, [], archive, false);
        Assert.Throws<SimulatedExit>(() => InterruptAt(point, occurrence).Cancel());

        Assert.Equal(RemovalRecoveryOutcome.Restored, Service().RecoverInterrupted());
        AssertRestored();
        Assert.Equal(RemovalRecoveryOutcome.NothingPending, Service().RecoverInterrupted());
    }

    [Fact]
    public void DurableMarkersSurviveExitAfterCompletion()
    {
        Assert.Throws<SimulatedExit>(() => InterruptAt("staging-complete", 1).Stage(approved, [], archive, false));
        Assert.Equal(RemovalRecoveryOutcome.AwaitingDecision, Service().RecoverInterrupted());
        Assert.Throws<SimulatedExit>(() => InterruptAt("cancel-complete", 1).Cancel());

        Assert.Equal(RemovalRecoveryOutcome.NothingPending, Service().RecoverInterrupted());
        AssertRestored();
    }

    [Theory]
    [InlineData("file-staged")]
    [InlineData("file-restored")]
    public void RestartsAfterFixtureProcessIsKilled(string checkpoint)
    {
        if (checkpoint == "file-restored") Service().Stage(approved, [], archive, false);
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "ShortcutManager.sln"))) repo = repo.Parent;
        var configuration = typeof(LocalDataRemovalStagingTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var exe = Path.Combine(repo!.FullName, "tests", "ShortcutManager.CrashDriver", "bin", configuration, "net10.0-windows", "ShortcutManager.CrashDriver.exe");
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(root);
        start.ArgumentList.Add("removal");
        start.ArgumentList.Add(checkpoint);
        using var process = Process.Start(start)!;
        try
        {
            Assert.True(process.WaitForExit(20000), "Fixture process timed out.");
            Assert.NotEqual(0, process.ExitCode);
            Assert.True(File.Exists(Path.Combine(root, "crash-confirmed-" + checkpoint)));
        }
        finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }

        Assert.Equal(RemovalRecoveryOutcome.Restored, Service().RecoverInterrupted());
        AssertRestored();
    }

    [Fact]
    public void ArchiveChangeStopsBeforeJournalOrMove()
    {
        File.AppendAllText(archive.Path, "tampered");
        Assert.Throws<InvalidDataException>(() => Service().Stage(approved, [], archive, false));

        Assert.All(sourceHashes, pair => Assert.Equal(pair.Value, Hash(pair.Key)));
        Assert.False(Directory.Exists(Path.Combine(edition.DataDirectory, "removal-recovery")));
        Assert.Contains("tampered", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(archive.Path)));
    }

    [Theory]
    [InlineData("both")]
    [InlineData("missing")]
    [InlineData("modified")]
    public void CancellationValidatesAllLocationsBeforeRestoringAnything(string conflict)
    {
        Service().Stage(approved, [], archive, false);
        var slots = Directory.GetFiles(OperationDirectory(), "stash-*").Order().ToArray();
        var suspect = slots[^1];
        switch (conflict)
        {
            case "both": File.WriteAllText(edition.StatePath, "new file"); break;
            case "missing": File.Move(suspect, Path.Combine(root, "retained-fixture")); break;
            case "modified": File.AppendAllText(suspect, "new bytes"); break;
        }
        var expectedStashes = Directory.GetFiles(OperationDirectory(), "stash-*").ToDictionary(path => path, Hash);

        Assert.Throws<InvalidDataException>(() => Service().Cancel());

        Assert.False(File.Exists(edition.ConfigPath));
        Assert.False(File.Exists(edition.LogPath));
        Assert.All(expectedStashes, pair => Assert.Equal(pair.Value, Hash(pair.Key)));
        if (conflict == "both") Assert.Equal("new file", File.ReadAllText(edition.StatePath));
        Assert.False(File.Exists(Path.Combine(OperationDirectory(), "cancelled")));
    }

    [Fact]
    public void OccupyingDestinationAfterPreflightCannotBeOverwritten()
    {
        Service().Stage(approved, [], archive, false);
        var conflict = Service(point => { if (point == "cancel-prepared") File.WriteAllText(edition.ConfigPath, "late write"); });

        Assert.Throws<IOException>(() => conflict.Cancel());

        Assert.Equal("late write", File.ReadAllText(edition.ConfigPath));
        Assert.Equal(3, Directory.GetFiles(OperationDirectory(), "stash-*").Length);
        File.Move(edition.ConfigPath, Path.Combine(root, "kept-conflict"));
        Assert.Equal(RemovalRecoveryOutcome.Restored, Service().RecoverInterrupted());
        AssertRestored();
        Assert.Equal("late write", File.ReadAllText(Path.Combine(root, "kept-conflict")));
    }

    [Fact]
    public void JournalCannotRedirectRestoreOutsideKnownDataPaths()
    {
        Service().Stage(approved, [], archive, false);
        var intent = Path.Combine(OperationDirectory(), "journal.json");
        var json = JsonNode.Parse(File.ReadAllText(intent))!;
        json["Files"]![0]!["RelativePath"] = @"..\outside.txt";
        File.WriteAllText(intent, json.ToJsonString());

        Assert.Throws<InvalidDataException>(() => Service().Cancel());

        Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        Assert.Equal(3, Directory.GetFiles(OperationDirectory(), "stash-*").Length);
        Assert.Equal("preserve", File.ReadAllText(unknown));
    }

    [Fact]
    public void RollbackCanReadItsStashWhenTheExternalArchiveIsUnavailable()
    {
        Service().Stage(approved, [], archive, false);
        var saved = Path.Combine(root, "disconnected.zip");
        File.Move(archive.Path, saved);

        Assert.Equal(RemovalRecoveryOutcome.Restored, Service().Cancel());

        Assert.All(sourceHashes, pair => Assert.Equal(pair.Value, Hash(pair.Key)));
        Assert.Equal(archive.Sha256, Hash(saved));
    }

    private static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private LocalDataRemovalStaging Service(Action<string>? checkpoint = null) => new(edition, other, checkpoint);

    private LocalDataRemovalStaging InterruptAt(string point, int occurrence) =>
        Service(actual => { if (actual == point && --occurrence == 0) throw new SimulatedExit(); });

    private string OperationDirectory() => Assert.Single(Directory.GetDirectories(Path.Combine(edition.DataDirectory, "removal-recovery")));

    private void AssertRestored()
    {
        Assert.All(sourceHashes, pair => Assert.Equal(pair.Value, Hash(pair.Key)));
        Assert.Equal(archive.Sha256, Hash(archive.Path));
        Assert.Equal("preserve", File.ReadAllText(unknown));
        Assert.Empty(Directory.GetFiles(OperationDirectory(), "stash-*"));
    }

    private ApplicationPaths Edition(string dataName, string managedName, bool debug)
    {
        var data = Path.Combine(root, dataName);
        return new(data, Path.Combine(data, "config.json"), Path.Combine(data, "state.json"),
            Path.Combine(data, "logs", "shortcut-manager.log"), Path.Combine(data, "backups"), Path.Combine(data, "staging"), Path.Combine(root, managedName), debug);
    }

    public void Dispose() => Directory.Delete(root, true);

    private sealed class SimulatedExit : Exception;
}
