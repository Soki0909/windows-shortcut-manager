using ShortcutManager.Core.Models;
using ShortcutManager.Windows.Diagnostics;
using ShortcutManager.Windows.Launching;

namespace ShortcutManager.Windows.Tests;

public sealed class ApplicationLaunchAdviceTests
{
    [Fact]
    public void TypingNeverQueriesRegistrationAndExplicitRecheckExplainsUnknownScheme()
    {
        var calls = 0;
        var inspector = new UriRegistrationInspector(_ => { calls++; return false; });
        var target = new ApplicationTarget { LaunchMode = ApplicationLaunchMode.Uri, Value = "fixture:payload", Arguments = "old", WorkingDirectory = @"C:\Example" };
        var initial = ApplicationLaunchAdvice.Describe(target, false, inspector);
        Assert.Equal(0, calls); Assert.Contains("F5", initial); Assert.Contains("起動には使いません", initial); Assert.Contains("Explorerに渡す", initial);
        var explicitResult = ApplicationLaunchAdvice.Describe(target, true, inspector);
        Assert.Equal(1, calls); Assert.Contains("登録が見つかりません", explicitResult);
    }

    [Theory]
    [InlineData(ApplicationLaunchMode.Executable)]
    [InlineData(ApplicationLaunchMode.Shortcut)]
    public void NonUriModesNeverQueryUriRegistration(ApplicationLaunchMode mode)
    {
        var target = new ApplicationTarget { LaunchMode = mode, Value = @"C:\Example\app.exe" };
        var message = ApplicationLaunchAdvice.Describe(target, true, new(_ => throw new InvalidOperationException()));
        Assert.Contains(mode == ApplicationLaunchMode.Executable ? "起動先へ渡します" : "元リンク側で指定", message);
    }
}
