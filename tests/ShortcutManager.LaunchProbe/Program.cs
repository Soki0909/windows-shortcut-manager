using System.Text.Json;

// No window, console, HotKey, registry access or persistent process. Only emit a
// new report inside the launch test's nonce directory, then exit immediately.
if (args.Length < 2 || args[0] != "--report") return 2;
var report = Path.GetFullPath(args[1]);
if (!Path.GetFileName(Path.GetDirectoryName(report)!).StartsWith("shortcut-manager-launch-", StringComparison.Ordinal)
    || !Path.GetFileName(report).StartsWith("report-", StringComparison.Ordinal)
    || !report.EndsWith(".json", StringComparison.Ordinal)) return 2;
using (var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    JsonSerializer.Serialize(stream, new { ProcessId = Environment.ProcessId, Arguments = args.Skip(2).ToArray(), WorkingDirectory = Environment.CurrentDirectory });
return 0;
