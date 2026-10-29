using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;
using ShortcutManager.Core.Validation;

namespace ShortcutManager.Core.Tests;

public sealed class ConfigSafetyTests
{
    [Theory]
    [InlineData("{\"settings\":null}", "$.settings")]
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
}
