using Microsoft.Win32;

namespace ShortcutManager.Windows.Diagnostics;

public sealed record UriRegistrationInfo(bool? Registered, string Message);

public sealed class UriRegistrationInspector(Func<string,bool?>? lookup = null)
{
    public UriRegistrationInfo Inspect(string value)
    {
        if (!Uri.TryCreate(value,UriKind.Absolute,out var uri)) return new(false,"URIの形式を確認してください。");
        if (uri.Scheme.Equals("shell",StringComparison.OrdinalIgnoreCase)) return new(null,"Explorerのshell名前空間へ渡します。Windowsアプリの登録状態は実行して確認してください。");
        if (uri.IsFile) return new(null,"ファイルURIをExplorerへ渡します。ファイルの存在と関連付けは実行時にも確認してください。");
        var registered=(lookup ?? Lookup)(uri.Scheme);
        return registered switch
        {
            true=>new(true,"このURI方式のWindows登録を確認しました。実際の起動成功は別に確認してください。"),
            false=>new(false,"このURI方式のWindows登録が見つかりません。未登録、または別の仕組みで登録されたアプリの可能性があります。インストール先とURIを確認してください。"),
            _=>new(null,"権限等によりURIの登録状態を確認できません。登録済みとも未登録とも断定できません。"),
        };
    }

    private static bool? Lookup(string scheme)
    {
        try
        {
            using var key=Registry.ClassesRoot.OpenSubKey(scheme);
            if(key is null) return false;
            using var command=key.OpenSubKey(@"shell\open\command");
            return key.GetValue("URL Protocol") is not null || command is not null;
        }
        catch(Exception error) when(error is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return null; }
    }
}
