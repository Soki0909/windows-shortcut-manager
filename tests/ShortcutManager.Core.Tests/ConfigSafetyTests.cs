using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Validation;

namespace ShortcutManager.Core.Tests;

public sealed class ConfigSafetyTests
{
    [Theory]
    [InlineData("{\"settings\":null}", "$.settings")]
    [InlineData("{}", "$.schemaVersion")]
    [InlineData("{\"shortcuts\":null}", "$.shortcuts")]
    [InlineData("{\"shortcuts\":[null]}", "$.shortcuts[0]")]
    [InlineData("{\"shortcuts\":[{\"name\":null}]}", ".name")]
    [InlineData("{\"shortcuts\":[{\"hotkey\":null}]}", ".hotkey")]
    [InlineData("{\"shortcuts\":[{\"target\":null}]}", ".target")]
    [InlineData("{\"shortcuts\":[{\"target\":{\"kind\":\"application\",\"launchMode\":999}}]}", "launchMode")]
    public void InvalidJsonReturnsRepairLocation(string json, string location)
        => Assert.Contains(location, Assert.Throws<JsonException>(() => ConfigJson.Deserialize(json)).Message);

    [Fact]
    public void ProgrammaticNullsAndUndefinedEnumsNeverReachLaunchResolution()
    {
        var validator = new ConfigValidator();
        Assert.False(validator.Validate(new() { Settings = null! }).IsValid);
        var config = new ShortcutConfig { Shortcuts = [new() { Target = new BatTarget { WindowMode = (BatWindowMode)999 } }] };
        Assert.Contains(validator.Validate(config).Issues, issue => issue.Path == "$.shortcuts[0].target.windowMode");
        config = new() { Shortcuts = [new() { Hotkey = new() { Modifiers = [null!] } }] };
        Assert.False(validator.Validate(config).IsValid);
    }

    [Fact]
    public void RejectsHugeTextAndLists()
    {
        Assert.Throws<JsonException>(() => ConfigJson.Deserialize(new string(' ', ConfigStructure.MaxJsonCharacters + 1)));
        var config = new ShortcutConfig { Shortcuts = [new() { Name = new string('a', 100000) }] };
        Assert.Contains(new ConfigValidator().Validate(config).Issues, issue => issue.Path == "$.shortcuts[0].name");
        Assert.False(new ConfigValidator().Validate(new() { Shortcuts = Enumerable.Range(0, 27).Select(_ => new ShortcutDefinition()).ToList() }).IsValid);
    }

    [Fact]
    public void RejectsAggregateSizeThatCouldNotBeReloadedAfterApply()
    {
        var config = new ShortcutConfig { Shortcuts = Enumerable.Range(0, 26).Select(index => new ShortcutDefinition {
            Name = "Test", Hotkey = new() { Key = ((char)('A' + index)).ToString() },
            Target = new ApplicationTarget { Value = new string('a', 32767), Arguments = new string('b', 32767), WorkingDirectory = new string('c', 32767) }
        }).ToList() };
        Assert.Contains(new ConfigValidator().Validate(config, false).Issues, issue => issue.Path == "$");
    }

    [Fact]
    public void KnownLegacyNumericEnumRemainsReadable()
    {
        var config = new ShortcutConfig { Shortcuts = [new() { Name = "Legacy", Hotkey = new() { Key = "A" }, Target = new ApplicationTarget { LaunchMode = ApplicationLaunchMode.Uri, Value = "mailto:test@example.com" } }] };
        var json = ConfigJson.Serialize(config).Replace("\"launchMode\": \"uri\"", "\"launchMode\": 2", StringComparison.Ordinal);
        Assert.Equal(ApplicationLaunchMode.Uri, Assert.IsType<ApplicationTarget>(ConfigJson.Deserialize(json).Shortcuts.Single().Target).LaunchMode);
    }
}
