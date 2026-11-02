using System.Text.Json;
using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.Core.Tests;

public sealed class ConfigPropertyOrderTests
{
    [Theory]
    [InlineData("\"kind\":\"website\",\"url\":\"https://example.com\"")]
    [InlineData("\"url\":\"https://example.com\",\"kind\":\"website\"")]
    [InlineData("\"value\":\"example:test\",\"kind\":\"application\",\"launchMode\":\"uri\"")]
    public void KindPositionDoesNotChangeMeaning(string properties)
    {
        var config = ConfigJson.Deserialize(Config(properties));
        var target = config.Shortcuts.Single().Target;
        if(target is WebsiteTarget website) Assert.Equal("https://example.com",website.Url);
        else Assert.Equal("example:test",Assert.IsType<ApplicationTarget>(target).Value);
    }

    [Theory]
    [InlineData("\"url\":\"https://example.com\",\"kind\":\"future\"")]
    [InlineData("\"kind\":\"website\",\"url\":\"https://example.com\",\"kind\":\"application\"")]
    [InlineData("\"kind\":\"website\",\"url\":\"https://example.com\",\"url\":\"https://example.org\"")]
    [InlineData("\"kind\":\"website\",\"url\":")]
    public void UnknownDuplicateAndMalformedPropertiesAreRejected(string properties) => Assert.ThrowsAny<JsonException>(()=>ConfigJson.Deserialize(Config(properties)));

    [Fact]
    public void FutureSchemaIsStillRecognizedBeforeUnknownKind() => Assert.Throws<UnsupportedConfigVersionException>(()=>ConfigJson.Deserialize(Config("\"url\":\"https://example.com\",\"kind\":\"future\"").Replace("\"schemaVersion\":1","\"schemaVersion\":2")));

    private static string Config(string target) => "{\"schemaVersion\":1,\"dispatchProtocolVersion\":1,\"settings\":{},\"shortcuts\":[{\"id\":\"55966bf8-d548-45fc-97f6-62300810a629\",\"name\":\"Order\",\"hotkey\":{\"key\":\"U\"},\"target\":{"+target+"}}]}";
}
