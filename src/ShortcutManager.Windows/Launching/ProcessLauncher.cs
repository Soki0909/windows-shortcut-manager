using System.Diagnostics;
using ShortcutManager.Core.Models;

namespace ShortcutManager.Windows.Launching;

public sealed class ProcessLauncher
{
    public void Launch(LaunchSpec spec)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = spec.TargetPath,
            Arguments = spec.Arguments,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = true,
            WindowStyle = spec.WindowStyle == 7 ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
        });
    }
}

