using ShortcutManager.Windows.Diagnostics;

namespace ShortcutManager.Windows.Tests;

public sealed class UriRegistrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void RegistrationResultDoesNotClaimLaunchSuccess(bool? registered)
    {
        string? inspected = null;
        var result = new UriRegistrationInspector(scheme => { inspected = scheme; return registered; }).Inspect("example:test");
        Assert.Equal("example", inspected);
        Assert.Equal(registered, result.Registered);
        Assert.NotEmpty(result.Message);
        Assert.DoesNotContain("起動成功しました", result.Message);
    }

    [Theory]
    [InlineData("shell:AppsFolder\\Example")]
    [InlineData("file:///C:/example.txt")]
    public void ShellAndFileUrisDoNotUseSchemeRegistration(string value)
    {
        var result = new UriRegistrationInspector(_ => throw new InvalidOperationException()).Inspect(value);
        Assert.Null(result.Registered);
    }
}
