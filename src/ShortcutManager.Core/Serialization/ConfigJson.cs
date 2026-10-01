using System.Text.Json;
using System.Text.Json.Serialization;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Serialization;

public static class ConfigJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(ShortcutConfig config) => JsonSerializer.Serialize(config, Options);
    public static ShortcutConfig Deserialize(string json) => JsonSerializer.Deserialize<ShortcutConfig>(json, Options)
        ?? throw new JsonException("Config JSON is empty.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

