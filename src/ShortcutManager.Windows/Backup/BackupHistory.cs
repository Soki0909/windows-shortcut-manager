namespace ShortcutManager.Windows.Backup;

public sealed record BackupHistoryEntry(string Path,BackupManifest? Manifest,int? Count,long Bytes,bool VerifiedAutomatic,string? Error)
{
    public string Timestamp => Manifest?.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "不明";
    public string Reason => Manifest?.Reason switch {"apply"=>"変更反映前","restore"=>"復元前","manual"=>"手動保存","recovery-initialize"=>"復旧用初期化前",null=>"読取り不可",var value=>value};
    public string Version => Manifest?.AppVersion ?? "不明";
    public string Origin => VerifiedAutomatic ? "検証済み自動" : Manifest?.Reason=="manual" ? "手動（記録上）" : "由来未確認";
}

public static class BackupHistory
{
    public static IReadOnlyList<BackupHistoryEntry> Read(string directory)
    {
        if(!Directory.Exists(directory))return [];
        var result=new List<BackupHistoryEntry>();
        try
        {
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("履歴フォルダがリンクのため自動走査しません。");
            foreach(var path in Directory.EnumerateFiles(directory,"*.zip",SearchOption.TopDirectoryOnly))
            {
                long bytes=0;BackupManifest? manifest=null;
                try
                {
                    var file=new FileInfo(path);bytes=file.Length;
                    if(file.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("リンクファイルは履歴の自動読取り対象外です。");
                    var archive=BackupArchive.Read(path);manifest=archive.Manifest;var config=RestoreService.ParseConfig(archive.Config);
                    var automatic=archive.Manifest.Reason is "apply" or "restore" && BackupService.HasAutomaticReceipt(path);
                    result.Add(new(path,archive.Manifest,config.Shortcuts.Count,bytes,automatic,null));
                }
                catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) {result.Add(new(path,manifest,null,bytes,false,error.Message));}
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) {result.Add(new(directory,null,null,0,false,error.Message));}
        return result.OrderByDescending(item=>item.Manifest?.CreatedAt).ThenBy(item=>item.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
