using System.Text.Json.Serialization;

namespace ShortcutManager.Core.Models;

public sealed record ShortcutConfig
{
    public int SchemaVersion { get; init; } = 1;
    public int DispatchProtocolVersion { get; init; } = 1;
    public ShortcutSettings Settings { get; init; } = new();
    public List<ShortcutDefinition> Shortcuts { get; init; } = [];
}

public sealed record ShortcutSettings
{
    public ChromeSettings Chrome { get; init; } = new();
    public string? ManagedShortcutDirectory { get; init; }
}

public sealed record ChromeSettings
{
    public string? ExecutablePath { get; init; }
    public string ExtensionId { get; init; } = string.Empty;
}

public sealed record ShortcutDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public HotkeyDefinition Hotkey { get; init; } = new();
    public ShortcutTarget Target { get; init; } = new WebsiteTarget();
}

public sealed record HotkeyDefinition
{
    public List<string> Modifiers { get; init; } = ["Ctrl", "Alt"];
    public string Key { get; init; } = string.Empty;

    [JsonIgnore]
    public string Canonical => $"CTRL+ALT+{Key.Trim().ToUpperInvariant()}";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WebsiteTarget), "website")]
[JsonDerivedType(typeof(ApplicationTarget), "application")]
[JsonDerivedType(typeof(BatTarget), "bat")]
[JsonDerivedType(typeof(ChromeTabGroupTarget), "chromeTabGroup")]
public abstract record ShortcutTarget;

public sealed record WebsiteTarget : ShortcutTarget
{
    public string Url { get; init; } = string.Empty;
}

public enum ApplicationLaunchMode { Executable, Shortcut, Uri }

public sealed record ApplicationTarget : ShortcutTarget
{
    public ApplicationLaunchMode LaunchMode { get; init; }
    public string Value { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
}

public enum BatWindowMode { Normal, Minimized }

public sealed record BatTarget : ShortcutTarget
{
    public string Path { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public BatWindowMode WindowMode { get; init; }
}

public sealed record ChromeTabGroupTarget : ShortcutTarget
{
    public string GroupName { get; init; } = string.Empty;
}

