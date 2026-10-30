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
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("shortcuts", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 26)
            throw new JsonException("$.shortcuts: ショートカットは最大26件です。");
        var config = JsonSerializer.Deserialize<ShortcutConfig>(json, Options) ?? throw new JsonException("Config JSON is empty.");
        var issues = Validation.ConfigStructure.Inspect(config);
        if (issues.Count > 0) throw new JsonException(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        foreach (var required in new[] { "schemaVersion", "dispatchProtocolVersion", "settings", "shortcuts" })
            if (!document.RootElement.TryGetProperty(required, out _)) throw new JsonException($"$.{required}: 必須の設定項目がありません。空設定として反映せず、バックアップから復旧してください。");
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
        // Preserve known legacy numeric values; ConfigStructure rejects undefined values.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

