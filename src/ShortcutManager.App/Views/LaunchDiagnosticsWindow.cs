using System.Text;
using System.Windows;
using System.Windows.Controls;
using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.App;

public sealed class LaunchDiagnosticsWindow : Window
{
    public LaunchDiagnosticsWindow()
    {
        Title = "HotKeyの起動遅延を診断"; Width = 760; Height = 540; MinWidth = 620; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new DockPanel { Margin = new(20) };
        var explanation = new TextBlock { Text = "HotKeyだけが遅い場合は、Windows Shellが他アプリの応答を待っている可能性があります。\n応答のないウィンドウの候補を読み取り専用で調べます。原因の断定や自動終了は行いません。", TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
        DockPanel.SetDock(explanation, Dock.Top); panel.Children.Add(explanation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var run = new Button { Content = "診断する", Padding = new(16, 8, 16, 8), Margin = new(4) };
        var close = new Button { Content = "閉じる", Padding = new(16, 8, 16, 8), Margin = new(4), IsCancel = true };
        buttons.Children.Add(run); buttons.Children.Add(close); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var report = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = "「診断する」を押してください。ウィンドウのタイトルや閲覧URLは収集しません。\n\n同じショートカットのダブルクリックは速く、実キーだけが数秒遅い場合は、対象アプリを起動する前のShell待機を疑います。\n\n候補が見つかったら、作業を保存して該当アプリを通常終了し、実キーと再診断を比較してください。システムプロセスの強制終了やドライバー・セキュリティ設定の変更はしないでください。" };
        panel.Children.Add(report); Content = panel;
        var cancellation = new CancellationTokenSource(); Closed += (_, _) => cancellation.Cancel();
        close.Click += (_, _) => Close();
        run.Click += async (_, _) => {
            run.IsEnabled = false; report.Text = "応答を調べています…";
            try {
                var result = await Task.Run(() => new ShellResponsivenessProbe().Run(cancellation.Token));
                if (!IsLoaded) return;
                var text = new StringBuilder($"調査 {result.Examined} 窓 / 応答 {result.Responded} 窓 / 自身・消滅など除外 {result.Skipped} 窓 / {result.ElapsedMs:F0} ms\n{(result.Complete ? "全件を調査しました。" : "上限・取消・列挙失敗のため一部のみの結果です。候補なしでも問題なしとは限りません。")}\n\n");
                foreach (var group in result.Candidates.GroupBy(item => (item.ProcessId, item.ProcessName, item.Status))) text.AppendLine($"{group.Key.ProcessName} / PID {group.Key.ProcessId} / {(group.Key.Status == "access-denied" ? "権限により読めません" : "短い待機時間内に応答なし")} / {group.Count()} 窓");
                text.AppendLine(result.Candidates.Count == 0 ? "今回の調査では応答のない候補はありません。遅いときに再診断してください。" : "候補には休止中・終了途中のアプリや、権限で読めないものも含まれます。候補だけで原因を断定しないでください。作業を保存して通常終了し、実キーの反応を比較してください。");
                text.AppendLine("\nアプリ起動自体が遅い場合やChromeの初回起動は別の区間です。キー入力から対象表示までを比較し、連打を避けてください。");
                report.Text = text.ToString();
            }
            catch (Exception error) { report.Text = $"診断できませんでした。設定とHotKeyは変更していません。\n{error.Message}"; }
            finally { run.IsEnabled = true; }
        };
    }
}
