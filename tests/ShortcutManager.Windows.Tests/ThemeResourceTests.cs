using System.Diagnostics;

namespace ShortcutManager.Windows.Tests;

public sealed class ThemeResourceTests
{
    [Fact]
    public void ActualWpfStylesKeepReadableHoverAndUpdateExistingControlsAcrossThemes()
    {
        // Every isolated process must pass. This is not a retry that accepts
        // one success after failures, and no Application lives in the testhost.
        for(var attempt=0;attempt<3;attempt++)RunProbe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void TimedOutApplicationIsStoppedAndFreshProbeCanRun()
    {
        var error=Assert.Throws<TimeoutException>(()=>RunProbe(TimeSpan.FromMilliseconds(100),stallAfterApp:true));
        Assert.Contains("last phase=app.forced-stall",error.Message);
        Assert.Contains("owned process stopped=True",error.Message);
        RunProbe(TimeSpan.FromSeconds(30));
    }

    private static void RunProbe(TimeSpan timeout,bool stallAfterApp=false)
    {
        var repository=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var executable=Path.Combine(repository,"tests","ShortcutManager.ThemeProbe","bin",configuration,"net10.0-windows","ShortcutManager.ThemeProbe.exe");
        Assert.True(File.Exists(executable),"Theme probe must be built before the Windows tests.");
        var root=Path.Combine(Path.GetTempPath(),"shortcut-manager-theme-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var progress=Path.Combine(root,"progress.txt");
        var start=new ProcessStartInfo(executable) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("--progress");start.ArgumentList.Add(progress);if(stallAfterApp)start.ArgumentList.Add("--stall-after-app");
        using var process=Process.Start(start)!;
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        string Phase() {try{return File.ReadAllText(progress).Trim();}catch(IOException){return "not-reported";}}
        string Details()=>$"last phase={Phase()}; owned process stopped={process.HasExited}; stdout: {stdout.GetAwaiter().GetResult()}; stderr: {stderr.GetAwaiter().GetResult()}";
        try
        {
            if(stallAfterApp)
            {
                // Start the deliberate timeout only after Application is live.
                // Slow startup is not the condition this regression is testing.
                var readiness=Stopwatch.StartNew();
                while(!process.HasExited && Phase()!="app.forced-stall" && readiness.Elapsed<TimeSpan.FromSeconds(30))Thread.Sleep(25);
                if(!process.HasExited && Phase()!="app.forced-stall") {Stop(process);throw new TimeoutException("Theme probe did not reach forced stall; "+Details());}
            }
            if(!process.WaitForExit(timeout)) {Stop(process);throw new TimeoutException("WPF probe timed out; "+Details());}
            Assert.True(process.ExitCode==0,$"Theme probe exit={process.ExitCode}; "+Details());
            Assert.Equal("completed",Phase());
        }
        finally
        {
            Stop(process);
            // Retain evidence if termination itself failed. Never hide a live
            // child behind successful cleanup or contaminate the next test.
            if(process.HasExited)Directory.Delete(root,true);
        }
    }
    private static void Stop(Process process)
    {
        if(!process.HasExited)
        {
            try {process.Kill(entireProcessTree:true);}
            catch(InvalidOperationException) when(process.HasExited) { }
        }
        if(!process.WaitForExit(5000))throw new TimeoutException("Owned WPF probe could not be stopped; test evidence retained.");
    }
}
