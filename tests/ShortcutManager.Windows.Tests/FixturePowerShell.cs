using System.Diagnostics;
using System.Text;

namespace ShortcutManager.Windows.Tests;

// No SMB operations here. Callers must guard real runner-only fixture scripts.
internal static class FixturePowerShell
{
    internal static void Run(string script, string phase, int timeoutMilliseconds = 30000)
    {
        var wrapped = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; try { & { " + script
            + " }; exit 0 } catch { [Console]::Error.WriteLine($_.ToString()); [Console]::Error.WriteLine($_.ScriptStackTrace); exit 1 }";
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped)) }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000)) throw new TimeoutException($"Fixture PowerShell [{phase}] did not stop after timeout.");
            throw new TimeoutException($"Fixture PowerShell [{phase}] timed out. stdout: {output.GetAwaiter().GetResult()} stderr: {error.GetAwaiter().GetResult()}");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Fixture PowerShell [{phase}] exit={process.ExitCode}. stdout: {output.GetAwaiter().GetResult()} stderr: {error.GetAwaiter().GetResult()}");
    }

    internal static string RemoveOwnedShare(string share, string owner, string path)
    {
        static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        // Enumerate successfully, then filter. A missing -Name query can itself
        // leave EncodedCommand's implicit process status at 1 despite no stderr.
        var find = "@(Get-SmbShare -ErrorAction Stop | Where-Object Name -EQ " + Quote(share) + ")";
        return "$owned=" + find + "; if($owned.Count -gt 1){throw 'Ambiguous fixture share'}; "
            + "if($owned.Count -eq 1){if($owned[0].Description -cne " + Quote(owner) + " -or $owned[0].Path -ine " + Quote(path)
            + "){throw 'Fixture share ownership mismatch; not removed'}; Remove-SmbShare -Name " + Quote(share) + " -Force -ErrorAction Stop}; "
            + "if(" + find + ".Count -ne 0){throw 'Fixture share still present after cleanup'}";
    }

    internal static async Task WithCleanup(Func<Task> action, Action cleanup)
    {
        Exception? primary = null;
        try { await action(); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { cleanup(); }
            catch (Exception error) when (primary is not null)
            { throw new AggregateException("Fixture operation and cleanup both failed.", primary, error); }
        }
    }
}
