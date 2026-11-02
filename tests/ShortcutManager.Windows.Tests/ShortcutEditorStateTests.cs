using ShortcutManager.App;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Windows.Tests;

public sealed class ShortcutEditorStateTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),$"editor-draft-{Guid.NewGuid():N}");
    public ShortcutEditorStateTests() => Directory.CreateDirectory(root);
    private static ShortcutDefinition Website(string key="U") => new() {Name="Fixture",Hotkey=new() {Key=key},Target=new WebsiteTarget {Url="https://example.com"}};

    [Fact]
    public void SwitchingKindsAndApplicationModesRetainsIndependentDrafts()
    {
        var state=new ShortcutEditorState(new(),Website());
        state.Current=new("https://example.org");
        state.Switch(1,ApplicationLaunchMode.Executable); state.Current=new("C:\\My App\\run.exe","--profile work","C:\\My App");
        state.Switch(1,ApplicationLaunchMode.Uri); state.Current=new("mailto:test@example.com");
        state.Switch(0); Assert.Equal("https://example.org",state.Current.Value);
        state.Switch(1); Assert.Equal("mailto:test@example.com",state.Current.Value); Assert.Empty(state.Current.Arguments);
        state.Switch(1,ApplicationLaunchMode.Executable); Assert.Equal("--profile work",state.Current.Arguments); Assert.Equal("C:\\My App",state.Current.WorkingDirectory);
    }

    [Fact]
    public void LiveValidationMapsErrorsToFieldsAndDoesNotIgnoreInvalidCurrentDraftWhenOtherDraftIsMalformed()
    {
        var other=Website("J") with {Name=new string('x',81)};
        var state=new ShortcutEditorState(new() {Shortcuts=[other]},Website()); state.Name=""; state.Current=new("not a url");
        var issues=state.Validate(false);
        Assert.Contains(issues,item=>item.Field=="name"); Assert.Contains(issues,item=>item.Field=="target"); Assert.True(state.OtherIssueCount>0);
    }

    [Fact]
    public void EnabledDuplicateIsBlockedButDisabledMissingTargetCanBeKeptAsDraft()
    {
        var other=Website(); var selected=Website(); var state=new ShortcutEditorState(new() {Shortcuts=[other]},selected);
        Assert.Contains(state.Validate(false),issue=>issue.Field=="key");
        state.Enabled=false; state.Current=new(""); Assert.Empty(state.Validate(true));
        state.Enabled=true; Assert.Contains(state.Validate(false),issue=>issue.Field=="target");
    }

    [Fact]
    public void MissingFilesAndWorkingFolderAreReportedOnlyOnExplicitFilesystemValidation()
    {
        var state=new ShortcutEditorState(new(),Website()); state.Switch(1); state.Current=new(Path.Combine(root,"missing.exe"),WorkingDirectory:Path.Combine(root,"missing-folder"));
        Assert.Empty(state.Validate(false));
        var errors=state.Validate(true); Assert.Contains(errors,issue=>issue.Field=="target"); Assert.Contains(errors,issue=>issue.Field=="directory");
        File.WriteAllText(state.Current.Value,"fixture, not executable"); Directory.CreateDirectory(state.Current.WorkingDirectory); Assert.Empty(state.Validate(true));
    }

    [Fact]
    public void ChromeSettingsAndCapacityErrorsRemainDistinctFromFieldErrors()
    {
        var state=new ShortcutEditorState(new(),Website()); state.Switch(3); state.Current=new("Group");
        Assert.Contains(state.Validate(false),issue=>issue.Field=="settings");
        var entries=Enumerable.Range(0,26).Select(index=>Website(((char)('A'+index)).ToString())).ToList();
        var full=new ShortcutEditorState(new() {Shortcuts=entries},initialKind:0) {Name="new",Key="U",Current=new("https://example.com")};
        Assert.Contains(full.Validate(false),issue=>issue.Field=="global");
    }

    [Fact]
    public void LegacyArgumentsForUriArePreservedAndNeverTransferredIntoAnotherMode()
    {
        var original=Website() with {Target=new ApplicationTarget {LaunchMode=ApplicationLaunchMode.Uri,Value="mailto:test@example.com",Arguments="legacy value"}};
        var state=new ShortcutEditorState(new(),original); Assert.Equal("legacy value",((ApplicationTarget)state.Proposal().Target).Arguments);
        state.Switch(1,ApplicationLaunchMode.Executable); Assert.Empty(state.Current.Arguments);
        state.Switch(1,ApplicationLaunchMode.Uri); Assert.Equal("legacy value",state.Current.Arguments);
    }

    public void Dispose() => Directory.Delete(root,true);
}
