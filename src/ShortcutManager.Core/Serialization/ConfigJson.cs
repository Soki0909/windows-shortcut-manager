using System.Text.Json;
using System.Text.Json.Serialization;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Core.Serialization;

public sealed class UnsupportedConfigVersionException(string message) : JsonException(message);

public static class ConfigJson
{
    public const int SupportedSchemaVersion = 1;
    public const int SupportedDispatchProtocolVersion = 1;
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(ShortcutConfig config) => JsonSerializer.Serialize(config, Options);
    public static ShortcutConfig Deserialize(string json)
    {
        if (json.Length > Validation.ConfigStructure.MaxJsonCharacters) throw new JsonException("$: Config JSON exceeds 1 MiB characters.");
        using var document = JsonDocument.Parse(json);
        RequireSupportedVersions(document.RootElement);
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

    // Check before polymorphic deserialization: future formats may use unknown
    // members/target types and must still be recognized as incompatible data.
    public static void RequireSupportedVersions(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return;
        foreach (var (name, supported) in new[] { ("schemaVersion", SupportedSchemaVersion), ("dispatchProtocolVersion", SupportedDispatchProtocolVersion) })
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                && (!value.TryGetInt32(out var version) || version != supported))
            {
                var declared = value.GetRawText();
                if (declared.Length > 32) declared = declared[..32] + "…";
                throw new UnsupportedConfigVersionException($"$.{name}: この版が対応する形式は{supported}、設定は{declared}です。対応する新版で開いてください。この版からの反映・初期化・復元は行いません。");
            }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            AllowOutOfOrderMetadataProperties = true,
            AllowDuplicateProperties = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        // Preserve known legacy numeric values; ConfigStructure rejects undefined values.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

