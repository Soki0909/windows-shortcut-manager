using ShortcutManager.Core.Models;
using ShortcutManager.Core.Protocol;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Utilities;
using ShortcutManager.Core.Validation;

namespace ShortcutManager.Core.Tests;

public sealed class CoreTests
{
    private const string ExtensionId = "abcdefghijklmnopabcdefghijklmnop";

    [Fact]
    public void Config_RoundTrips_AllTargetTypes_AndUnicode()
    {
        var config = SampleConfig(
            Entry("A", "Site", new WebsiteTarget { Url = "https://example.com/" }),
            Entry("B", "App", new ApplicationTarget { LaunchMode = ApplicationLaunchMode.Uri, Value = "mailto:test@example.com" }),
            Entry("C", "BAT", new BatTarget { Path = "C:\\tools\\task.cmd", WindowMode = BatWindowMode.Minimized }),
            Entry("R", "研究", new ChromeTabGroupTarget { GroupName = "研究 🚀" }));

        var result = ConfigJson.Deserialize(ConfigJson.Serialize(config));
        Assert.Equal(4, result.Shortcuts.Count);
        Assert.IsType<ChromeTabGroupTarget>(result.Shortcuts[3].Target);
        Assert.Equal("研究 🚀", ((ChromeTabGroupTarget)result.Shortcuts[3].Target).GroupName);
    }

    [Fact]
    public void Config_Rejects_UnknownProperties() => Assert.ThrowsAny<System.Text.Json.JsonException>(() => ConfigJson.Deserialize("{\"schemaVersion\":1,\"dispatchProtocolVersion\":1,\"settings\":{\"chrome\":{\"executablePath\":null,\"extensionId\":\"\"}},\"shortcuts\":[],\"extra\":true}"));

    [Theory]
    [InlineData("研究", "%E7%A0%94%E7%A9%B6")]
    [InlineData("7脱", "7%E8%84%B1")]
    [InlineData("hello world", "hello%20world")]
    [InlineData("🚀", "%F0%9F%9A%80")]
    public void DispatcherUri_Encodes_GroupName(string name, string expected) => Assert.EndsWith($"group={expected}", new DispatcherUriBuilder().BuildFocusGroupUri(ExtensionId, name).AbsoluteUri);

    [Fact]
    public void DispatcherUri_Rejects_InvalidExtensionId() => Assert.Throws<ArgumentException>(() => new DispatcherUriBuilder().BuildFocusGroupUri("bad", "研究"));

    [Fact]
    public void Validator_DetectsDuplicateHotkeyAndId()
    {
        var id = Guid.NewGuid();
        var left = Entry("A", "Left", new WebsiteTarget { Url = "https://example.com" }) with { Id = id };
        var right = Entry("A", "Right", new WebsiteTarget { Url = "https://example.org" }) with { Id = id };
        var result = new ConfigValidator().Validate(SampleConfig(left, right), checkFileSystem: false);
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate-id");
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "duplicate-hotkey"));
    }

    [Fact]
    public void DisabledDuplicate_DoesNotBlockApply()
    {
        var left = Entry("A", "Left", new WebsiteTarget { Url = "https://example.com" });
        var right = Entry("A", "Right", new WebsiteTarget { Url = "https://example.org" }) with { Enabled = false };
        Assert.True(new ConfigValidator().Validate(SampleConfig(left, right), checkFileSystem: false).IsValid);
    }

    [Fact]
    public void DisabledMissingTarget_DoesNotBlockApply()
    {
        var disabled = Entry("Z", "Missing", new BatTarget { Path = "C:\\missing\\task.bat" }) with { Enabled = false };
        Assert.True(new ConfigValidator().Validate(SampleConfig(disabled), checkFileSystem: true).IsValid);
    }

    [Theory]
    [InlineData("file:///tmp/no", "invalid-website")]
    [InlineData("not a url", "invalid-website")]
    public void Validator_RejectsInvalidWebsite(string url, string code)
    {
        var result = new ConfigValidator().Validate(SampleConfig(Entry("A", "Site", new WebsiteTarget { Url = url })), false);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    [Fact]
    public void Validator_RejectsInvalidSchemaAndExtensionId()
    {
        var config = SampleConfig(Entry("R", "研究", new ChromeTabGroupTarget { GroupName = "研究" })) with { SchemaVersion = 2, Settings = new() { Chrome = new() { ExtensionId = "bad" } } };
        var result = new ConfigValidator().Validate(config, false);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-schema-version");
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-extension-id");
    }

    [Fact]
    public void Filename_SanitizesInvalidReservedAndLongNames()
    {
        Assert.Equal("A - CON_.lnk", ManagedFilename.For(Entry("A", "CON?", new WebsiteTarget { Url = "https://example.com" })));
        Assert.True(ManagedFilename.For(Entry("B", new string('長', 100), new WebsiteTarget { Url = "https://example.com" })).Length <= 88);
    }

    private static ShortcutConfig SampleConfig(params ShortcutDefinition[] shortcuts) => new() { Settings = new() { Chrome = new() { ExtensionId = ExtensionId } }, Shortcuts = [.. shortcuts] };
    private static ShortcutDefinition Entry(string key, string name, ShortcutTarget target) => new() { Name = name, Hotkey = new() { Key = key }, Target = target };
}
