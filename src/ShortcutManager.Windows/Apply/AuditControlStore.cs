using System.Security.Cryptography;
using System.Text.Json;

namespace ShortcutManager.Windows.Apply;

public sealed record AuditPolicy(int FormatVersion,bool AuditOnly);
public sealed record AuditHistoryRecord(int FormatVersion,DateTimeOffset At,string Mode,string Outcome,IReadOnlyList<AuditDifference> Before,IReadOnlyList<AuditDifference> After,string? BackupPath,string Message);
public sealed record AuditHistoryItem(string Path,AuditHistoryRecord? Record,string? Error);

public sealed class AuditControlStore(string directory)
{
    internal const int MaxHistoryBytes=4*1024*1024;
    internal const string PolicyFilename="audit-settings.json";
    internal const string AttemptFilename="repair-attempt.json";
    internal const string HistoryDirectoryName="audit-history";
    internal const string HistoryTimestampFormat="yyyyMMddHHmmssfffffff";
    private string PolicyPath=>Path.Combine(directory,PolicyFilename);
    private string AttemptPath=>Path.Combine(directory,AttemptFilename);
    private string HistoryDirectory=>Path.Combine(directory,HistoryDirectoryName);
    public (bool AuditOnly,string? Warning) ReadPolicy()
    {
        try {using var input=OpenRecord(PolicyPath);return(ReadPolicyRecord(input).AuditOnly,null);}
        catch(Exception error) when(error is FileNotFoundException or DirectoryNotFoundException) {return(false,null);}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) {return(true,"監査設定を読めないため自動修復を保留します: "+error.Message);}
    }
    public void SavePolicy(bool auditOnly)=>Write(PolicyPath,new AuditPolicy(1,auditOnly));
    public bool WasAttempted(string fingerprint)
    {
        try {using var input=OpenRecord(AttemptPath);return ReadAttemptRecord(input).Fingerprint==fingerprint;}
        catch(Exception error) when(error is FileNotFoundException or DirectoryNotFoundException) {return false;}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) {return true;}
    }
    public void MarkAttempt(string fingerprint)=>Write(AttemptPath,new Attempt(1,fingerprint,DateTimeOffset.Now));
    public void Record(AuditHistoryRecord record)=>Write(Path.Combine(HistoryDirectory,$"{DateTime.UtcNow.ToString(HistoryTimestampFormat, System.Globalization.CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}.json"),record);
    public IReadOnlyList<AuditHistoryItem> ReadHistory()
    {
        if(!Directory.Exists(HistoryDirectory))return [];
        var items=new List<AuditHistoryItem>();
        try {
            foreach(var path in Directory.EnumerateFiles(HistoryDirectory,"*.json").OrderDescending(StringComparer.Ordinal).Take(50)) {
                try {using var input=OpenRecord(path);items.Add(new(path,ReadHistoryRecord(input),null));}
                catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) {items.Add(new(path,null,error.Message));}
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) {items.Add(new(HistoryDirectory,null,error.Message));}
        return items;
    }
    public static string Fingerprint(string config,IReadOnlyList<AuditDifference> differences)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config+JsonSerializer.Serialize(differences.OrderBy(item=>item.File,StringComparer.OrdinalIgnoreCase).ThenBy(item=>item.Field,StringComparer.Ordinal)))));
    private sealed record Attempt(int FormatVersion,string Fingerprint,DateTimeOffset At);
    internal static AuditPolicy ReadPolicyRecord(Stream input)
    {
        var policy = Read<AuditPolicy>(input, 4096);
        return policy is { FormatVersion: 1 } ? policy : throw new InvalidDataException("対応しない監査設定です。");
    }

    internal static void ValidateAttemptRecord(Stream input) => _ = ReadAttemptRecord(input);

    private static Attempt ReadAttemptRecord(Stream input)
    {
        var attempt = Read<Attempt>(input, 4096);
        if (attempt is not { FormatVersion: 1, Fingerprint.Length: 64 } || attempt.Fingerprint.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("対応しない監査の修復試行記録です。");
        return attempt;
    }

    internal static AuditHistoryRecord ReadHistoryRecord(Stream input)
    {
        var record = Read<AuditHistoryRecord>(input, MaxHistoryBytes);
        if (record is not { FormatVersion: 1, Before.Count: <= 256, After.Count: <= 256 }
            || record.Before.Concat(record.After).Any(item => item is null || item.File is null || item.Field is null || item.Actual is null || item.Expected is null))
            throw new InvalidDataException("対応しない履歴です。");
        return record;
    }

    private static FileStream OpenRecord(string path)
    {
        var input = File.OpenRead(path);
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("リンクになっている記録は読みません。");
            return input;
        }
        catch
        {
            input.Dispose();
            throw;
        }
    }

    private static T? Read<T>(Stream input,int limit)
    {
        if(input.Length>limit)throw new InvalidDataException("記録のサイズが上限を超えています。");
        return JsonSerializer.Deserialize<T>(input,new JsonSerializerOptions {RespectRequiredConstructorParameters=true,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,AllowDuplicateProperties=false});
    }
    private static void Write<T>(string path,T value)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions {WriteIndented=true});if(bytes.Length>MaxHistoryBytes)throw new InvalidDataException("監査記録のサイズが上限を超えています。");
        var parent=Path.GetDirectoryName(path)!;Directory.CreateDirectory(parent);
        if(File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))throw new InvalidDataException("リンクになっている記録は上書きしません。");
        var temp=path+".tmp-"+Guid.NewGuid().ToString("N");
        try {using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){output.Write(bytes);output.Flush(true);}File.Move(temp,path,true);}
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
}
