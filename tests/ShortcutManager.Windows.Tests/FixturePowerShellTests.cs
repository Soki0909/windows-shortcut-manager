namespace ShortcutManager.Windows.Tests;

public sealed class FixturePowerShellTests
{
    [Fact]
    public void MissingShareCleanupIsIdempotentWithoutCallingRemove()
    {
        FixturePowerShell.Run("function Get-SmbShare {} function Remove-SmbShare { throw 'must not delete' }; "
            + FixturePowerShell.RemoveOwnedShare("fixture", "owner", @"C:\fixture"), "already absent");
    }

    [Fact]
    public void HandledMissingItemDoesNotBecomeAnUnexplainedProcessFailure()
    {
        FixturePowerShell.Run("$missing=Get-Item -LiteralPath (Join-Path $env:TEMP ([guid]::NewGuid().ToString('N'))) -ErrorAction SilentlyContinue; if($missing){throw 'unexpected fixture'}", "handled absence");
    }

    [Theory]
    [InlineData("other", @"C:\fixture")]
    [InlineData("owner", @"C:\other")]
    public void CleanupRefusesMismatchedOwnerOrPath(string owner, string path)
    {
        var mock = "function Get-SmbShare { [pscustomobject]@{Name='fixture';Description='" + owner + "';Path='" + path
            + "'} }; function Remove-SmbShare { throw 'must not delete' }; ";
        var error = Assert.Throws<InvalidOperationException>(() => FixturePowerShell.Run(mock
            + FixturePowerShell.RemoveOwnedShare("fixture", "owner", @"C:\fixture"), "ownership"));
        Assert.Contains("ownership mismatch", error.Message);
        Assert.DoesNotContain("must not delete", error.Message);
    }

    [Fact]
    public void CleanupVerifiesRemovalAndDoesNotAcceptSuccessfulNoOp()
    {
        var error = Assert.Throws<InvalidOperationException>(() => FixturePowerShell.Run(
            @"function Get-SmbShare { [pscustomobject]@{Name='fixture';Description='owner';Path='C:\fixture'} }; function Remove-SmbShare {}; "
            + FixturePowerShell.RemoveOwnedShare("fixture", "owner", @"C:\fixture"), "postcondition"));
        Assert.Contains("still present", error.Message);
        Assert.Contains("exit=1", error.Message);
        Assert.Contains("postcondition", error.Message);
    }

    [Fact]
    public void OwnedShareRemovalIsVerified()
    {
        FixturePowerShell.Run(@"$script:present=$true; function Get-SmbShare { if($script:present){[pscustomobject]@{Name='fixture';Description='owner';Path='C:\fixture'}} }; "
            + "function Remove-SmbShare { $script:present=$false }; "
            + FixturePowerShell.RemoveOwnedShare("fixture", "owner", @"C:\fixture"), "owned removal");
    }

    [Fact]
    public void CommandReportsPhaseAndFailureEvenWithPriorOutput()
    {
        var error = Assert.Throws<InvalidOperationException>(() => FixturePowerShell.Run("'fixture output'; throw 'fixture failure'", "create"));
        Assert.Contains("[create] exit=1", error.Message);
        Assert.Contains("fixture output", error.Message);
        Assert.Contains("fixture failure", error.Message);
    }

    [Fact]
    public async Task CleanupCannotHideOriginalLaunchFailure()
    {
        var primary = new InvalidOperationException("launch failed"); var cleanup = new IOException("cleanup failed");
        var error = await Assert.ThrowsAsync<AggregateException>(() => FixturePowerShell.WithCleanup(() => Task.FromException(primary), () => throw cleanup));
        Assert.Same(primary, error.InnerExceptions[0]); Assert.Same(cleanup, error.InnerExceptions[1]);
    }
}
