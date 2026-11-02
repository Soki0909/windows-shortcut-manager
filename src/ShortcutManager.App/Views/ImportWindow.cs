using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ShortcutManager.Core.Models;
using ShortcutManager.Windows.Inventory;

namespace ShortcutManager.App;

public sealed class ImportWindow : Window
{
    private readonly DataGrid grid = new() { AutoGenerateColumns=false, IsReadOnly=true, SelectionMode=DataGridSelectionMode.Single, EnableRowVirtualization=true };
    private readonly ComboBox keys=new() {DisplayMemberPath="Label",SelectedValuePath="Key",MaxWidth=900};
    public ShortcutInventoryItem? Selected => grid.SelectedItem as ShortcutInventoryItem;
    public string? SelectedKey => keys.SelectedValue as string;

    public ImportWindow(IReadOnlyList<ShortcutInventoryItem> items, ShortcutConfig config, InventoryScanReport? report)
    {
        Title="既存ショートカットから移行";Width=1050;Height=740;MinWidth=600;MinHeight=460;WindowStartupLocation=WindowStartupLocation.CenterOwner;DialogLayout.Constrain(this);
        keys.ItemsSource=ImportKeyChoices.Build(config,items);
        var choices=new Style(typeof(ComboBoxItem));choices.Setters.Add(new Setter(IsEnabledProperty,new Binding(nameof(ImportKeyChoice.Available))));keys.ItemContainerStyle=choices;
        var search=new TextBox();var withKey=new CheckBox {Content="キーあり"};var readable=new CheckBox {Content="移行可能のみ"};
        System.Windows.Automation.AutomationProperties.SetName(search,"移行候補を検索");System.Windows.Automation.AutomationProperties.SetName(keys,"移行先の文字キー");
        var view=CollectionViewSource.GetDefaultView(items.Where(item=>!item.IsManaged).ToList());
        view.Filter=value=>value is ShortcutInventoryItem item && InventorySearch.Matches(item,search.Text,withKey.IsChecked==true,readable.IsChecked==true);
        grid.ItemsSource=view;
        var count=new TextBlock {Margin=new(0,5,0,5)};
        void Refresh() {view.Refresh();count.Text=$"表示 {view.Cast<object>().Count()} 件 / 管理外 {items.Count(item=>!item.IsManaged)} 件";}
        search.TextChanged+=(_,_)=>Refresh();withKey.Click+=(_,_)=>Refresh();readable.Click+=(_,_)=>Refresh();
        var fields=new[] {("キー",nameof(ShortcutInventoryItem.Hotkey),110d),("名前",nameof(ShortcutInventoryItem.DisplayName),180d),("形式",nameof(ShortcutInventoryItem.FileType),65d),("起動先",nameof(ShortcutInventoryItem.Target),230d),("読取りエラー",nameof(ShortcutInventoryItem.ParseError),180d)};
        foreach(var (label,path,width) in fields) grid.Columns.Add(new DataGridTextColumn {Header=label,Binding=new Binding(path),Width=width});
        var detail=new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=90,MaxHeight=140};
        var import=new Button {Content="選んだキーで候補へ追加",IsDefault=true,IsEnabled=false};
        void Update() {
            var item=Selected;
            detail.Text=item is null ? "一覧で移行元を選ぶと、元ファイル・起動先・競合を確認できます。" : $"元ファイル: {item.SourcePath}\n元キー: {(string.IsNullOrEmpty(item.Hotkey)?"なし":item.Hotkey)}\n起動先: {item.Target}\n引数: {item.Arguments}\n作業フォルダ: {item.WorkingDirectory}\n{item.ParseError}\n"+(item.FileType.Equals(".lnk",StringComparison.OrdinalIgnoreCase)?"元の.lnkを参照して起動条件を保持します。移行後も元ファイルが必要です。":"WebサイトのURLを候補へ複製します。")+"\n元ファイルと元キーは変更しません。同じキーが残る場合は競合します。";
            if(config.Shortcuts.Count>=26) detail.Text+="\n割当は最大26件です。キャンセルして不要な候補を整理してください。";
            else if(item is not null && !ImportKeyChoices.CanImport(item)) detail.Text+="\nこの形式または読取り結果は移行対象外です。";
            import.IsEnabled=config.Shortcuts.Count<26 && item is not null && ImportKeyChoices.CanImport(item) && keys.SelectedItem is ImportKeyChoice {Available:true};
        }
        grid.SelectionChanged+=(_,_)=>Update();keys.SelectionChanged+=(_,_)=>Update();
        import.Click+=(_,_)=> {if(import.IsEnabled) DialogResult=true;};
        var cancel=new Button {Content="キャンセル",IsCancel=true};cancel.Click+=(_,_)=>DialogResult=false;
        var location=new Button {Content="元ファイルの場所を表示"};location.Click+=(_,_)=> {if(Selected is { } item) InventoryLocation.Show(this,item.SourcePath);};
        var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(location);buttons.Children.Add(cancel);buttons.Children.Add(import);
        var top=new StackPanel();top.Children.Add(new TextBlock {Text=(report?.Summary ?? "指定範囲から検出した候補です。")+"\nアプリが動的に取得したキーは検出できません。空きは今回読めたファイル内での判定です。",TextWrapping=TextWrapping.Wrap});
        top.Children.Add(new TextBlock {Text="名前・キー・起動先・元パス・エラーで検索",Margin=new(0,8,0,0)});top.Children.Add(search);
        var filters=new WrapPanel();filters.Children.Add(withKey);filters.Children.Add(readable);top.Children.Add(filters);top.Children.Add(count);
        top.Children.Add(new Expander {Header="走査範囲と未走査・除外の詳細",Content=new TextBox {IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=110,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text=string.Join("\n",report?.Roots ?? [])+"\n"+string.Join("\n",report?.Issues.Select(issue=>$"{issue.Path}: {issue.Message}") ?? [])}});
        var bottom=new StackPanel();bottom.Children.Add(detail);bottom.Children.Add(new TextBlock {Text="移行先の文字キー（QWERTY順）。外部使用中のキーは選べません。",TextWrapping=TextWrapping.Wrap});bottom.Children.Add(keys);bottom.Children.Add(buttons);
        var panel=new DockPanel {Margin=new(14)};DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);panel.Children.Add(grid);Content=panel;Refresh();Update();
    }
}

internal static class InventoryLocation
{
    public static void Show(Window owner,string path)
    {
        try {
            if(!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException("元ファイルが見つかりません。再スキャンしてください。");
            var info=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")) {UseShellExecute=false};
            info.Arguments="/select,"+Windows.Launching.LaunchSpecResolver.Quote(System.IO.Path.GetFullPath(path));System.Diagnostics.Process.Start(info);
        }
        catch(Exception error) {MessageBox.Show(owner,error.Message,"場所を表示できません",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
}
