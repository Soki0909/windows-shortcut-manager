using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using ShortcutManager.App;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Validation;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.Backup;
using ShortcutManager.Windows.Chrome;
using ShortcutManager.Windows.Inventory;
using ShortcutManager.Windows.Launching;
using ShortcutManager.Windows.Logging;
using ShortcutManager.Windows.Shell;
using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Windows.Storage;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("edit" or "audit")) return 2;
        var root = Path.GetFullPath(args[1]);
        var temp = Path.GetFullPath(Path.GetTempPath());
        const string prefix = "shortcut-manager-gui-";
        var name = Path.GetFileName(root);
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
            !string.Equals(Path.GetDirectoryName(root), Path.TrimEndingDirectorySeparator(temp), StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(root) || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint) ||
            !File.Exists(Path.Combine(root, ".gui-probe")))
            return 2;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, failure) =>
        {
            failure.Handled = true;
            Console.Error.WriteLine(failure.Exception);
            app.Shutdown(1);
        };
        try
        {
            Log("load WPF resources");
            app.Resources = ReadAppResources();
            using var theme = new ThemeResources(app);
            var scenario = new GuiScenario(root);
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (args[0] == "edit") await scenario.EditAsync();
                    else await scenario.AuditAsync();
                    Log("completed " + args[0]);
                    app.Shutdown(0);
                }
                catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
            }));
            return app.Run();
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static ResourceDictionary ReadAppResources()
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var document = XDocument.Load(Path.Combine(repository, "src", "ShortcutManager.App", "App.xaml"));
        XNamespace namespaceWpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace namespaceXaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var entries = document.Root!.Element(namespaceWpf + "Application.Resources")!.Elements().Single().Elements();
        var dictionary = new XElement(namespaceWpf + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", namespaceXaml), entries);
        return (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
    }

    internal static void Log(string step)
    {
        Console.WriteLine(step);
        Console.Out.Flush();
    }
}

internal sealed class GuiScenario
{
    private const string FirstName = "架空の学校ポータル";
    private const string EditedName = "編集した学校ポータル";
    private const string FirstUrl = "https://example.invalid/first";
    private const string EditedUrl = "https://example.invalid/edited";
    private const string SourceContents = "[InternetShortcut]\r\nURL=https://example.invalid/source\r\n";
    private readonly ApplicationPaths paths;
    private readonly string sourcePath;
    private readonly string scanRoot;
    private readonly ConfigRepository repository;
    private readonly ShellLinkService links = new();
    private readonly ConfigValidator validator = new();
    private readonly LaunchSpecResolver resolver = new(new ChromeLocator(), new DispatcherUriBuilder());
    private readonly SimulatedReload shell = new();
    private readonly ManagedShortcutApplier applier;
    private readonly BackupService backups;
    private bool editingExisting;
    private bool guideOffered;
    private int reloadPrompts;
    private Exception? dialogError;

    internal GuiScenario(string root)
    {
        paths = new(root, Path.Combine(root, "config.json"), Path.Combine(root, "state.json"),
            Path.Combine(root, "log.txt"), Path.Combine(root, "backups"), Path.Combine(root, "stage"),
            Path.Combine(root, "managed"), IsDevelopment: true);
        scanRoot = Path.Combine(root, "inventory");
        sourcePath = Path.Combine(scanRoot, "source.url");
        repository = new(paths.ConfigPath);
        backups = new(paths.BackupDirectory, "gui-fixture");
        applier = new(repository, validator, resolver, links, backups, paths.StagingDirectory, paths.StatePath);
    }

    internal async Task EditAsync()
    {
        Program.Log("empty startup and first-run guide");
        Check(!File.Exists(paths.ConfigPath) && !Directory.Exists(paths.ManagedShortcutDirectory), "Expected empty synthetic user data");
        Directory.CreateDirectory(scanRoot);
        File.WriteAllText(sourcePath, SourceContents);
        var (main, model) = Open(auditOnly: false);
        await Until(() => guideOffered, "first-run guide did not finish");
        Check(model.Shortcuts.Single().Name == FirstName, "Guide did not add the website");
        Check(!model.IsDirty && model.ShellReflectionPending && shell.Calls == 0 && reloadPrompts == 1,
            "Declining restart must preserve the saved configuration and keep reflection pending");
        CheckSaved(FirstName, FirstUrl, 1);
        var first = repository.Load().Shortcuts.Single();

        Program.Log("edit existing row, then apply");
        var grid = Children<DataGrid>(main).Single();
        grid.SelectedItem = model.Shortcuts.Single();
        editingExisting = true;
        Click(Button(main, "編集(_E)"));
        Check(model.IsDirty && model.Shortcuts.Single().Name == EditedName, "Edit did not update the visible candidate");
        CheckSaved(FirstName, FirstUrl, 1);
        Click(Button(main, UiText.ApplyWithAccessKey));
        await Until(() => shell.Calls == 1 && !model.IsBusy, "accepting the restart prompt did not call the simulated Explorer reload");
        Check(!model.IsDirty && !model.ShellReflectionPending && reloadPrompts == 2,
            "Accepting restart must clear pending reflection after the reload succeeds");
        Check(!model.Shortcuts.Single().StateLabel.Contains("確認済み", StringComparison.Ordinal),
            "Explorer reload must not claim a physical shortcut-key test");
        CheckSaved(EditedName, EditedUrl, 2);
        Check(repository.Load().Shortcuts.Single().Id == first.Id, "Editing an existing row must keep its identity");
        main.Close();
    }

    internal async Task AuditAsync()
    {
        Program.Log("restart process and audit saved managed link");
        var link = CheckSaved(EditedName, EditedUrl, 2);
        var before = File.ReadAllBytes(link);
        var config = File.ReadAllBytes(paths.ConfigPath);
        var (main, model) = Open(auditOnly: false);
        await Until(() => model.AuditNotice.Contains("一致しています", StringComparison.Ordinal), "startup did not verify the saved link");
        Check(model.Shortcuts.Single().Name == EditedName && !model.IsDirty && !guideOffered && reloadPrompts == 0,
            "Saved startup must show the edited row without another guide or restart prompt");
        Check(before.SequenceEqual(File.ReadAllBytes(link)), "Passing audit must retain the managed link bytes");

        Program.Log("detect drift via audit command");
        var saved = repository.Load();
        var expected = resolver.Resolve(saved, saved.Shortcuts.Single());
        links.Write(link, new(expected.TargetPath, "https://example.invalid/drift", expected.WorkingDirectory,
            expected.Hotkey, expected.IconLocation, expected.Description, expected.WindowStyle));
        var driftBytes = File.ReadAllBytes(link);
        var auditMenu = Children<MenuItem>(main).Single(item => Equals(item.Header, "保存済みを監査（編集は保持）"));
        Check(auditMenu.IsEnabled, "Audit menu must be enabled for the saved row");
        auditMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Until(() => !model.IsBusy && model.LastAuditDetails.Count > 0, "manual audit did not report link drift");
        Check(File.ReadAllBytes(link).SequenceEqual(driftBytes) && File.ReadAllBytes(paths.ConfigPath).SequenceEqual(config),
            "Read-only audit must keep the changed link and saved settings intact");
        main.Close();

        Program.Log("restart in audit-only mode with drift");
        var (reopened, reopenedModel) = Open(auditOnly: true);
        await Until(() => !reopenedModel.IsBusy && reopenedModel.LastAuditDetails.Count > 0, "audit-only startup did not report drift");
        Check(File.ReadAllBytes(link).SequenceEqual(driftBytes) && File.ReadAllBytes(paths.ConfigPath).SequenceEqual(config),
            "Audit-only startup must not repair the link");
        Check(Directory.GetFiles(paths.BackupDirectory, "*.zip").Length == 2 && File.ReadAllText(sourcePath) == SourceContents,
            "Audit must preserve backup count and source shortcut");
        Check(!guideOffered && reloadPrompts == 0 && shell.Calls == 0, "Audit must not launch an onboarding or Explorer prompt");
        reopened.Close();
    }

    private (MainWindow Window, MainViewModel Model) Open(bool auditOnly)
    {
        var scanner = new ShortcutInventoryScanner(links, new());
        var model = new MainViewModel(paths, repository, validator, resolver, new ProcessLauncher(), scanner,
            new ShortcutImporter(), backups, applier, new RestoreService(validator, applier),
            new FileLogger(paths.LogPath), shell, () => [scanRoot]);
        var main = new MainWindow(model, new ManagedShortcutAuditor(validator, resolver, links),
            ShowFixtureDialog, ConfirmFixtureRestart);
        Hide(main);
        main.AttachStartupActions(auditOnly);
        main.Show();
        return (main, model);
    }

    private bool? ShowFixtureDialog(Window dialog)
    {
        Hide(dialog);
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                switch (dialog)
                {
                    case GettingStartedWindow guide:
                        Check(!guideOffered, "First-run guide reopened unexpectedly");
                        await FinishFirstRunAsync(guide);
                        guideOffered = true;
                        guide.Close();
                        break;
                    case ShortcutEditorWindow editor:
                        FinishEditor(editor);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected dialog {dialog.GetType().Name}");
                }
            }
            catch (Exception error)
            {
                dialogError = error;
                dialog.Close();
            }
        }));
        var result = dialog.ShowDialog();
        if (dialogError is not null) throw new InvalidOperationException("GUI action failed inside modal dialog", dialogError);
        return result;
    }

    private async Task FinishFirstRunAsync(GettingStartedWindow guide)
    {
        var apply = Button(guide, UiText.Apply);
        Check(!apply.IsEnabled && !Button(guide, UiText.TestTarget).IsEnabled, "Empty guide must disable Apply and test launch");
        Click(Button(guide, "Webサイトを追加"));
        Check(apply.IsEnabled && Button(guide, UiText.TestTarget).IsEnabled, "Valid saved candidate must enable guide actions");
        Check(!File.Exists(paths.ConfigPath), "Save Candidate wrote configuration before Apply");
        Click(apply);
        await Until(() => reloadPrompts == 1 && !apply.IsEnabled, "first Apply did not offer Explorer restart");
    }

    private void FinishEditor(ShortcutEditorWindow editor)
    {
        var save = Button(editor, UiText.SaveCandidate);
        var name = Field(editor, "名前");
        var target = Field(editor, "WebサイトのURL");
        var key = Children<ComboBox>(editor).Single(value => (AutomationProperties.GetLabeledBy(value) as TextBlock)?.Text == "文字キー");
        if (editingExisting)
            Check(name.Text == FirstName && target.Text == FirstUrl && Equals(key.SelectedValue, "Q"), "Editor must load the present assignment");
        name.Text = editingExisting ? EditedName : FirstName;
        key.SelectedValue = "Q";
        target.Text = "invalid";
        Click(Button(editor, "入力を再確認"));
        Check(!save.IsEnabled, "An invalid URL must keep Save Candidate disabled");
        target.Text = editingExisting ? EditedUrl : FirstUrl;
        Click(save);
    }

    private bool ConfirmFixtureRestart(Window owner, string message, string title)
    {
        reloadPrompts++;
        Check(title == "エクスプローラーを再起動" && message.Contains("タスクバー", StringComparison.Ordinal),
            "Restart confirmation must describe Explorer side effects");
        Check(reloadPrompts == 1 && owner is GettingStartedWindow || reloadPrompts == 2 && owner is MainWindow,
            "Restart prompt must belong to the initiating window");
        return reloadPrompts == 2;
    }

    private string CheckSaved(string name, string url, int backupCount)
    {
        var saved = repository.Load().Shortcuts.Single();
        Check(saved.Name == name && saved.Target is WebsiteTarget website && website.Url == url && saved.Hotkey.Key == "Q",
            "Saved assignment did not match the editor inputs");
        var link = Directory.GetFiles(paths.ManagedShortcutDirectory, "*.lnk").Single();
        var actual = links.Read(link);
        Check(actual.Arguments.Contains(url, StringComparison.Ordinal) &&
            actual.Hotkey.Equals("Ctrl+Alt+Q", StringComparison.OrdinalIgnoreCase), "Generated link has incorrect URL or key");
        Check(Directory.GetFiles(paths.BackupDirectory, "*.zip").Length == backupCount, "Apply did not create the expected backup");
        Check(File.ReadAllText(sourcePath) == SourceContents, "Unmanaged source shortcut changed");
        return link;
    }

    private static void Hide(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.Opacity = 0;
    }

    private static TextBox Field(Window window, string label) =>
        Children<TextBox>(window).Single(value => (AutomationProperties.GetLabeledBy(value) as TextBlock)?.Text == label);
    private static Button Button(Window window, string content) =>
        Children<Button>(window).Single(button => Equals(button.Content, content));
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var nested in Children<T>(child)) yield return nested;
        }
    }

    private static void Click(Button button)
    {
        Check(button.IsEnabled, $"Cannot click disabled button {button.Content}");
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    }
    private static async Task Until(Func<bool> condition, string failure)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
        Check(condition(), failure);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SimulatedReload : IExplorerShellReloader
    {
        public int Calls { get; private set; }
        public Task<ExplorerShellReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ExplorerShellReloadResult(true, "Simulated Explorer restart"));
        }
    }
}
