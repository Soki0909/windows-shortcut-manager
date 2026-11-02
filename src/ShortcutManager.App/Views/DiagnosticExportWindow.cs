using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.App;

public sealed class DiagnosticExportWindow : Window
{
    public DiagnosticExportWindow(DiagnosticReport report)
    {
        Title = "報告用の診断を確認・保存"; Width = 790; Height = 700; MinWidth = 460; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; DialogLayout.Constrain(this);
        var panel = new StackPanel { Margin = new(18) };
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = "この画面を開いた時点の版・状態・件数・検証コードです。名前・パス・URL・ID・ログ原文は既定で含めません。監査や走査を新たに実行せず、実キーの動作も推定しません。保存後の送信は自分で行います。", TextWrapping = TextWrapping.Wrap });
        var includeNotes = new CheckBox { Content = new TextBlock { Text = "自分で入力した補足を追加する（自動でマスクされません）", TextWrapping = TextWrapping.Wrap }, Margin = new(0, 10, 0, 5) };
        var notes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 70,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = DiagnosticReport.MaxNotesLength, IsEnabled = false };
        AutomationProperties.SetName(notes, "任意の補足。名前・URL・パスを入れる場合は自分で確認してください");
        header.Children.Add(includeNotes); header.Children.Add(notes); panel.Children.Add(header);
        var footer = new StackPanel { Margin = new(0, 8, 0, 0) };
        var reviewed = new CheckBox { Content = new TextBlock { Text = "診断の全文と補足を確認し、共有してよい内容であることを確認しました", TextWrapping = TextWrapping.Wrap }, Margin = new(0, 4, 0, 6) };
        var status = new TextBlock { Text = "保存してもIssueへアップロードしません。既存ファイルは上書きしません。", TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var close = new Button { Content = "閉じる", IsCancel = true }; close.Click += (_, _) => Close();
        var save = new Button { Content = "確認した診断を保存…", IsEnabled = false };
        buttons.Children.Add(close); buttons.Children.Add(save); footer.Children.Add(reviewed); footer.Children.Add(status); footer.Children.Add(buttons);
        var preview = new TextBox { Text = report.Preview(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 300, Margin = new(0, 10, 0, 0) };
        AutomationProperties.SetName(preview, "保存される診断の全文"); panel.Children.Add(preview); panel.Children.Add(footer);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Refresh() { notes.IsEnabled = includeNotes.IsChecked == true; preview.Text = report.Preview(includeNotes.IsChecked == true ? notes.Text : null); reviewed.IsChecked = false; save.IsEnabled = false; }
        includeNotes.Checked += (_, _) => Refresh(); includeNotes.Unchecked += (_, _) => Refresh(); notes.TextChanged += (_, _) => Refresh();
        reviewed.Checked += (_, _) => save.IsEnabled = true; reviewed.Unchecked += (_, _) => save.IsEnabled = false;
        save.Click += (_, _) =>
        {
            if (reviewed.IsChecked != true) return;
            var dialog = new SaveFileDialog { Title = "診断を新しいファイルに保存", FileName = "ShortcutManager-diagnostic.json",
                DefaultExt = ".json", Filter = "診断JSON|*.json", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try { DiagnosticFile.SaveNew(dialog.FileName, preview.Text); status.Text = "確認した内容を保存しました。自動送信はしていません。"; }
            catch (Exception error) { status.Text = "保存できませんでした。別の名前や保存先を選んでください: " + error.Message; }
        };
    }
}
