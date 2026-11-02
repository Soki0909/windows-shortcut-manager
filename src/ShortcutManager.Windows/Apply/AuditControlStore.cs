using System.Security.Cryptography;
using System.Text.Json;

namespace ShortcutManager.Windows.Apply;

public sealed record AuditPolicy(int FormatVersion,bool AuditOnly);
public sealed record AuditHistoryRecord(int FormatVersion,DateTimeOffset At,string Mode,string Outcome,IReadOnlyList<AuditDifference> Before,IReadOnlyList<AuditDifference> After,string? BackupPath,string Message);
public sealed record AuditHistoryItem(string Path,AuditHistoryRecord? Record,string? Error);

public sealed class AuditControlStore(string directory)
{
    private const int MaxHistoryBytes=4*1024*1024;
    private string PolicyPath=>Path.Combine(directory,"audit-settings.json");
    private string AttemptPath=>Path.Combine(directory,"repair-attempt.json");
    private string HistoryDirectory=>Path.Combine(directory,"audit-history");
    public (bool AuditOnly,string? Warning) ReadPolicy()
    {
        try {var policy=Read<AuditPolicy>(PolicyPath,4096);if(policy is null || policy.FormatVersion!=1)throw new InvalidDataException("対応しない監査設定です。");return(policy.AuditOnly,null);}
        catch(Exception error) when(error is FileNotFoundException or DirectoryNotFoundException) {return(false,null);}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) {return(true,"監査設定を読めないため自動修復を保留します: "+error.Message);}
    }
    public void SavePolicy(bool auditOnly)=>Write(PolicyPath,new AuditPolicy(1,auditOnly));
    public bool WasAttempted(string fingerprint)
    {
        try {var attempt=Read<Attempt>(AttemptPath,4096);return attempt is null || attempt.FormatVersion!=1 || attempt.Fingerprint is null || attempt.Fingerprint.Length!=64 || attempt.Fingerprint.Any(character=>!Uri.IsHexDigit(character)) || attempt.Fingerprint==fingerprint;}
        catch(Exception error) when(error is FileNotFoundException or DirectoryNotFoundException) {return false;}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) {return true;}
    }
    public void MarkAttempt(string fingerprint)=>Write(AttemptPath,new Attempt(1,fingerprint,DateTimeOffset.Now));
    public void Record(AuditHistoryRecord record)=>Write(Path.Combine(HistoryDirectory,$"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json"),record);
    public IReadOnlyList<AuditHistoryItem> ReadHistory()
    {
        if(!Directory.Exists(HistoryDirectory))return [];
        var items=new List<AuditHistoryItem>();
        try {
            foreach(var path in Directory.EnumerateFiles(HistoryDirectory,"*.json").OrderDescending(StringComparer.Ordinal).Take(50)) {
                try {var record=Read<AuditHistoryRecord>(path,MaxHistoryBytes);if(record is null || record.FormatVersion!=1 || record.Before is null || record.After is null || record.Before.Count>256 || record.After.Count>256 || record.Before.Concat(record.After).Any(item=>item is null || item.File is null || item.Field is null || item.Actual is null || item.Expected is null))throw new InvalidDataException("対応しない履歴です。");items.Add(new(path,record,null));}
                catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) {items.Add(new(path,null,error.Message));}
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) {items.Add(new(HistoryDirectory,null,error.Message));}
        return items;
    }
    public static string Fingerprint(string config,IReadOnlyList<AuditDifference> differences)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config+JsonSerializer.Serialize(differences.OrderBy(item=>item.File,StringComparer.OrdinalIgnoreCase).ThenBy(item=>item.Field,StringComparer.Ordinal)))));
    private sealed record Attempt(int FormatVersion,string Fingerprint,DateTimeOffset At);
    private static T? Read<T>(string path,int limit)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var file=new FileInfo(path);if(file.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new InvalidDataException("リンクになっている記録は読みません。");
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
