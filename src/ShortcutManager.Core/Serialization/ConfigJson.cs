using System.Text.Json;
using System.Text.Json.Serialization;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Serialization;

public static class ConfigJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(ShortcutConfig config) => JsonSerializer.Serialize(config, Options);
    public static ShortcutConfig Deserialize(string json)
    {
        if (json.Length > Validation.ConfigStructure.MaxJsonCharacters) throw new JsonException("$: Config JSON exceeds 1 MiB characters.");
        var config = JsonSerializer.Deserialize<ShortcutConfig>(json, Options) ?? throw new JsonException("Config JSON is empty.");
        var issues = Validation.ConfigStructure.Inspect(config);
        if (issues.Count > 0) throw new JsonException(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        return config;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}

