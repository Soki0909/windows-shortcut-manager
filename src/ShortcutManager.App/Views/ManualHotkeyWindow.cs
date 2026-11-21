using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.App;

internal sealed class ManualHotkeyWindow : Window
{
    internal InventoryScanReport? VerifiedReport { get; private set; }

    internal ManualHotkeyWindow(ShortcutInventoryItem source, ManualHotkeyMigration migration,
        Func<Task<InventoryScanReport>> rescan)
    {
        Title = "元のショートカットキーを手動で解除";
        Width = 690;
        Height = 650;
        MinWidth = 430;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DialogLayout.Constrain(this);

        var copyButton = new Button { Content = "対象を確認して復旧用コピーを保存" };
        var properties = new Button { Content = "元ファイルのプロパティを開く", IsEnabled = false };
        var verify = new Button { Content = "再スキャンして解除を確認", IsEnabled = false };
        var backupLocation = new Button { Content = "復旧用コピーの場所を表示", IsEnabled = false };
        var originalLocation = new Button { Content = "元ファイルの場所を表示", IsEnabled = false };
        var close = new Button { Content = "戻る", IsCancel = true };
        var status = Text("最初に対象のファイルとキーを確認してください。");
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var backupPath = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        AutomationProperties.SetName(backupPath, "復旧用コピーの保存先");
        HotkeyRecoveryCopy? copy = null;
        var busy = false;
        Closing += (_, args) => args.Cancel = busy;

        async Task RunAsync(Func<Task> action)
        {
            busy = true;
            copyButton.IsEnabled = properties.IsEnabled = verify.IsEnabled = backupLocation.IsEnabled = originalLocation.IsEnabled = close.IsEnabled = false;
            try { await action(); }
            catch (Exception error) { status.Text = error.Message; }
            finally
            {
                busy = false;
                copyButton.IsEnabled = copy is null;
                properties.IsEnabled = verify.IsEnabled = backupLocation.IsEnabled = originalLocation.IsEnabled = copy is not null;
                close.IsEnabled = true;
            }
        }

        copyButton.Click += async (_, _) => await RunAsync(async () =>
        {
            copy = await Task.Run(() => migration.CreateCopy(source));
            status.Text = "復旧用コピーを保存し、原本と内容が一致することを確認しました。";
            backupPath.Text = copy.BackupPath;
            backupPath.Visibility = Visibility.Visible;
        });
        properties.Click += async (_, _) => await RunAsync(async () =>
        {
            await Task.Run(() => migration.CheckBeforeProperties(copy!));
            try
            {
                const uint filePath = 2;
                if (SHObjectProperties(new WindowInteropHelper(this).Handle, filePath, source.SourcePath, null))
                {
                    status.Text = "Windowsのプロパティでキーを解除したら、閉じて「再スキャンして解除を確認」を押してください。";
                    return;
                }
            }
            catch (EntryPointNotFoundException) { }
            status.Text = "プロパティを開けませんでした。「元ファイルの場所を表示」で選択されたファイルのプロパティを開いてください。";
        });
        originalLocation.Click += async (_, _) => await RunAsync(async () =>
        {
            await Task.Run(() => migration.CheckBeforeProperties(copy!));
            InventoryLocation.Show(this, source.SourcePath);
        });
        backupLocation.Click += (_, _) => InventoryLocation.Show(this, copy!.BackupPath);
        verify.Click += async (_, _) =>
        {
            await RunAsync(async () =>
            {
                status.Text = "元ファイルと同じキーを使うほかのファイルを調べています…";
                var report = await rescan();
                await Task.Run(() => migration.VerifyRelease(source, report));
                VerifiedReport = report;
            });
            if (VerifiedReport is not null) DialogResult = true;
        };

        var body = new StackPanel { Margin = new Thickness(18) };
        body.Children.Add(Text("解除するキー  " + source.Hotkey));
        var targetPath = new TextBox { Text = source.SourcePath, TextWrapping = TextWrapping.Wrap, IsReadOnly = true };
        AutomationProperties.SetName(targetPath, "元ファイルのパス");
        body.Children.Add(targetPath);
        body.Children.Add(Text("復旧用コピーを保存してから、Windowsのプロパティで「ショートカット キー」を「なし」にして保存してください。欄が見つからない、または変更できない場合は、戻って別のキーを選べます。"));
        body.Children.Add(new WrapPanel { Children = { copyButton, properties, originalLocation } });
        body.Children.Add(status);
        body.Children.Add(backupPath);
        body.Children.Add(new Expander
        {
            Header = "元に戻す方法",
            Content = new StackPanel
            {
                Children =
                {
                    Text("同じキーを移行先で反映済みなら、先にその割当のキーを変更または無効にして反映します。復旧用ZIPの original フォルダーから元ファイルを取り出し、上の元パスとファイル名を照合して、エクスプローラーで元の場所に戻します。最後にもう一度スキャンしてください。ZIP内の migration-source.json でも元パスとキーを確認できます。"),
                    backupLocation
                }
            }
        });
        body.Children.Add(new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { close, verify } });
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(IntPtr owner, uint objectType, string objectName, string? propertyPage);
}
