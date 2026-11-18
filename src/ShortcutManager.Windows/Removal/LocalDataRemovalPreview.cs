using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.Windows.Removal;

public enum RemovalDisposition { Candidate, RequiresIndividualSelection, Keep }

public sealed record RemovalItem(string Path, string Kind, RemovalDisposition Disposition, string Reason,
    long? Bytes = null, string? Sha256 = null, string? AutomaticArchive = null)
{
    internal RemovalFileIdentity? Identity { get; init; }
}

public sealed record LocalDataRemovalPlan(string DataDirectory, bool IsDevelopment, int? SavedAssignments,
    IReadOnlyList<string> ManagedDirectories, IReadOnlyList<RemovalItem> Items, IReadOnlyList<string> Blockers)
{
    public bool CanReviewCandidates => Blockers.Count == 0 && Items.Any(item => item.Disposition != RemovalDisposition.Keep);
    internal string? UserSid { get; init; }
}

// These observations are for display and review. A later operation must revalidate them.
public sealed class LocalDataRemovalPreview(ApplicationPaths paths, ApplicationPaths otherEdition)
{
    public LocalDataRemovalPlan Inspect(bool hasUnsavedChanges)
    {
        var items = new List<RemovalItem>();
        var blockers = new List<string>();
        var managedDirectories = new List<string>();
        var data = paths.DataDirectory;
        ShortcutConfig? config = null;
        string? configHash = null;
        AppliedState? state = null;
        if (hasUnsavedChanges) blockers.Add("未保存の変更を反映するか破棄してください。");

        try
        {
            data = RemovalFileInspection.DirectoryPath(paths.DataDirectory);
            var otherData = RemovalFileInspection.DirectoryPath(otherEdition.DataDirectory);
            var otherManaged = RemovalFileInspection.DirectoryPath(otherEdition.ManagedShortcutDirectory);
            if (Overlaps(data, otherData) || Overlaps(data, otherManaged))
                throw new InvalidDataException("現在の保存先が他エディションの保存先と重なっています。");

            var configPath = DataFile(data, paths.ConfigPath);
            var statePath = DataFile(data, paths.StatePath);
            if (RemovalFileInspection.Attributes(configPath) is not null)
            {
                var item = InspectFile(configPath, "設定", BackupArchive.MaxConfigBytes, input =>
                {
                    using var reader = new StreamReader(input, Encoding.UTF8, true, leaveOpen: true);
                    config = ConfigJson.Deserialize(reader.ReadToEnd());
                });
                items.Add(item);
                configHash = item.Sha256;
                if (item.Disposition == RemovalDisposition.Keep)
                    blockers.Add("保存済み設定から管理フォルダを確定できません。");
            }
            if (RemovalFileInspection.Attributes(statePath) is not null)
            {
                var item = InspectFile(statePath, "反映状態", BackupArchive.MaxEntryBytes, input => state = ReadAppliedState(input));
                items.Add(item);
                if (item.Disposition == RemovalDisposition.Keep)
                    blockers.Add("反映状態から以前の管理フォルダを確定できません。");
            }
            if (configHash is not null || state is not null)
            {
                if (configHash is null || state is null || !configHash.Equals(state.ConfigSha256, StringComparison.OrdinalIgnoreCase))
                    blockers.Add("保存済み設定と最後の反映状態が一致しません。反映先を復旧してから確認してください。");
            }

            if (config?.Shortcuts.Any(item => item.Enabled) == true)
                blockers.Add("有効な割当を解除してから確認してください。");

            var previousDirectory = state?.ManagedDirectory;
            if (paths.IsDevelopment && previousDirectory is not null && !RemovalFileInspection.SamePath(
                Path.GetFullPath(previousDirectory), Path.GetFullPath(paths.ManagedShortcutDirectory)))
            {
                blockers.Add("反映状態が開発版の管理先と一致しません。他エディションの反映状態を確認してください。");
                previousDirectory = null;
            }
            var managedPaths = new[] { paths.ManagedShortcutDirectory, paths.ResolveManagedDirectory(config?.Settings.ManagedShortcutDirectory), previousDirectory };
            foreach (var path in managedPaths.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
                InspectManagedDirectory(path, data, otherData, otherManaged, managedDirectories, items, blockers);

            var policyPath = Path.Combine(data, AuditControlStore.PolicyFilename);
            var attemptPath = Path.Combine(data, AuditControlStore.AttemptFilename);
            AddKnownFile(policyPath, "監査設定", 4096, input => _ = AuditControlStore.ReadPolicyRecord(input), items);
            AddKnownFile(attemptPath, "修復試行記録", 4096, AuditControlStore.ValidateAttemptRecord, items);

            var history = Path.Combine(data, AuditControlStore.HistoryDirectoryName);
            foreach (var path in ListCategory(history, "監査履歴", items))
                items.Add(IsAuditHistoryName(Path.GetFileName(path))
                    ? InspectFile(path, "監査履歴", AuditControlStore.MaxHistoryBytes, ValidateHistory)
                    : Keep(path, "監査履歴", "監査履歴として識別できない項目です。"));

            var log = DataFile(data, paths.LogPath, allowSubdirectory: true);
            var logs = Path.GetDirectoryName(log)!;
            var logNames = Enumerable.Range(1, 4).Select(index => Path.GetFileName(log) + "." + index)
                .Append(Path.GetFileName(log)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in ListCategory(logs, "ログ", items))
                items.Add(logNames.Contains(Path.GetFileName(path))
                    ? InspectFile(path, "ログ", BackupArchive.MaxEntryBytes, ValidateLog)
                    : Keep(path, "ログ", "ログとして識別できない項目です。"));

            var backups = DataFile(data, paths.BackupDirectory);
            InspectBackups(backups, items);
            var staging = DataFile(data, paths.StagingDirectory);
            InspectStaging(staging, items, blockers);

            var known = new[] { configPath, statePath, policyPath, attemptPath, history, logs, backups, staging }.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Entries(data).Where(path => !known.Contains(path)))
                items.Add(Keep(path, "その他", "この版が所有するデータとして識別できない項目です。"));
        }
        catch (Exception error) when (IsReadError(error))
        {
            blockers.Add(error.Message);
        }

        using var user = WindowsIdentity.GetCurrent();
        return new(data, paths.IsDevelopment, config?.Shortcuts.Count, managedDirectories.AsReadOnly(),
            Array.AsReadOnly(items.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray()), blockers.AsReadOnly())
        { UserSid = user.User?.Value };
    }

    private static void InspectManagedDirectory(string path, string data, string otherData, string otherManaged,
        List<string> directories, List<RemovalItem> items, List<string> blockers)
    {
        try
        {
            var directory = RemovalFileInspection.DirectoryPath(path);
            if (directories.Contains(directory, StringComparer.OrdinalIgnoreCase)) return;
            directories.Add(directory);
            if (Overlaps(directory, otherData) || Overlaps(directory, otherManaged) || Overlaps(directory, data))
                throw new InvalidDataException("管理フォルダがエディションのデータ保存先または他エディションの管理先と重なっています。");

            ManagedShortcutApplier.EnsureOwnershipSafe(directory);
            var existing = Entries(directory).ToArray();
            var markers = existing.Where(IsOwnershipMarker).ToArray();
            if (markers.Length > 1) throw new InvalidDataException("所有マーカーの名前が重複しています。");
            var marker = markers.SingleOrDefault();
            if (marker is not null)
            {
                using var input = RemovalFileInspection.OpenFile(marker, 1024);
                items.Add(Describe(marker, input, "管理フォルダ", "所有マーカーを保持します。", RemovalDisposition.Keep));
            }
            foreach (var entry in existing.Where(entry => !IsOwnershipMarker(entry)))
                items.Add(Keep(entry, "管理フォルダ", "管理フォルダの内容を保護します。"));
            if (existing.Any(entry => !IsOwnershipMarker(entry)))
                throw new InvalidDataException("所有マーカー以外の項目が残っています。管理ショートカットの解除または内容確認が必要です。");
        }
        catch (Exception error) when (IsReadError(error))
        {
            blockers.Add($"{path}: {error.Message}");
        }
    }

    private static void InspectBackups(string directory, List<RemovalItem> items)
    {
        const string receiptSuffix = ".automatic.json";
        var entries = ListCategory(directory, "バックアップ", items);
        var paired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zip in entries.Where(path => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var input = RemovalFileInspection.OpenFile(zip, BackupArchive.MaxTotalBytes);
                var (manifest, config) = BackupArchive.Read(zip);
                _ = RestoreService.ParseConfig(config);
                var receiptPath = zip + receiptSuffix;
                if (manifest.Reason is "apply" or "restore" && entries.Contains(receiptPath, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var receipt = RemovalFileInspection.OpenFile(receiptPath, 4096);
                        if (BackupService.HasAutomaticReceipt(zip))
                        {
                            items.Add(Describe(zip, input, "自動バックアップ", "バックアップ形式と自動作成記録のハッシュが一致します。", archive: zip));
                            items.Add(Describe(receiptPath, receipt, "自動作成記録", "対応する自動バックアップと組にします。", archive: zip));
                            paired.Add(receiptPath);
                            continue;
                        }
                    }
                    catch (Exception error) when (IsReadError(error))
                    {
                        // A readable backup without a verified receipt stays opt-in per file.
                    }
                }
                items.Add(Describe(zip, input, "バックアップ", "自動作成の根拠がないため、対象にするには個別選択が必要です。",
                    RemovalDisposition.RequiresIndividualSelection));
            }
            catch (Exception error) when (IsReadError(error))
            {
                items.Add(Keep(zip, "バックアップ", error.Message));
            }
        }
        foreach (var path in entries.Where(path => !Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) && !paired.Contains(path)))
            items.Add(Keep(path, "バックアップ", "対応する自動バックアップを確認できないか、バックアップ用の形式ではありません。"));
    }

    private static void InspectStaging(string directory, List<RemovalItem> items, List<string> blockers)
    {
        try
        {
            directory = RemovalFileInspection.DirectoryPath(directory);
            foreach (var path in Entries(directory))
            {
                items.Add(Keep(path, "復旧・退避", "反映や復旧の処理用データを保持します。"));
                var attributes = RemovalFileInspection.Attributes(path);
                if (attributes is null || (attributes.Value & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"復旧用項目を照会できません: {path}");
                if ((attributes.Value & FileAttributes.Directory) == 0) continue;
                var hasJournal = RemovalFileInspection.Attributes(Path.Combine(path, ApplyJournal.Filename)) is not null;
                var hasLegacyRecord = RemovalFileInspection.Attributes(Path.Combine(path, "recovery.json")) is not null;
                if (!hasJournal && !hasLegacyRecord) continue;

                var terminal = Path.Combine(path, ApplyJournal.Terminal);
                if (hasJournal && !hasLegacyRecord && Guid.TryParseExact(Path.GetFileName(path), "N", out _)
                    && RemovalFileInspection.Attributes(terminal) is not null)
                {
                    using var marker = RemovalFileInspection.OpenFile(terminal, 1);
                    if (marker.ReadByte() == 1) continue;
                }
                blockers.Add($"反映または復元の中断記録があります: {path}");
            }
        }
        catch (Exception error) when (IsReadError(error))
        {
            blockers.Add(error.Message);
        }
    }

    private static string[] ListCategory(string directory, string kind, List<RemovalItem> items)
    {
        try { return Entries(RemovalFileInspection.DirectoryPath(directory)).ToArray(); }
        catch (Exception error) when (IsReadError(error))
        {
            items.Add(Keep(directory, kind, error.Message));
            return [];
        }
    }

    private static IEnumerable<string> Entries(string directory) =>
        RemovalFileInspection.Attributes(directory) is null ? [] : Directory.EnumerateFileSystemEntries(directory);

    private static void AddKnownFile(string path, string kind, int maxBytes, Action<Stream> validate, List<RemovalItem> items)
    {
        if (RemovalFileInspection.Attributes(path) is not null) items.Add(InspectFile(path, kind, maxBytes, validate));
    }

    private static RemovalItem InspectFile(string path, string kind, int maxBytes, Action<Stream> validate)
    {
        try
        {
            using var input = RemovalFileInspection.OpenFile(path, maxBytes);
            validate(input);
            return Describe(path, input, kind, "このエディションの保存先と既知の形式を確認しました。");
        }
        catch (Exception error) when (IsReadError(error))
        {
            return Keep(path, kind, error.Message);
        }
    }

    private static RemovalItem Describe(string path, FileStream input, string kind, string reason,
        RemovalDisposition disposition = RemovalDisposition.Candidate, string? archive = null)
    {
        input.Position = 0;
        return new(path, kind, disposition, reason, input.Length, Convert.ToHexString(SHA256.HashData(input)), archive)
        { Identity = RemovalFileInspection.Identity(input, path) };
    }

    private static RemovalItem Keep(string path, string kind, string reason) => new(path, kind, RemovalDisposition.Keep, reason);

    private string DataFile(string data, string path, bool allowSubdirectory = false)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("保存先のパスが絶対パスではありません。");
        var relative = Path.GetRelativePath(paths.DataDirectory, path);
        var components = relative.Split(Path.DirectorySeparatorChar);
        if (components.Length != (allowSubdirectory ? 2 : 1) || Path.IsPathRooted(relative)
            || components.Any(item => item is "" or "." or ".." || item.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("ファイルの保存先がこのエディションの保存フォルダと一致しません。");
        return Path.Combine(data, relative);
    }

    private static bool Overlaps(string left, string right) =>
        RemovalFileInspection.IsWithin(left, right) || RemovalFileInspection.IsWithin(right, left);

    private static bool IsOwnershipMarker(string path) =>
        Path.GetFileName(path).Equals(ManagedShortcutApplier.OwnershipMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsReadError(Exception error) =>
        error is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException
            or ArgumentException or NotSupportedException or System.Security.SecurityException;

    private static bool IsAuditHistoryName(string name)
    {
        var timestampLength = AuditControlStore.HistoryTimestampFormat.Length;
        return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && name.Length == timestampLength + 38
            && name[timestampLength] == '-'
            && DateTime.TryParseExact(name[..timestampLength], AuditControlStore.HistoryTimestampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && Guid.TryParseExact(name[(timestampLength + 1)..^5], "N", out _);
    }

    private static void ValidateHistory(Stream input)
    {
        var record = AuditControlStore.ReadHistoryRecord(input);
        if (record.At == default || string.IsNullOrWhiteSpace(record.Mode) || string.IsNullOrWhiteSpace(record.Outcome) || record.Message is null)
            throw new InvalidDataException("監査履歴の形式を確認できません。");
    }

    private static void ValidateLog(Stream input)
    {
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true, leaveOpen: true);
        if (reader.EndOfStream) throw new InvalidDataException("空のログは所有を確認できません。");
        while (reader.ReadLine() is { } line)
        {
            var separator = line.IndexOf(' ');
            if (separator < 0 || !DateTimeOffset.TryParseExact(line[..separator], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !(line.AsSpan(separator).StartsWith(" [INFO] ", StringComparison.Ordinal)
                    || line.AsSpan(separator).StartsWith(" [ERROR] ", StringComparison.Ordinal)))
                throw new InvalidDataException("全行をこの版のログ形式として確認できません。");
        }
    }

    private static AppliedState ReadAppliedState(Stream input)
    {
        var state = JsonSerializer.Deserialize<AppliedState>(input, StateOptions);
        if (state is not { LastSuccessfulApply: var timestamp, ConfigSha256.Length: 64, ManagedDirectory.Length: > 0 }
            || timestamp == default || state.ConfigSha256.Any(character => !Uri.IsHexDigit(character))
            || !Path.IsPathFullyQualified(state.ManagedDirectory))
            throw new InvalidDataException("反映状態の形式または管理フォルダを確認できません。");
        return state;
    }

    private static readonly JsonSerializerOptions StateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    private sealed record AppliedState(DateTimeOffset LastSuccessfulApply, string ConfigSha256, string ManagedDirectory);
}
