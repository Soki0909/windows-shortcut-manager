using ShortcutManager.Core.Models;
using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.Windows.Launching;

public static class ApplicationLaunchAdvice
{
    public static string Describe(ApplicationTarget target, bool inspectRegistration, UriRegistrationInspector? inspector = null)
    {
        if (target.LaunchMode == ApplicationLaunchMode.Executable)
            return "追加引数と作業フォルダを起動先へ渡します。作業フォルダが空欄なら起動先ファイルのあるフォルダを使います。";
        var lines = new List<string>
        {
            target.LaunchMode == ApplicationLaunchMode.Shortcut
                ? "元の.lnkをWindowsに開かせます。元リンクの引数・作業フォルダ等は元リンク側で指定します。元ファイルは変更しません。"
                : "アプリURIをWindowsに開かせます。URIの内容とWindowsの関連付けに従って起動します。",
            "この方式では追加引数を渡しません。保存済みの追加引数は保持しますが、起動には使いません。"
        };
        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory))
            lines.Add("ここでの作業フォルダはExplorerに渡す値です。元リンクやURI先アプリの作業フォルダを上書きする指定ではありません。");
        if (target.LaunchMode == ApplicationLaunchMode.Uri)
            lines.Add(inspectRegistration ? (inspector ?? new()).Inspect(target.Value).Message
                : "「入力を再確認」（F5）または保存時にURIのWindows登録を照会します。照会結果は起動成功の保証ではありません。");
        return string.Join("\n", lines);
    }
}
