using System.Diagnostics;
using System.Text.Json;
using ShortcutManager.Windows.Shell;
using ShortcutManager.Windows.ShellLinks;

internal static class LatencySession
{
    private static string Root(string root)
    {
        var full = Path.GetFullPath(root);
        if (!Path.GetFileName(full).StartsWith("latency-", StringComparison.Ordinal)) throw new ArgumentException("Owned latency fixture required.");
        Directory.CreateDirectory(full); return full;
    }
    public static void Target(string directory)
    {
        var started = Stopwatch.GetTimestamp();
        var root = Root(directory);
        using var form = new System.Windows.Forms.Form { Text = "起動時間計測用の一時ウィンドウ", Width = 380, Height = 100 };
        using var close = new System.Windows.Forms.Timer { Interval = 500 };
        form.Shown += (_, _) => {
            File.WriteAllText(Path.Combine(root, "arrival-" + Guid.NewGuid().ToString("N") + ".json"), JsonSerializer.Serialize(new { started, ready = Stopwatch.GetTimestamp(), utc = DateTimeOffset.UtcNow }));
            close.Start();
        };
        close.Tick += (_, _) => form.Close();
        System.Windows.Forms.Application.Run(form);
    }
    public static void Session(string directory)
    {
        var root = Root(directory);
        var executable = Environment.ProcessPath!;
        var arrivals = new HashSet<string>();
        var samples = new List<object>();
        var direct = new List<object>();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var link = Path.Combine(desktop, "ShortcutManager-latency-" + Guid.NewGuid().ToString("N") + ".lnk");
        if (!Native.RegisterHotKey(0, 0x115, 3, 'U')) throw new InvalidOperationException("Ctrl+Alt+U is already registered; no changes made.");
        Native.UnregisterHotKey(0, 0x115);
        new ShellLinkService().Write(link, new(executable, $"latency-target \"{root}\"", root, "CTRL+ALT+U", executable, "Shortcut Manager isolated latency fixture"));
        var linkBytes = File.ReadAllBytes(link);
        new ShellChangeNotifier().NotifyItemCreated(link);
        using var form = new System.Windows.Forms.Form { Text = "HotKey起動遅延の計測（製品設定は変更しません）", Width = 700, Height = 240 };
        var label = new System.Windows.Forms.Label { Dock = System.Windows.Forms.DockStyle.Fill, Padding = new(20), Font = new("Yu Gothic UI", 12), Text = "直接起動の基準値を計測中です…" };
        form.Controls.Add(label);
        long pending = 0; bool held = false; bool ready = false; long nextDirect = Stopwatch.GetTimestamp(); int directCount = 0;
        var deadline = nextDirect + 300 * Stopwatch.Frequency;
        void Save() => File.WriteAllText(Path.Combine(root, "measurements.json"), JsonSerializer.Serialize(new { frequency = Stopwatch.Frequency, direct, samples }, new JsonSerializerOptions { WriteIndented = true }));
        using var timer = new System.Windows.Forms.Timer { Interval = 5 };
        timer.Tick += (_, _) => {
            var now = Stopwatch.GetTimestamp();
            if (now >= deadline) { File.WriteAllText(Path.Combine(root, "session-expired"), "Five minute test deadline reached."); form.Close(); return; }
            var down = (Native.GetAsyncKeyState('U') & 0x8000) != 0 && (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 && (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
            if (ready && down && !held && pending == 0) pending = now;
            held = down;
            if (!ready && pending == 0 && now >= nextDirect && directCount < 10)
            {
                pending = now;
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            }
            foreach (var path in Directory.GetFiles(root, "arrival-*.json"))
            {
                if (arrivals.Contains(path)) continue;
                JsonDocument data; try { data = JsonDocument.Parse(File.ReadAllText(path)); } catch (IOException) { continue; } catch (JsonException) { continue; }
                using (data)
                {
                    arrivals.Add(path);
                    if (pending == 0) continue;
                    var started = data.RootElement.GetProperty("started").GetInt64(); var shown = data.RootElement.GetProperty("ready").GetInt64();
                    var sample = new { pressed = pending, started, ready = shown, requestToStartMs = (started - pending) * 1000.0 / Stopwatch.Frequency, requestToReadyMs = (shown - pending) * 1000.0 / Stopwatch.Frequency, utc = DateTimeOffset.UtcNow };
                    if (ready) samples.Add(sample); else { direct.Add(sample); directCount++; nextDirect = now + Stopwatch.Frequency; }
                    pending = 0; Save();
                }
            }
            if (!ready && directCount == 10) { ready = true; File.WriteAllText(Path.Combine(root, "ready"), link); }
            if (pending != 0 && (now - pending) / (double)Stopwatch.Frequency > 12)
            {
                File.WriteAllText(Path.Combine(root, "timeout"), "No target arrived after selected key/request; do not treat as measured launch latency."); pending = 0;
            }
            if (ready) label.Text = $"Ctrl+Alt+U を1回押し、小窓が出て消えてから次を押してください。\n製品の22キーは触りません。計測済み: {samples.Count}/10\n10回で自動終了します。終了時に一時HotKeyを除去します。";
            if (samples.Count >= 10) form.Close();
        };
        form.Shown += (_, _) => timer.Start();
        try { System.Windows.Forms.Application.Run(form); }
        finally {
            Save();
            if (File.Exists(link) && File.ReadAllBytes(link).AsSpan().SequenceEqual(linkBytes)) { File.Delete(link); new ShellChangeNotifier().NotifyItemDeleted(link); }
        }
    }
}
