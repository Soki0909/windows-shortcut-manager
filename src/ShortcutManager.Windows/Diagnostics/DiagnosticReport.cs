using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.Windows.Diagnostics;

[JsonConverter(typeof(JsonStringEnumConverter<DiagnosticErrorCode>))]
public enum DiagnosticErrorCode { None, OperationFailed, AccessDenied, IoFailure, InvalidData, InvalidFormat, UnsupportedConfigVersion, InvalidArgument }

public sealed record DiagnosticState(bool ConfigLoaded, bool RecoveryRequired, bool IncompatibleConfig,
    bool UnsavedChanges, bool SavedConfigChangedExternally, bool SavedLinksVerified,
    bool ShellReflectionPending, bool AuditOnly, bool LoggingUnavailable,
    int RecordedLogErrorCount = 0, DiagnosticErrorCode LastRecordedLogErrorCode = DiagnosticErrorCode.None);

// Only counts, booleans and fixed codes cross this boundary. Do not serialize the
// source config, audit values, exception messages or scan paths into the report.
public sealed class DiagnosticReport
{
    public const int MaxNotesLength = 8000;
    private static readonly HashSet<string> KnownCodes = new(StringComparer.Ordinal)
    {
        "invalid-structure", "invalid-schema-version", "invalid-dispatch-version", "too-many-shortcuts", "duplicate-id",
        "duplicate-hotkey", "invalid-name", "invalid-modifiers", "invalid-key", "invalid-website",
        "invalid-application-uri", "invalid-shortcut-target", "invalid-bat-extension", "invalid-group-name",
        "invalid-extension-id", "missing-application-target", "missing-bat", "relative-target-path",
        "target-is-directory", "relative-working-directory", "missing-working-directory"
    };
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly string baseline;

    private DiagnosticReport(object report) => baseline = JsonSerializer.Serialize(report, Options);

    public static DiagnosticReport Capture(Version productVersion, bool development, ShortcutConfig candidate,
        DiagnosticState state, IReadOnlyList<ValidationIssue> validation,
        IReadOnlyList<AuditDifference> audit, InventoryScanReport? scan)
    {
        var codes = validation.GroupBy(issue => KnownCodes.Contains(issue.Code) ? issue.Code : "other-validation-error")
            .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new { Code = group.Key, Count = group.Count() }).ToArray();
        return new(new
        {
            Format = "ShortcutManager.Diagnostic.v1", CapturedAtUtc = DateTimeOffset.UtcNow,
            Product = new { Version = productVersion.ToString(), Edition = development ? "Debug" : "Release",
                SupportedSchema = ConfigJson.SupportedSchemaVersion, SupportedProtocol = ConfigJson.SupportedDispatchProtocolVersion },
            Environment = new { WindowsVersion = System.Environment.OSVersion.Version.ToString(),
                RuntimeVersion = System.Environment.Version.ToString(), ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString() },
            Privacy = new { Paths = "omitted", Urls = "omitted", Names = "omitted", Ids = "omitted", RawLogs = "omitted",
                UserNotes = "included only when explicitly supplied; not automatically redacted" },
            State = state,
            Candidate = new { Total = candidate.Shortcuts.Count, Enabled = candidate.Shortcuts.Count(item => item.Enabled),
                Websites = candidate.Shortcuts.Count(item => item.Target is WebsiteTarget),
                Applications = candidate.Shortcuts.Count(item => item.Target is ApplicationTarget),
                Bats = candidate.Shortcuts.Count(item => item.Target is BatTarget),
                ChromeGroups = candidate.Shortcuts.Count(item => item.Target is ChromeTabGroupTarget) },
            Validation = new { Scope = "candidate-structure-only-no-filesystem-probe", Errors = codes },
            Audit = new { LastObservedDifferenceCount = audit.Count, Values = "omitted",
                ZeroDifferencesDoesNotProveAuditRan = true, PhysicalHotkeys = "not-inferred" },
            Scan = scan is null ? null : new { Items = scan.Items.Count, ReadFailures = scan.ReadFailureCount,
                UncheckedOrExcludedLocations = scan.Issues.Count, CompleteForRequestedScope = scan.IsComplete }
        });
    }

    public string Preview(string? userNotes = null)
    {
        if (userNotes?.Length > MaxNotesLength) throw new ArgumentException("補足は8000文字以内にしてください。", nameof(userNotes));
        if (string.IsNullOrWhiteSpace(userNotes)) return baseline;
        var document = System.Text.Json.Nodes.JsonNode.Parse(baseline)!.AsObject();
        document["userNotes"] = userNotes;
        return document.ToJsonString(Options);
    }
}
