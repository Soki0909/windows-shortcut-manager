using ShortcutManager.Windows.ShellLinks;
using ShortcutManager.Core.Models;
using System.Runtime.InteropServices;

namespace ShortcutManager.Windows.Tests;

public sealed class ShortcutReferenceGuardTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "link-reference-" + Guid.NewGuid().ToString("N"));
    private string FilePath(string name) => Path.Combine(root, name);
    public ShortcutReferenceGuardTests() => Directory.CreateDirectory(root);

    [Fact]
    public void CyclicSourceChainIsRejectedWithoutWriting()
    {
        var a = FilePath("a.lnk"); var b = FilePath("b.lnk");
        var error = Assert.Throws<InvalidDataException>(() => ShortcutReferenceGuard.EnsureAcyclic(a, new GraphLinks(new() { [a] = b, [b] = a })));
        Assert.Contains("循環", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourcesCannotReferenceAnyFileThatApplyWillReplace(bool indirect)
    {
        var managed = FilePath("managed"); var owned = Path.Combine(managed, "generated.lnk"); var source = FilePath("source.lnk");
        var graph = new GraphLinks(new() { [source] = owned });
        var error = Assert.Throws<InvalidDataException>(() => ShortcutReferenceGuard.EnsureAcyclic(indirect ? source : owned, graph, managedDirectory: managed));
        Assert.Contains("管理フォルダ", error.Message);
    }

    [Fact]
    public void SiblingDirectoryAndOpaqueShellTargetAreNotConfusedWithManagedFiles()
    {
        var source = Path.Combine(FilePath("managed-other"), "store.lnk");
        ShortcutReferenceGuard.EnsureAcyclic(source, new GraphLinks(new() { [source] = "" }), managedDirectory: FilePath("managed"));
    }

    [Fact]
    public void RelativeAndUnboundedChainsFailClosed()
    {
        var source = FilePath("source.lnk");
        Assert.Throws<InvalidDataException>(() => ShortcutReferenceGuard.EnsureAcyclic(source, new GraphLinks(new() { [source] = "relative.lnk" })));
        var graph = new Dictionary<string, string>();
        for (var i = 0; i < 17; i++) graph[FilePath(i + ".lnk")] = FilePath((i + 1) + ".lnk");
        var error = Assert.Throws<InvalidDataException>(() => ShortcutReferenceGuard.EnsureAcyclic(FilePath("0.lnk"), new GraphLinks(graph)));
        Assert.Contains("深すぎる", error.Message);
    }

    [Fact]
    public void NativeWriterRejectsDirectAndIndirectCyclesBeforeChangingEitherFile()
    {
        var a = FilePath("a.lnk"); var b = FilePath("b.lnk"); var links = new ShellLinkService();
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        links.Write(a, new(executable, "", root, "CTRL+ALT+Q", "", "original"));
        links.Write(b, new(a, "", root, "", "", "reference"));
        Assert.Equal(a, links.Read(b).TargetPath);
        var beforeA = File.ReadAllBytes(a); var beforeB = File.ReadAllBytes(b);
        Assert.Throws<InvalidDataException>(() => links.Write(a, new(a, "", root, "", "", "self")));
        Assert.Throws<InvalidDataException>(() => links.Write(a, new(b, "", root, "", "", "cycle")));
        Assert.Equal(beforeA, File.ReadAllBytes(a)); Assert.Equal(beforeB, File.ReadAllBytes(b));
        Assert.Equal("CTRL+ALT+Q", links.Read(a).Hotkey);
    }

    [Fact]
    public void MissingReferenceModeFailsComparisonAndInvalidRawReferenceIsRejectedByWindows()
    {
        var source = FilePath("source.lnk"); var wrapper = FilePath("wrapper.lnk"); var links = new ShellLinkService();
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        links.Write(source, new(executable, "", root, "CTRL+ALT+Q", "", "original"));
        links.Write(wrapper, new(source, "", root, "CTRL+ALT+J", "", "reference"));
        var expected = new LaunchSpec(source, "", root, "CTRL+ALT+J", "", "reference");
        var original = links.Read(wrapper);
        Assert.True(ShellLinkComparison.Matches(original, expected));
        var withoutMode = original with { IsLinkReference = false };
        Assert.False(ShellLinkComparison.Matches(withoutMode, expected));
        Assert.Contains(ShellLinkComparison.Differences(withoutMode, expected), difference => difference.Field == "元リンク参照");
        var bytes = File.ReadAllBytes(wrapper); var flags = BitConverter.ToUInt32(bytes, 20);
        BitConverter.TryWriteBytes(bytes.AsSpan(20, 4), flags & ~0x00800000u); File.WriteAllBytes(wrapper, bytes);
        // Windows rejects a link-to-link PIDL whose required flag was removed.
        // A read failure is also a failed stage/audit; do not weaken this to a
        // successful metadata read just to exercise the comparison branch.
        Assert.ThrowsAny<COMException>(() => links.Read(wrapper));
        Assert.Equal("CTRL+ALT+Q", links.Read(source).Hotkey);
    }

    private sealed class GraphLinks(Dictionary<string, string> targets) : IShellLinkService
    {
        public ShellLinkInfo Read(string path) => new(path, targets[path], "", "", "", "", "", 1);
        public void Write(string path, ShellLinkSpec spec) => throw new InvalidOperationException("Reference validation must not write.");
    }
    public void Dispose() => Directory.Delete(root, true);
}
