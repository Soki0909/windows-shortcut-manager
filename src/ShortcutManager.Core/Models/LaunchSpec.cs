namespace ShortcutManager.Core.Models;

public sealed record LaunchSpec(
    string TargetPath,
    string Arguments,
    string WorkingDirectory,
    string Hotkey,
    string IconLocation,
    string Description,
    int WindowStyle = 1);

public sealed record ValidationIssue(string Code, string Message, Guid? ShortcutId = null, string? Path = null);

public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

