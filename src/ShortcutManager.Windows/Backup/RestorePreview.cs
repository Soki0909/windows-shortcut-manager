using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.Windows.Backup;

public enum RestoreMode { Entire, DisableUnavailable }
public sealed record RestoreEntryChange(string Change,string Before,string After);
public sealed record RestorePreview(string ArchiveSha256,BackupManifest Manifest,ShortcutConfig Config,IReadOnlyList<RestoreEntryChange> Changes,IReadOnlyList<ValidationIssue> Issues,IReadOnlyList<Guid> DisabledEntries,IReadOnlyList<string> ExternalConflicts,bool SettingsChanged,string? OriginalManagedDirectory,string? TargetManagedDirectory,IReadOnlyList<string> DisabledReasons)
{
    public bool CanRestore => Issues.Count==0 && ExternalConflicts.Count==0;
    public string Description => $"バックアップ: {Manifest.CreatedAt:yyyy-MM-dd HH:mm:ss zzz} / {Manifest.AppVersion} / {Manifest.Reason}\n復元対象: 有効 {Config.Shortcuts.Count(item=>item.Enabled)} 件 / 全 {Config.Shortcuts.Count} 件\n変更 {Changes.Count} 件、設定変更 {(SettingsChanged?"あり":"なし")}、明示モードで無効化 {DisabledEntries.Count} 件\n元の管理先: {OriginalManagedDirectory ?? "元環境の既定フォルダ"}\n今回の管理先: {TargetManagedDirectory ?? "プレビューで未指定"}\n元環境の管理先へは書き込まず、今回の管理先を保存後も使います。\n"+BackupScope.Description+"\n\n"+string.Join("\n\n",Changes.Select(change=>$"{change.Change}\n変更前: {change.Before}\n変更後: {change.After}"))+"\n\n"+string.Join("\n",Issues.Select(issue=>issue.Message+(issue.Path is null?"":" / "+issue.Path)))+"\n"+string.Join("\n",ExternalConflicts.Select(key=>"外部キー競合: "+key))+"\n\nURIアプリの登録、Chromeのプロファイル・拡張・グループは実際の起動で別に確認してください。";
}
public sealed record PreparedRestore(string BackupPath,string CurrentConfigSha256,RestoreMode Mode,RestorePreview Preview);

public static class RestoreDifference
{
    public static IReadOnlyList<RestoreEntryChange> Compare(ShortcutConfig? before,ShortcutConfig after)
    {
        if(before is null)return [new("現在の設定を読めないため比較不可","原本を復元前バックアップへ保全します。",$"復元する割当 {after.Shortcuts.Count} 件")];
        var changes=new List<RestoreEntryChange>();
        foreach(var old in before.Shortcuts)
        {
            var current=after.Shortcuts.FirstOrDefault(item=>item.Id==old.Id);
            if(current is null)changes.Add(new("削除",Describe(old),"なし"));
            else if(JsonSerializer.Serialize(old,ConfigJson.Options)!=JsonSerializer.Serialize(current,ConfigJson.Options))changes.Add(new("変更",Describe(old),Describe(current)));
        }
        foreach(var current in after.Shortcuts.Where(item=>before.Shortcuts.All(old=>old.Id!=item.Id)))changes.Add(new("追加","なし",Describe(current)));
        if(JsonSerializer.Serialize(before.Settings,ConfigJson.Options)!=JsonSerializer.Serialize(after.Settings,ConfigJson.Options))changes.Add(new("設定",DescribeSettings(before.Settings),DescribeSettings(after.Settings)));
        return changes;
    }
    private static string DescribeSettings(ShortcutSettings settings)=>$"Chrome実行ファイル: {settings.Chrome.ExecutablePath ?? "自動検索"}\n拡張機能ID: {settings.Chrome.ExtensionId}\n管理先: {settings.ManagedShortcutDirectory ?? "この環境の既定フォルダ"}";
    public static string Describe(ShortcutDefinition entry)=>$"{entry.Hotkey.Canonical} / {entry.Name} / {(entry.Enabled?"有効":"無効")} / "+(entry.Target switch {
        WebsiteTarget site=>"Web: "+site.Url,
        ApplicationTarget app=>$"アプリ ({(app.LaunchMode==ApplicationLaunchMode.Executable?"直接起動":app.LaunchMode==ApplicationLaunchMode.Shortcut?"既存の.lnk":"URI")}): {app.Value}\n追加引数: {app.Arguments} / 作業フォルダ: {app.WorkingDirectory}",
        BatTarget bat=>$"BAT: {bat.Path}\n追加引数: {bat.Arguments} / 作業フォルダ: {bat.WorkingDirectory} / 表示: {(bat.WindowMode==BatWindowMode.Minimized?"最小化":"通常")}",
        ChromeTabGroupTarget group=>"Chromeグループ: "+group.GroupName,
        _=>"不明な対象"});
}
