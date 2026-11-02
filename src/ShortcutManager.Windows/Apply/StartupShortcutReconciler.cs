using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.Windows.Apply;

public sealed record StartupReconcileResult(bool Repaired,string Message,int Differences,string? BackupPath=null)
{
    public bool LinksMatch {get;init;}
    public bool SourceChanged {get;init;}
    public IReadOnlyList<AuditDifference> Before {get;init;}=[];
    public IReadOnlyList<AuditDifference> After {get;init;}=[];
}

public sealed class StartupShortcutReconciler(ManagedShortcutAuditor auditor,ManagedShortcutApplier applier)
{
    public async Task<StartupReconcileResult> ReconcileAsync(ShortcutConfig config,string directory,Func<Task<IReadOnlyList<ShortcutInventoryItem>>> scan,bool auditOnly=false,bool explicitRepair=false,AuditControlStore? controls=null,Func<bool>? scanComplete=null,Func<bool>? sourceUnchanged=null)
    {
        var mode=explicitRepair?"明示修復":auditOnly?"監査のみ":"自動修復";
        StartupReconcileResult Finish(StartupReconcileResult result,string outcome)
        {
            try {controls?.Record(new(1,DateTimeOffset.Now,mode,outcome,result.Before,result.After,result.BackupPath,result.Message));}
            catch(Exception error) when(error is not OutOfMemoryException) {return result with {Message=result.Message+" 監査履歴を保存できません: "+error.Message};}
            return result;
        }
        ShortcutAuditResult? audit=null;
        try
        {
            audit=await Task.Run(()=>auditor.Audit(config,directory));
            var inventory=await scan();
            if(sourceUnchanged?.Invoke()==false)return Finish(new(false,"監査中に保存設定が外部で変わったため、修復せず候補を保持しました。再読込前に候補を保全してください。",audit.Differences.Count){Before=audit.Details,After=audit.Details,SourceChanged=true},"保存設定の変更で保留");
            var external=inventory.Where(item=>!item.IsManaged && !string.IsNullOrWhiteSpace(item.Hotkey)).Select(item=>item.Hotkey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var conflicts=config.Shortcuts.Where(item=>item.Enabled && external.Contains(item.Hotkey.Canonical)).ToArray();
            if(conflicts.Length>0)return Finish(new(false,"起動時監査: 管理外のHotKey競合があります。修復を中止しました: "+string.Join(" / ",conflicts.Select(item=>item.Hotkey.Canonical)),audit.Differences.Count){Before=audit.Details,After=audit.Details,LinksMatch=audit.Matches},"競合で保留");
            if(audit.Matches)return Finish(new(false,"起動時監査: 保存済み設定とショートカットは一致しています。"+(scanComplete?.Invoke()==false?" 外部キーは一部未走査です。":""),0){LinksMatch=true},"差分なし");
            if(auditOnly && !explicitRepair)return Finish(new(false,$"監査のみ: リンク差分 {audit.Differences.Count} 件。今回の修復を保留しました。保存済み設定へ逆取り込みせず、詳細を確認して明示修復できます。",audit.Differences.Count){Before=audit.Details,After=audit.Details},"監査のみ・保留");
            if(!explicitRepair && scanComplete?.Invoke()==false)return Finish(new(false,"外部キーの確認に未走査・読取り失敗があるため自動修復を保留しました。詳細を確認してください。",audit.Differences.Count){Before=audit.Details,After=audit.Details},"走査不完全で保留");
            var fingerprint=AuditControlStore.Fingerprint(ConfigJson.Serialize(config),audit.Details);
            if(!explicitRepair && controls?.WasAttempted(fingerprint)==true)return Finish(new(false,"同じ設定・同じ差分で修復を試行済みのため、自動再試行を保留しました。履歴を確認して明示修復してください。",audit.Differences.Count){Before=audit.Details,After=audit.Details},"同じ修復の再試行を保留");
            // Persist intent first: interruption or an unrecordable result must not
            // create an automatic repair/backup loop on the next launch.
            if(controls is not null)
            {
                try {controls.MarkAttempt(fingerprint);}
                catch(Exception error) when(error is not OutOfMemoryException) {if(!explicitRepair)return Finish(new(false,"修復試行を記録できないため自動修復を保留しました: "+error.Message,audit.Differences.Count){Before=audit.Details,After=audit.Details},"試行記録不能で保留");}
            }
            var result=await Task.Run(()=>applier.Apply(config,directory,external.ToArray()));
            var after=audit;
            if(result.Succeeded)
            {
                try {after=await Task.Run(()=>auditor.Audit(config,directory));}
                catch(Exception error) when(error is not OutOfMemoryException) {return Finish(new(true,"修復のトランザクションは完了しましたが、独立した修復後監査は確認できません: "+error.Message,audit.Differences.Count,result.BackupPath){Before=audit.Details,After=[new(directory,"修復後監査","確認不能","再監査が必要")],LinksMatch=false},"修復済み・再監査が必要");}
            }
            var message=result.Succeeded ? after.Matches ? $"起動時監査: {audit.Differences.Count} 件の不一致をバックアップ後に修復しました。Explorer反映と実キー動作は別に確認してください。" : "修復のトランザクションは完了しましたが、修復後のリンクに未確認の差分が残っています。履歴と再監査で確認してください。" : "修復できませんでした。"+result.Message;
            return Finish(new(result.Succeeded,message,audit.Differences.Count,result.BackupPath){Before=audit.Details,After=after.Details,LinksMatch=after.Matches},result.Succeeded?(after.Matches?"修復済み":"修復済み・再監査が必要"):"修復失敗");
        }
        catch(Exception error) when(error is not OutOfMemoryException)
        {
            return Finish(new(false,"監査・修復を中止しました。外付け先の接続や履歴を確認してください。"+error.Message,audit?.Differences.Count ?? -1){Before=audit?.Details ?? [],After=audit?.Details ?? []},"確認不能");
        }
    }
}
