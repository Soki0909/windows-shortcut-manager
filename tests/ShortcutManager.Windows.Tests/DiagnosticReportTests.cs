using System.Text;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Diagnostics;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.Windows.Tests;

public sealed class DiagnosticReportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "diagnostic-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly DiagnosticState State = new(true, false, false, true, false, false, true, true, false);
    public DiagnosticReportTests() => Directory.CreateDirectory(root);

    [Fact]
    public void HostileSourceStringsAreOmittedWhileCountsAndKnownCodesSurvive()
    {
        const string secret = "private-canary-名前-https://private.invalid/a?q=token";
        var id = Guid.NewGuid();
        var config = new ShortcutConfig { Settings = new() { ManagedShortcutDirectory = secret, Chrome = new() { ExtensionId = secret, ExecutablePath = secret } },
            Shortcuts = [new() { Id = id, Name = secret, Hotkey = new() { Key = secret }, Target = new ApplicationTarget { Value = secret, Arguments = secret, WorkingDirectory = secret } }] };
        var scan = new InventoryScanReport([new(secret, secret, secret, secret, secret, secret, secret, secret, false, secret)], [new(secret, secret, secret)], [secret]);
        var report = DiagnosticReport.Capture(new(1, 0, 0), true, config, State,
            [new("missing-bat", secret, id, secret), new(secret, secret, id, secret), new("missing-bat", secret)],
            [new(secret, secret, secret, secret)], scan);
        var json = report.Preview();
        using var parsed = JsonDocument.Parse(json); var data = parsed.RootElement;
        Assert.DoesNotContain("private-canary", json); Assert.DoesNotContain("private.invalid", json); Assert.DoesNotContain(id.ToString(), json);
        Assert.Equal(1, data.GetProperty("candidate").GetProperty("applications").GetInt32());
        Assert.Equal(1, data.GetProperty("audit").GetProperty("lastObservedDifferenceCount").GetInt32());
        Assert.Equal(1, data.GetProperty("scan").GetProperty("readFailures").GetInt32());
        Assert.False(data.GetProperty("scan").GetProperty("completeForRequestedScope").GetBoolean());
        Assert.Contains(data.GetProperty("validation").GetProperty("errors").EnumerateArray(), value => value.GetProperty("code").GetString() == "missing-bat" && value.GetProperty("count").GetInt32() == 2);
        Assert.Contains("other-validation-error", json);
        config.Shortcuts.Clear(); Assert.Equal(json, report.Preview());
    }

    [Fact]
    public void OptionalNotesAreExplicitBoundedAndNeverModifyTheDefaultSnapshot()
    {
        var report = DiagnosticReport.Capture(new(1, 0, 0), false, new(), State, [], [], null);
        var baseline = report.Preview();
        using var defaultDocument = JsonDocument.Parse(baseline);
        Assert.False(defaultDocument.RootElement.TryGetProperty("userNotes", out _));
        var note = "自分で追加した C:\\Private\\notes https://example.invalid/?private=true\n再現手順";
        using var parsed = JsonDocument.Parse(report.Preview(note));
        Assert.Equal(note, parsed.RootElement.GetProperty("userNotes").GetString());
        Assert.Equal(baseline, report.Preview()); Assert.Equal(baseline, report.Preview(" "));
        Assert.Throws<ArgumentException>(() => report.Preview(new string('a', DiagnosticReport.MaxNotesLength + 1)));
    }

    [Fact]
    public void UnperformedScanAndUnverifiedAuditNeverBecomeSuccessfulChecks()
    {
        using var parsed = JsonDocument.Parse(DiagnosticReport.Capture(new(1, 0, 0), false, new(), State, [], [], null).Preview());
        var data = parsed.RootElement;
        Assert.False(data.TryGetProperty("scan", out _));
        Assert.False(data.GetProperty("state").GetProperty("savedLinksVerified").GetBoolean());
        Assert.True(data.GetProperty("audit").GetProperty("zeroDifferencesDoesNotProveAuditRan").GetBoolean());
        Assert.Equal("not-inferred", data.GetProperty("audit").GetProperty("physicalHotkeys").GetString());
    }

    [Fact]
    public void SavePublishesExactlyTheReviewedUnicodeTextAndKeepsExistingFiles()
    {
        var path = Path.Combine(root, "診断.json"); const string preview = "{\"userNotes\":\"日本語🧪\"}";
        DiagnosticFile.SaveNew(path, preview);
        Assert.Equal(new UTF8Encoding(false).GetBytes(preview), File.ReadAllBytes(path));
        Assert.Throws<IOException>(() => DiagnosticFile.SaveNew(path, "replacement"));
        Assert.Equal(preview, File.ReadAllText(path)); Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void FailedSaveDoesNotCreatePartialDestinationOrDirectories()
    {
        var destination = Path.Combine(root, "absent", "report.json");
        Assert.Throws<DirectoryNotFoundException>(() => DiagnosticFile.SaveNew(destination, "{}"));
        Assert.False(Directory.Exists(Path.GetDirectoryName(destination)));
        Assert.Throws<IOException>(() => DiagnosticFile.SaveNew(root, "{}"));
        Assert.Throws<ArgumentException>(() => DiagnosticFile.SaveNew(Path.Combine(root, "oversized.json"), new string('a', 128 * 1024 + 1)));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Fact]
    public void FailedLogWriteStillRecordsOnlyFixedErrorCodeAndSessionCount()
    {
        // A directory at the log path forces a write failure without touching a user's log.
        var logger = new Logging.FileLogger(root);
        logger.Error("private-path-name", new UnauthorizedAccessException("https://private.invalid/secret"));
        Assert.NotNull(logger.LastFailure);
        Assert.Equal((1, DiagnosticErrorCode.AccessDenied), logger.DiagnosticErrors);
        logger.Error("private-path-name", new InvalidDataException("private data"));
        Assert.Equal((2, DiagnosticErrorCode.InvalidData), logger.DiagnosticErrors);
        var state = State with { LoggingUnavailable = true, RecordedLogErrorCount = logger.DiagnosticErrors.Count, LastRecordedLogErrorCode = logger.DiagnosticErrors.LastCode };
        var json = DiagnosticReport.Capture(new(1, 0, 0), false, new(), state, [], [], null).Preview();
        Assert.Contains("InvalidData", json); Assert.DoesNotContain("private", json);
    }

    [Fact]
    public async Task ConcurrentSavesPublishOneCompleteReportWithoutReplacingIt()
    {
        var path = Path.Combine(root, "race.json");
        using var start = new ManualResetEventSlim();
        var writes = new[] { "first reviewed text", "second reviewed text" }.Select(text => Task.Run(() =>
        {
            start.Wait();
            try { DiagnosticFile.SaveNew(path, text); return true; } catch (IOException) { return false; }
        })).ToArray();
        start.Set(); var results = await Task.WhenAll(writes);
        Assert.Single(results, value => value);
        Assert.Contains(File.ReadAllText(path), new[] { "first reviewed text", "second reviewed text" });
        Assert.Single(Directory.GetFiles(root));
    }
    public void Dispose() => Directory.Delete(root, true);
}
