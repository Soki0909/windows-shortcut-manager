using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ShortcutManager.Windows.Removal;
using ShortcutManager.Windows.Storage;

namespace ShortcutManager.App;

public sealed class RemovalPreparationWindow : Window
{
    private readonly ApplicationPaths paths;
    private readonly ApplicationPaths otherEdition;
    private readonly MainViewModel? viewModel;
    private readonly Func<Task>? offerShellReload;
    private readonly LocalDataRemovalStaging staging;
    private readonly LocalDataRemovalPreview previewService;
    private readonly LocalDataRemovalArchive archiveService;
    private readonly LocalDataRemovalRestore restoreService;

    private readonly StackPanel body = new();
    private readonly TextBlock statusBanner = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private readonly Button closeButton = new() { Margin = new(4), Padding = new(16, 6, 16, 6), IsCancel = true };
    private readonly List<CheckBox> individualCheckBoxes = [];
    private LocalDataRemovalPlan? currentPlan;
    private RemovalArchiveRestorePreview? currentRestorePreview;
    internal bool RequiresRecovery
    {
        get
        {
            try { return staging.InspectPending() is not null; }
            catch (Exception) { return true; }
        }
    }

    public RemovalPreparationWindow(ApplicationPaths paths, ApplicationPaths otherEdition, MainViewModel? viewModel = null, Func<Task>? offerShellReload = null)
    {
        this.paths = paths;
        this.otherEdition = otherEdition;
        this.viewModel = viewModel;
        this.offerShellReload = offerShellReload;
        staging = new LocalDataRemovalStaging(paths, otherEdition, holdsInstanceLease: true);
        previewService = new LocalDataRemovalPreview(paths, otherEdition);
        archiveService = new LocalDataRemovalArchive(paths, otherEdition);
        restoreService = new LocalDataRemovalRestore(paths, otherEdition, holdsInstanceLease: true);

        Title = "削除の準備・ローカルデータの削除と復元";
        Width = 860;
        Height = 680;
        MinWidth = 540;
        MinHeight = 420;
        WindowStartupLocation = viewModel is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        DialogLayout.Constrain(this);

        var root = new DockPanel { Margin = new(18) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 12, 0, 0) };
        var refresh = new Button { Content = "状態を更新(_R)", Margin = new(4), Padding = new(12, 6, 12, 6) };
        refresh.Click += (_, _) => RebuildView();
        closeButton.Click += (_, _) => Close();
        footer.Children.Add(refresh);
        footer.Children.Add(closeButton);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var container = new StackPanel();
        container.Children.Add(statusBanner);
        container.Children.Add(body);
        scroll.Content = container;
        root.Children.Add(scroll);
        Content = root;

        RebuildView();
    }

    internal void ShowForStartup()
    {
        var application = Application.Current;
        var previousMode = application.ShutdownMode;
        // Before the main window exists, closing this dialog must not schedule shutdown.
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try { ShowDialog(); }
        finally { application.ShutdownMode = previousMode; }
    }

    private void RebuildView(string? notice = null)
    {
        body.Children.Clear();
        individualCheckBoxes.Clear();
        currentPlan = null;
        closeButton.Content = "閉じる";
        statusBanner.Text = notice ?? "公式アンインストーラーは本体のみを削除し、設定・バックアップ・管理ショートカットキーを保持します。ここでは管理キーの解除、確認済みローカルデータの退避・消去、および削除前アーカイブからの復元を行えます。";

        PendingRemovalSummary? pending;
        try { pending = staging.InspectPending(); }
        catch (Exception error)
        {
            closeButton.Content = "閉じて終了";
            body.Children.Add(MakeSection("未完了の削除記録に異常があります",
                $"復旧に必要なファイルを保護するため、新しい削除や書込みを停止しています。\n{error.Message}"));
            return;
        }

        if (pending is not null)
        {
            closeButton.Content = "閉じて終了";
            BuildPendingDecisionView(pending);
            return;
        }

        BuildNormalView();
    }

    private void BuildPendingDecisionView(PendingRemovalSummary pending)
    {
        var summary = $"退避済みのローカルデータ（{pending.RelativePaths.Count} 件）が確認待ちです。\n"
            + $"削除前アーカイブ: {pending.ArchivePath}\n"
            + $"アーカイブ SHA-256: {pending.ArchiveSha256}\n"
            + $"作業記録フォルダ: {pending.OperationDirectory}\n\n"
            + "・取り消すと、退避済みファイルを元の場所へ戻します。\n"
            + "・確定消去すると、アーカイブと退避内容を再検証した上で退避済みファイルを削除します。消去後は上記の削除前アーカイブから復元できます。\n"
            + "・閉じて終了すると、次回起動時にこの確認画面から再開できます。";

        var section = MakeSection("退避完了 — 取り消しまたは確定消去を選択してください", summary);
        var list = new TextBox
        {
            Text = string.Join(Environment.NewLine, pending.RelativePaths),
            IsReadOnly = true,
            MinHeight = 80,
            MaxHeight = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new(0, 6, 0, 10)
        };
        section.Children.Add(list);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var cancelButton = new Button { Content = "退避を取り消して元に戻す(_U)", Margin = new(0, 0, 8, 0), Padding = new(14, 6, 14, 6) };
        cancelButton.Click += (_, _) => ExecuteCancel();
        var commitButton = new Button { Content = "確定消去する(_D)", Margin = new(0, 0, 8, 0), Padding = new(14, 6, 14, 6), Style = (Style)FindResource("PrimaryButtonStyle") };
        commitButton.Click += (_, _) => ExecuteCommit(pending);
        actions.Children.Add(cancelButton);
        actions.Children.Add(commitButton);
        section.Children.Add(actions);
        body.Children.Add(section);
    }

    private void BuildNormalView()
    {
        var hasUnsaved = viewModel?.HasUnsavedChanges ?? false;
        var plan = previewService.Inspect(hasUnsaved);
        currentPlan = plan;

        var info = $"エディション: {(paths.IsDevelopment ? "デバッグ版" : "製品版")}\n"
            + $"データ保存先: {plan.DataDirectory}\n"
            + $"管理フォルダ: {(plan.ManagedDirectories.Count > 0 ? string.Join(" / ", plan.ManagedDirectories) : paths.ManagedShortcutDirectory)}\n"
            + $"保存済みの割当件数: {(plan.SavedAssignments.HasValue ? $"{plan.SavedAssignments.Value} 件" : "なし / 未作成")}\n"
            + $"未保存の変更: {(hasUnsaved ? "あり（先に反映または破棄してください）" : "なし")}";
        body.Children.Add(MakeSection("現在の状態", info));

        if (viewModel is not null)
        {
            var enabledCount = viewModel.Shortcuts.Count(row => row.Enabled);
            var disableSection = MakeSection("1. 管理ショートカットキーを解除（任意）",
                $"現在有効な割当: {enabledCount} 件。解除前に別の場所へバックアップZIPを保存・検証してから、全割当を無効にして反映します。割当の定義は設定に残ります。");
            var disableButton = new Button
            {
                Content = "解除前バックアップを保存して全キーを解除…",
                IsEnabled = enabledCount > 0 && !hasUnsaved && !viewModel.RecoveryRequired && !viewModel.IsBusy,
                Padding = new(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            disableButton.Click += async (_, _) => await ExecuteDisableAllAsync();
            disableSection.Children.Add(disableButton);
            body.Children.Add(disableSection);
        }

        var removalSection = MakeSection("2. 確認済みのローカルデータを削除（任意）",
            "管理リンクが残っていないことを確認した上で、対象ファイルを削除前アーカイブへ保存・検証し、同じボリュームの退避領域へ移します。管理フォルダと所有マーカー、管理外ファイル、外部BAT、Chrome拡張は保持します。");

        if (plan.Blockers.Count > 0)
        {
            var blockerText = ThemeResources.ErrorText();
            blockerText.Text = "現在はデータ削除を開始できません:\n・" + string.Join("\n・", plan.Blockers);
            blockerText.Margin = new(0, 4, 0, 8);
            removalSection.Children.Add(blockerText);
        }

        var candidates = plan.Items.Where(item => item.Disposition == RemovalDisposition.Candidate).ToArray();
        var individual = plan.Items.Where(item => item.Disposition == RemovalDisposition.RequiresIndividualSelection).ToArray();
        var kept = plan.Items.Where(item => item.Disposition == RemovalDisposition.Keep).ToArray();

        removalSection.Children.Add(new TextBlock { Text = $"削除候補（{candidates.Length} 件）", FontWeight = FontWeights.SemiBold, Margin = new(0, 6, 0, 2) });
        removalSection.Children.Add(new TextBox
        {
            Text = candidates.Length == 0
                ? "対象ファイルはありません。"
                : string.Join(Environment.NewLine, candidates.Select(item => $"{item.Kind} | {item.Path} ({item.Bytes ?? 0} B) | {item.Reason}")),
            IsReadOnly = true,
            MinHeight = 60,
            MaxHeight = 140,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new(0, 0, 0, 8)
        });

        if (individual.Length > 0)
        {
            removalSection.Children.Add(new TextBlock { Text = $"個別に選択できるバックアップZIP（{individual.Length} 件・既定は保持）", FontWeight = FontWeights.SemiBold, Margin = new(0, 4, 0, 2) });
            foreach (var item in individual)
            {
                var check = new CheckBox
                {
                    Content = $"{Path.GetFileName(item.Path)} ({item.Bytes ?? 0} B) — {item.Reason}",
                    Tag = item.Path,
                    Margin = new(0, 2, 0, 2)
                };
                individualCheckBoxes.Add(check);
                removalSection.Children.Add(check);
            }
        }

        if (kept.Length > 0)
        {
            removalSection.Children.Add(new TextBlock { Text = $"削除せず保持する項目（{kept.Length} 件）", FontWeight = FontWeights.SemiBold, Margin = new(0, 6, 0, 2) });
            removalSection.Children.Add(new TextBox
            {
                Text = string.Join(Environment.NewLine, kept.Select(item => $"{item.Kind} | {item.Path} | 理由: {item.Reason}")),
                IsReadOnly = true,
                MinHeight = 48,
                MaxHeight = 110,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new(0, 0, 0, 8)
            });
        }

        var stageButton = new Button
        {
            Content = "削除前アーカイブを作成して退避する…",
            IsEnabled = plan.CanReviewCandidates,
            Padding = new(14, 6, 14, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        stageButton.Click += (_, _) => ExecuteStage();
        removalSection.Children.Add(stageButton);
        body.Children.Add(removalSection);

        var restoreSection = MakeSection("3. 削除前アーカイブから復元",
            "消去後に再導入した場合など、別の場所へ保存した削除前アーカイブ（ZIP）を検証して現在のエディションの保存先へ復元します。既存ファイルがある場合は上書きせず停止します。");
        var restoreButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var inspectArchiveButton = new Button { Content = "削除前アーカイブを選択してプレビュー…", Padding = new(14, 6, 14, 6), Margin = new(0, 0, 8, 0) };
        var executeRestoreButton = new Button { Content = "プレビュー内容で復元を実行", Padding = new(14, 6, 14, 6), IsEnabled = false };
        var restorePreviewBox = new TextBox
        {
            IsReadOnly = true,
            MinHeight = 60,
            MaxHeight = 140,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };

        inspectArchiveButton.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog { Filter = "削除前アーカイブ (*.zip)|*.zip", Title = "削除前アーカイブを選択" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                currentRestorePreview = restoreService.Inspect(dialog.FileName);
                var lines = new List<string>
                {
                    $"アーカイブ: {currentRestorePreview.ArchivePath}",
                    $"エディション: {currentRestorePreview.Edition} / 作成日時: {currentRestorePreview.CreatedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}",
                    $"復元先: {currentRestorePreview.DataDirectory}"
                };
                if (currentRestorePreview.Blockers.Count > 0)
                    lines.Add("ブロッカー: " + string.Join(" / ", currentRestorePreview.Blockers));
                if (currentRestorePreview.Conflicts.Count > 0)
                    lines.Add("既存ファイルと競合（上書き不可）: " + string.Join(", ", currentRestorePreview.Conflicts));
                lines.Add("--- 項目 ---");
                foreach (var item in currentRestorePreview.Items)
                    lines.Add($"{(item.WillRestore ? "[復元]" : "[除外]")} {item.RelativePath} ({item.Bytes} B) — {item.Note}");
                restorePreviewBox.Text = string.Join(Environment.NewLine, lines);
                restorePreviewBox.Visibility = Visibility.Visible;
                executeRestoreButton.IsEnabled = currentRestorePreview.CanRestore;
            }
            catch (Exception error)
            {
                currentRestorePreview = null;
                executeRestoreButton.IsEnabled = false;
                MessageBox.Show(this, "アーカイブを検証できませんでした:\n" + error.Message, "削除前アーカイブの検証", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        executeRestoreButton.Click += (_, _) =>
        {
            if (currentRestorePreview is null) return;
            try
            {
                var result = restoreService.Restore(currentRestorePreview);
                currentRestorePreview = null;
                executeRestoreButton.IsEnabled = false;
                viewModel?.Load();
                var message = $"削除前アーカイブから {result.RestoredCount} 件のファイルを復元しました。"
                    + (result.SkippedStateCount > 0 ? "\n反映状態（state.json）は旧パス保護のため除外しました。必要に応じてメイン画面で「変更を反映」または既存バックアップの復元を行ってください。" : string.Empty);
                MessageBox.Show(this, message, "復元完了", MessageBoxButton.OK, MessageBoxImage.Information);
                RebuildView(message);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "復元に失敗しました（新規作成分はロールバックしました）:\n" + error.Message, "復元エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        restoreButtons.Children.Add(inspectArchiveButton);
        restoreButtons.Children.Add(executeRestoreButton);
        restoreSection.Children.Add(restoreButtons);
        restoreSection.Children.Add(restorePreviewBox);
        body.Children.Add(restoreSection);
    }

    private async Task ExecuteDisableAllAsync()
    {
        if (viewModel is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "バックアップZIP (*.zip)|*.zip",
            FileName = $"shortcut-manager-before-disable-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            Title = "解除前バックアップZIPの保存先を選択（データ保存先・管理フォルダ外）"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var applied = await viewModel.DisableAllForRemovalAsync(dialog.FileName, otherEdition);
            if (!applied)
            {
                MessageBox.Show(this, "管理ショートカットキーの解除を完了できませんでした。メイン画面のステータスと競合を確認してください。", "解除できませんでした", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (offerShellReload is not null) await offerShellReload();
            RebuildView($"管理ショートカットキーを解除しました（解除前バックアップ: {dialog.FileName}）。続けてデータ削除を行う場合は下の対象一覧を確認してください。");
        }
        catch (Exception error)
        {
            MessageBox.Show(this, "解除に失敗しました:\n" + error.Message, "管理キー解除エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteStage()
    {
        if (currentPlan is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "削除前アーカイブ (*.zip)|*.zip",
            FileName = $"shortcut-manager-before-removal-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            Title = "削除前アーカイブの保存先を選択（データ保存先・管理フォルダ外）"
        };
        if (dialog.ShowDialog(this) != true) return;
        var selectedIndividual = individualCheckBoxes.Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToArray();
        try
        {
            var hasUnsaved = viewModel?.HasUnsavedChanges ?? false;
            var archive = archiveService.Create(currentPlan, selectedIndividual, dialog.FileName, hasUnsaved);
            var stagedCount = staging.Stage(currentPlan, selectedIndividual, archive, hasUnsaved);
            RebuildView($"{stagedCount} 件のファイルを削除前アーカイブへ検証保存し、退避しました。下のボタンから「取り消し」または「確定消去」を選んでください。");
        }
        catch (Exception error)
        {
            MessageBox.Show(this, "削除前アーカイブの作成または退避を停止しました。下の画面で、未完了の処理があるか確認してください。\n" + error.Message, "退避エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            RebuildView();
        }
    }

    private void ExecuteCancel()
    {
        try
        {
            var outcome = staging.Cancel();
            viewModel?.Load();
            var message = outcome == RemovalRecoveryOutcome.Restored
                ? "退避を取り消し、すべてのファイルを元の場所へ戻しました。"
                : "取り消し対象の退避データはありませんでした。";
            MessageBox.Show(this, message, "退避の取り消し", MessageBoxButton.OK, MessageBoxImage.Information);
            RebuildView(message);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, "取り消しを停止しました（復旧用ファイルは保持しています）:\n" + error.Message, "取り消しエラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteCommit(PendingRemovalSummary pending)
    {
        var confirm = MessageBox.Show(this,
            $"退避済みの {pending.RelativePaths.Count} 件のローカルデータを確定消去します。\n\n"
            + $"削除前アーカイブ:\n{pending.ArchivePath}\n\n"
            + "管理フォルダの所有マーカー、管理外ショートカット、外部BAT、Chrome拡張は削除されません。\n"
            + "消去後に復元したい場合は、上記の削除前アーカイブから復元できます。\n\n確定消去を実行しますか？",
            "確定消去の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var result = staging.Commit();
            viewModel?.Load();
            var message = $"{result.DeletedCount} 件のローカルデータを確定消去しました。\n\n"
                + $"削除前アーカイブ: {result.ArchivePath}\n"
                + $"処理記録フォルダ: {result.OperationDirectory}\n\n"
                + "本体の削除はWindowsの「設定」→「アプリ」から公式アンインストーラーを実行してください。\n"
                + "再導入後に戻す場合は、この「削除の準備」画面から上記の削除前アーカイブを選択して復元できます。";
            MessageBox.Show(this, message, "確定消去が完了しました", MessageBoxButton.OK, MessageBoxImage.Information);
            RebuildView(message);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, "確定消去を停止しました（アーカイブまたは退避内容を再確認してください）:\n" + error.Message, "確定消去エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static StackPanel MakeSection(string title, string description)
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 4) });
        panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 6) });
        return panel;
    }
}
