using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ShortcutManager.Core.Models;

namespace ShortcutManager.App;

public sealed class ShortcutEditorWindow : Window
{
    private readonly ShortcutEditorState state;
    private readonly TextBox name = new();
    private readonly CheckBox enabled = new() { Content = "この割当を有効にする", IsChecked = true };
    private readonly TextBox legacyKey = new() { IsReadOnly = true };
    private readonly ComboBox key = new() { DisplayMemberPath = "Label", SelectedValuePath = "Value" };
    private readonly ComboBox type = new() { ItemsSource = new[] { "Webサイト", "アプリ・Windowsショートカット", "BAT / CMD", "Chromeタブグループ" } };
    private readonly ComboBox mode = new() { DisplayMemberPath = "Label", SelectedValuePath = "Value" };
    private readonly ComboBox windowMode = new() { DisplayMemberPath = "Label", SelectedValuePath = "Value" };
    private readonly TextBox target = new();
    private readonly TextBox arguments = new();
    private readonly TextBox directory = new();
    private readonly Button browse = new() { Content = "参照…", Margin = new(6,0,0,0) };
    private readonly Button save = new() { Content = "候補に保存", IsDefault = true };
    private readonly TextBlock typeHelp = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0,8,0,10) };
    private readonly TextBlock summary = ThemeResources.ErrorText(new Thickness(0,8,0,8));
    private readonly TextBlock help = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0,8,0,8) };
    private readonly Expander advanced = new() { Header = "任意の詳細（引数・作業フォルダなど）", Margin = new(0,8,0,4) };
    private readonly Dictionary<string, Field> fields = [];
    private readonly TextBlock launchAdvice = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0,8,0,8) };
    private bool syncing;
    private string focusedHelp = "Tabで項目を移動し、F1で入力欄の説明、F5でファイルの存在を再確認できます。";
    public ShortcutDefinition? Result { get; private set; }

    public ShortcutEditorWindow(ShortcutDefinition? definition, string? preselectedKey, IReadOnlyDictionary<string,string>? usedKeys = null, int initialType = 0, ShortcutConfig? context = null, bool validateOnOpen = false)
    {
        state = new(context ?? new(), definition, initialType);
        help.SetResourceReference(OpacityProperty,"SecondaryTextOpacity");
        var occupied = usedKeys ?? new Dictionary<string,string>();
        if (definition is null) state.Key = preselectedKey ?? Enumerable.Range('A',26).Select(value => ((char)value).ToString()).FirstOrDefault(value => !occupied.ContainsKey(value)) ?? string.Empty;
        key.ItemsSource = Enumerable.Range('A',26).Select(value => ((char)value).ToString()).Select(value => new Choice<string>(value, occupied.TryGetValue(value,out var assigned) ? $"{value} — この設定では「{assigned}」が使用中" : $"{value} — この設定では空き")).ToArray();
        mode.ItemsSource = new[] { new Choice<ApplicationLaunchMode>(ApplicationLaunchMode.Executable,"ファイルを直接起動"), new(ApplicationLaunchMode.Shortcut,"既存のWindowsショートカット (.lnk)"), new(ApplicationLaunchMode.Uri,"アプリURI / shell:AppsFolder") };
        windowMode.ItemsSource = new[] { new Choice<BatWindowMode>(BatWindowMode.Normal,"通常のウィンドウ"), new(BatWindowMode.Minimized,"最小化して実行") };
        Title = definition is null ? "ショートカットを追加" : "ショートカットを編集";
        Width = 720; Height = 720; MinWidth = 460; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DialogLayout.Constrain(this);
        var form = NewGrid();
        Add(form,"name","名前",name,"一覧で見分ける名前です。1～80文字。例: 学校のポータル");
        Add(form,"enabled","有効 / 無効",enabled,"無効の割当はリンクを生成しません。起動先の存在確認も省略します。有効に戻す前に確認してください。");
        var keyPanel = new StackPanel();
        keyPanel.Children.Add(new TextBlock { Text="Ctrl + Alt + 選んだ文字（同時押しで登録する必要はありません）",TextWrapping=TextWrapping.Wrap }); keyPanel.Children.Add(key);
        keyPanel.Children.Add(new Expander { Header="実キー入力（既存の割当が起動する場合あり）",Content=legacyKey });
        Add(form,"key","文字キー",keyPanel,"一覧からA–Zを選びます。別アプリやWindowsの既存キーとの競合は変更反映前にも確認します。",key);
        Add(form,"type","対象の種類",type,"種類・起動方法を切り替えても入力を種類ごとに保持します。選択中の種類だけ候補へ保存します。");
        Add(form,"mode","起動方法",mode,"ファイルを直接起動する方法だけ追加引数を渡せます。.lnk / URIはExplorerへ値を渡し、元の起動方法を利用します。");
        var targetPanel = new DockPanel(); DockPanel.SetDock(browse,Dock.Right); targetPanel.Children.Add(browse); targetPanel.Children.Add(target);
        Add(form,"target","起動先",targetPanel,"URL、引用符を付けない絶対ファイルパス、URI、グループ名を種類に合わせて入力します。",target);
        var details = NewGrid();
        Add(details,"arguments","追加引数",arguments,"任意の引数です。例: --profile " + '"' + "作業用" + '"' + "。BATではcmd.exeの引数として解釈されます。引用や記号の意味を確認してください。");
        var directoryPanel = new DockPanel(); var folder = new Button { Content="参照…",Margin=new(6,0,0,0) };
        folder.Click += (_,_) => { var dialog = new OpenFolderDialog { Title="作業フォルダを選ぶ" }; if(dialog.ShowDialog(this)==true) directory.Text=dialog.FolderName; };
        DockPanel.SetDock(folder,Dock.Right); directoryPanel.Children.Add(folder); directoryPanel.Children.Add(directory);
        Add(details,"directory","作業フォルダ",directoryPanel,"直接起動/BATでは空欄なら起動先ファイルのあるフォルダを使います。.lnk/URIではExplorerへ渡す作業フォルダです。元リンクやURI先アプリの作業フォルダを上書きする指定ではありません。指定する場合は存在するフォルダの絶対パスを使います。",directory);
        Add(details,"window","実行時の表示",windowMode,"BAT/CMDのウィンドウを通常表示または最小化します。バックグラウンドの常駐化ではありません。");
        advanced.Content = details;
        var content = new StackPanel { Margin=new(18) };
        content.Children.Add(new TextBlock { Text="この画面は編集候補を作ります。Windowsへの保存はメインの「変更を反映」です。",TextWrapping=TextWrapping.Wrap });
        content.Children.Add(typeHelp); content.Children.Add(form); content.Children.Add(launchAdvice); content.Children.Add(advanced); content.Children.Add(summary); content.Children.Add(help);
        var buttons = new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(12) };
        var recheck = new Button { Content="入力を再確認",ToolTip="ファイルや作業フォルダの存在を再確認します（F5）。" }; recheck.Click += (_,_) => Validate(true,false);
        var cancel = new Button { Content="キャンセル",IsCancel=true }; cancel.Click += (_,_) => DialogResult=false;
        save.Click += (_,_) => { Capture(); if(!Validate(true,true)) return; Result=state.Proposal(); DialogResult=true; };
        buttons.Children.Add(recheck); buttons.Children.Add(cancel); buttons.Children.Add(save);
        var root = new DockPanel(); DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons); root.Children.Add(new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }); Content=root;
        browse.Click += (_,_) => Browse();
        foreach(var box in new[] {name,target,arguments,directory}) box.TextChanged += (_,_) => InputChanged();
        arguments.GotKeyboardFocus += (_,_) => {if(arguments.IsReadOnly) {focusedHelp=fields["arguments"].Panel.ToolTip?.ToString() ?? string.Empty;help.Text=focusedHelp;}};
        enabled.Checked += (_,_) => InputChanged(); enabled.Unchecked += (_,_) => InputChanged();
        key.SelectionChanged += (_,_) => InputChanged(); windowMode.SelectionChanged += (_,_) => InputChanged();
        type.SelectionChanged += (_,_) => { if(syncing) return; Capture(); state.Switch(type.SelectedIndex); Sync(); };
        mode.SelectionChanged += (_,_) => { if(syncing) return; Capture(); state.Switch(state.Kind,mode.SelectedValue is ApplicationLaunchMode value ? value : ApplicationLaunchMode.Executable); Sync(); };
        legacyKey.PreviewKeyDown += (_,e) =>
        {
            if(e.Key is Key.Tab or Key.Escape) return;
            e.Handled=true; var pressed=e.Key==Key.System ? e.SystemKey : e.Key;
            if(Keyboard.Modifiers!=(ModifierKeys.Control|ModifierKeys.Alt) || pressed is < Key.A or > Key.Z) { help.Text="Ctrl+Alt+A–Zだけを受け付けます。通常は文字キー一覧を使ってください。"; return; }
            key.SelectedValue=pressed.ToString();
        };
        PreviewKeyDown += (_,e) => { if(e.Key==Key.F1) { MessageBox.Show(this,focusedHelp,"入力欄の説明"); e.Handled=true; } else if(e.Key==Key.F5) { Validate(true,false); e.Handled=true; } else if(e.Key==Key.Enter && !save.IsEnabled && !key.IsDropDownOpen && !type.IsDropDownOpen && !mode.IsDropDownOpen && !windowMode.IsDropDownOpen) {Validate(true,true);e.Handled=true;} };
        Sync(); advanced.IsExpanded=!string.IsNullOrEmpty(state.Current.Arguments) || !string.IsNullOrEmpty(state.Current.WorkingDirectory) || state.Current.WindowMode!=BatWindowMode.Normal;
        Loaded += (_,_) => {if(validateOnOpen) Validate(true,true);else name.Focus();};
    }

    private static Grid NewGrid()
    {
        var grid=new Grid(); grid.ColumnDefinitions.Add(new() {Width=new(126)}); grid.ColumnDefinitions.Add(new() {Width=new(1,GridUnitType.Star)}); return grid;
    }
    private void Add(Grid grid,string id,string label,FrameworkElement content,string explanation,Control? input=null)
    {
        var row=grid.RowDefinitions.Count; grid.RowDefinitions.Add(new() {Height=GridLength.Auto});
        var title=new TextBlock {Text=label,Margin=new(0,8,10,8),TextWrapping=TextWrapping.Wrap};
        var error=ThemeResources.ErrorText(fontSize:12);
        var panel=new StackPanel {Margin=new(0,5,0,5)}; panel.Children.Add(content); panel.Children.Add(error);
        Grid.SetRow(title,row); Grid.SetRow(panel,row); Grid.SetColumn(panel,1); grid.Children.Add(title); grid.Children.Add(panel);
        input ??= content as Control;
        fields[id]=new(title,panel,error,input);
        content.ToolTip=explanation;
        content.GotKeyboardFocus += (_,_) => { focusedHelp=explanation; help.Text=explanation+"（F1で説明、F5で再確認）"; };
        if(input is not null) System.Windows.Automation.AutomationProperties.SetLabeledBy(input,title);
    }
    private void Capture()
    {
        state.Name=name.Text; state.Enabled=enabled.IsChecked==true; state.Key=key.SelectedValue as string ?? string.Empty;
        state.Current=new(target.Text,arguments.Text,directory.Text,windowMode.SelectedValue is BatWindowMode value ? value : BatWindowMode.Normal);
    }
    private void Sync()
    {
        syncing=true;
        name.Text=state.Name; enabled.IsChecked=state.Enabled; key.SelectedValue=state.Key; legacyKey.Text=$"Ctrl+Alt+{state.Key}";
        type.SelectedIndex=state.Kind; mode.SelectedValue=state.Mode; target.Text=state.Current.Value; arguments.Text=state.Current.Arguments; directory.Text=state.Current.WorkingDirectory; windowMode.SelectedValue=state.Current.WindowMode;
        syncing=false;
        var app=state.Kind==1; var bat=state.Kind==2; var direct=app && state.Mode==ApplicationLaunchMode.Executable;
        Show("mode",app); advanced.Visibility=app||bat ? Visibility.Visible : Visibility.Collapsed;
        Show("arguments",app||bat); Show("directory",app||bat); Show("window",bat);
        arguments.IsReadOnly=app && !direct;
        fields["directory"].Label.Text=app && !direct ? "Explorerの作業フォルダ" : "作業フォルダ";
        fields["arguments"].Panel.ToolTip=arguments.IsReadOnly ? "この方法では追加引数を渡しません。既存の値は保持します。引数が必要ならファイルを直接起動する方法を選んでください。" : "任意の追加引数。引用や記号の意味を確認してください。";
        browse.Visibility=bat || (app && state.Mode!=ApplicationLaunchMode.Uri) ? Visibility.Visible : Visibility.Collapsed;
        fields["target"].Label.Text=state.Kind switch {0=>"WebサイトのURL",1 when state.Mode==ApplicationLaunchMode.Uri=>"アプリURI",1 when state.Mode==ApplicationLaunchMode.Shortcut=>"既存の.lnkファイル",1=>"起動するファイル",2=>"BAT / CMDファイル",_=>"グループ名"};
        typeHelp.Text=state.Kind switch
        {
            0=>"OSの既定ブラウザーで開きます。例: https://example.com。Chrome拡張は不要です。",
            1 when state.Mode==ApplicationLaunchMode.Shortcut=>"元のWindowsショートカットをExplorerへ渡します。追加引数は渡しません。既存の保存値がある場合は保持します。",
            1 when state.Mode==ApplicationLaunchMode.Uri=>"URI / shell:AppsFolder をExplorerへ渡します。追加引数は渡しません。Windowsの登録状態と実際の起動成功は別に確認します。",
            1=>"ファイルを直接起動します。追加引数を渡せます。パスは引用符を付けずに入力してください。",
            2=>"外部のBAT/CMDを実行します。本文を取り込んだり書き換えたりしません。",
            _=>"現在開いているChromeグループの完全一致名です。閉じた保存済みグループは開けません。拡張機能IDは先にメインの「設定」で指定します。",
        };
        typeHelp.Text += "\n種類ごとに入力を保持し、選択中の種類だけ保存します。";
        if(bat || app && state.Mode!=ApplicationLaunchMode.Uri) typeHelp.Text += "\n空白・日本語・共有名から始まるパスを指定できます。共有先の存在確認には接続と権限が必要です。";
        Validate(false,false);
    }
    private void Show(string field,bool visible) { fields[field].Label.Visibility=fields[field].Panel.Visibility=visible ? Visibility.Visible : Visibility.Collapsed; }
    private void InputChanged() { if(syncing) return; Capture(); legacyKey.Text=$"Ctrl+Alt+{state.Key}"; Validate(false,false); }
    private bool Validate(bool fileSystem,bool focus)
    {
        Capture(); var issues=state.Validate(fileSystem);
        launchAdvice.Text=state.Proposal().Target is ApplicationTarget application
            ? Windows.Launching.ApplicationLaunchAdvice.Describe(application,fileSystem && state.Enabled) : string.Empty;
        launchAdvice.Visibility=launchAdvice.Text.Length==0 ? Visibility.Collapsed : Visibility.Visible;
        foreach(var pair in fields) { pair.Value.Error.Text=string.Join("\n",issues.Where(issue=>issue.Field==pair.Key).Select(issue=>issue.Message)); if(pair.Value.Input is { } input) { if(pair.Value.Error.Text.Length>0) input.SetResourceReference(Control.BorderBrushProperty,"DangerBrush"); else input.ClearValue(Control.BorderBrushProperty); } }
        summary.Text=string.Join("\n",issues.Where(issue=>!fields.ContainsKey(issue.Field)).Select(issue=>issue.Field=="settings" ? issue.Message+" キャンセルしてメインの「設定」で修正してください。" : issue.Message));
        if(!state.Enabled) summary.Text += "\n無効のため起動先の存在確認は省略します。有効にする前に確認してください。";
        if(state.OtherIssueCount>0) summary.Text += $"\nほかの割当にも {state.OtherIssueCount} 件の問題があります。現在の対象を修正した後、メイン一覧で確認してください。";
        save.IsEnabled=issues.Count==0;
        if(focus && issues.Count>0 && fields.TryGetValue(issues[0].Field,out var field)) { if(issues[0].Field is "arguments" or "directory" or "window") advanced.IsExpanded=true; field.Input?.Focus(); field.Panel.BringIntoView(); }
        return issues.Count==0;
    }
    private void Browse()
    {
        var filter=state.Kind==2 ? "BAT / CMD|*.bat;*.cmd" : state.Mode==ApplicationLaunchMode.Shortcut ? "Windowsショートカット|*.lnk" : "実行ファイル|*.exe;*.com|すべてのファイル|*.*";
        var dialog=new OpenFileDialog {Filter=filter,CheckFileExists=true,Title=fields["target"].Label.Text}; if(dialog.ShowDialog(this)==true) target.Text=dialog.FileName;
    }
    private sealed record Choice<T>(T Value,string Label);
    private sealed record Field(TextBlock Label,StackPanel Panel,TextBlock Error,Control? Input);
}
