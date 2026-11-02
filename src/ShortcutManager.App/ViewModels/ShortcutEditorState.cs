using ShortcutManager.Core.Models;
using ShortcutManager.Core.Validation;

namespace ShortcutManager.App;

public sealed record TargetDraft(string Value = "", string Arguments = "", string WorkingDirectory = "", BatWindowMode WindowMode = BatWindowMode.Normal);
public sealed record EditorIssue(string Field, string Message);

// Framework-independent editor state: switching types/modes never discards another draft.
public sealed class ShortcutEditorState
{
    private readonly ShortcutConfig context;
    private readonly Dictionary<(int Kind, ApplicationLaunchMode Mode), TargetDraft> drafts = [];
    public Guid Id { get; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Kind { get; private set; }
    public ApplicationLaunchMode Mode { get; private set; } = ApplicationLaunchMode.Executable;
    public TargetDraft Current { get; set; } = new();
    public int OtherIssueCount { get; private set; }

    public ShortcutEditorState(ShortcutConfig context, ShortcutDefinition? original = null, int initialKind = 0)
    {
        this.context = context;
        Id = original?.Id ?? Guid.NewGuid();
        Kind = initialKind;
        if (original is null) return;
        Name = original.Name; Key = original.Hotkey.Key; Enabled = original.Enabled;
        switch (original.Target)
        {
            case WebsiteTarget target: Kind = 0; Current = new(target.Url); break;
            case ApplicationTarget target: Kind = 1; Mode = target.LaunchMode; Current = new(target.Value, target.Arguments, target.WorkingDirectory); break;
            case BatTarget target: Kind = 2; Current = new(target.Path, target.Arguments, target.WorkingDirectory, target.WindowMode); break;
            case ChromeTabGroupTarget target: Kind = 3; Current = new(target.GroupName); break;
        }
    }

    public void Switch(int kind, ApplicationLaunchMode? mode = null)
    {
        drafts[(Kind, Kind == 1 ? Mode : ApplicationLaunchMode.Executable)] = Current;
        Kind = kind;
        if (mode is not null) Mode = mode.Value;
        Current = drafts.GetValueOrDefault((Kind, Kind == 1 ? Mode : ApplicationLaunchMode.Executable)) ?? new();
    }

    public ShortcutDefinition Proposal() => new()
    {
        Id = Id, Name = Name.Trim(), Enabled = Enabled, Hotkey = new() { Key = Key },
        Target = Kind switch
        {
            0 => new WebsiteTarget { Url = Current.Value.Trim() },
            1 => new ApplicationTarget { LaunchMode = Mode, Value = Current.Value.Trim(), Arguments = Current.Arguments, WorkingDirectory = Current.WorkingDirectory.Trim() },
            2 => new BatTarget { Path = Current.Value.Trim(), Arguments = Current.Arguments, WorkingDirectory = Current.WorkingDirectory.Trim(), WindowMode = Current.WindowMode },
            3 => new ChromeTabGroupTarget { GroupName = Current.Value.Trim() },
            _ => throw new InvalidOperationException("起動対象の種類を選んでください。"),
        },
    };

    public IReadOnlyList<EditorIssue> Validate(bool checkFileSystem)
    {
        var entries = context.Shortcuts.Where(item => item.Id != Id).Append(Proposal()).ToList();
        var issues = new List<EditorIssue>(); OtherIssueCount = 0;
        var prefix = $"$.shortcuts[{entries.Count - 1}]";
        var validator = new ConfigValidator();
        // Other malformed drafts must not short-circuit validation of this entry.
        foreach (var issue in validator.Validate(context with { Shortcuts = [Proposal()] }, checkFileSystem).Issues)
            issues.Add(ToEditorIssue(issue));
        foreach (var issue in validator.Validate(context with { Shortcuts = entries }, checkFileSystem).Issues)
        {
            var otherPath = issue.Path?.StartsWith("$.shortcuts[", StringComparison.Ordinal) == true && !issue.Path.StartsWith(prefix + ".", StringComparison.Ordinal);
            if ((issue.ShortcutId is not null && issue.ShortcutId != Id) || otherPath) { OtherIssueCount++; continue; }
            issues.Add(ToEditorIssue(issue));
        }
        if (Enabled && context.Shortcuts.Any(item => item.Id != Id && item.Enabled && item.Hotkey?.Key == Key))
            issues.Add(new("key", $"Ctrl+Alt+{Key} は他の有効な割当で使用中です。別のキーを選ぶか、この割当を無効にしてください。"));
        return issues.Distinct().ToArray();
    }

    private static EditorIssue ToEditorIssue(ValidationIssue issue)
    {
        var message = issue.Path is not null && issue.Message.StartsWith(issue.Path + ": ", StringComparison.Ordinal) ? issue.Message[(issue.Path.Length + 2)..] : issue.Message;
        return new(FieldFor(issue), message);
    }

    private static string FieldFor(ValidationIssue issue) => issue.Code switch
    {
        "invalid-name" => "name",
        "invalid-key" or "invalid-modifiers" or "duplicate-hotkey" => "key",
        "relative-working-directory" or "missing-working-directory" => "directory",
        "invalid-extension-id" => "settings",
        "invalid-schema-version" or "invalid-dispatch-version" or "too-many-shortcuts" => "global",
        "invalid-structure" => issue.Path?.Contains(".name", StringComparison.Ordinal) == true ? "name" : issue.Path?.Contains(".hotkey", StringComparison.Ordinal) == true ? "key" : issue.Path?.Contains(".arguments", StringComparison.Ordinal) == true ? "arguments" : issue.Path?.Contains(".workingDirectory", StringComparison.Ordinal) == true ? "directory" : issue.Path?.Contains(".target", StringComparison.Ordinal) == true ? "target" : "global",
        _ => "target",
    };
}
