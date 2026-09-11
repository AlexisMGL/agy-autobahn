using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Autobahn;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--host") { RunHost(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--visual") { RunVisualCheck(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--counter") { RunCounterCheck(args[1]); return 0; }
        if (args.Length == 1 && args[0] == "--languages") { RunLanguageTests(); return 0; }
        if (args.Length == 3 && args[0] == "--read-language") return new Preferences(args[1]).Language == args[2] ? 0 : 1;
        try { RunTests().GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static async Task RunTests()
    {
        foreach (var name in new[] { "Submit", "Submit ↵", " Submit ⏎ ", "Submit↩" })
            Require(SubmitWatcher.IsSubmitLabel(name), "Recognizes " + name);
        foreach (var name in new string?[] { null, "", "Submit feedback", "Submit command", "Allow", "Send", "Do not Submit" })
            Require(!SubmitWatcher.IsSubmitLabel(name), "Rejects unrelated label " + name);

        string folder = Path.Combine(Path.GetTempPath(), "AutobahnTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        using var host = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
        {
            ArgumentList = { "--host", folder }, UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        })!;
        try
        {
            await Until(() => File.Exists(Path.Combine(folder, "ready")), "test window ready");
            var handle = new IntPtr(long.Parse(File.ReadAllText(Path.Combine(folder, "ready"))));
            int submitted = 0;
            using var watcher = new SubmitWatcher(() => new[] { handle });
            watcher.Submitted += _ => Interlocked.Increment(ref submitted);
            watcher.Error += error => Console.Error.WriteLine(error);
            watcher.Start();
            await Task.Delay(1800);
            Require(Clicks(folder) == 0, "Paused startup never clicks");
            var foregroundBefore = GetForegroundWindow();
            Require(foregroundBefore != handle, "Test target is in the background");
            watcher.SetEnabled(true);
            await Until(() => Volatile.Read(ref submitted) == 1 && Clicks(folder) == 1, "background UIA invocation");
            var foregroundAfter = GetForegroundWindow();
            Console.WriteLine(foregroundAfter == foregroundBefore
                ? "INFO Provider preserved the foreground window."
                : $"INFO Provider/user changed foreground (before={foregroundBefore}, after={foregroundAfter}, target={handle}). Foreground preservation is provider-dependent.");
            await Task.Delay(3200);
            Require(Clicks(folder) == 1, "Persistent button is clicked only once");
            watcher.SetEnabled(false);
            watcher.SetEnabled(true);
            await Task.Delay(2200);
            Require(Clicks(folder) == 1, "Toggling does not resubmit a handled prompt");
            await Command(folder, "hide");
            await Task.Delay(2200);
            await Command(folder, "show-disabled");
            await Task.Delay(2200);
            Require(Clicks(folder) == 1, "Disabled Submit is ignored");
            await Command(folder, "enable");
            await Until(() => Volatile.Read(ref submitted) == 2 && Clicks(folder) == 2, "new prompt after disappearance");
            watcher.SetEnabled(false);
            await Command(folder, "hide");
            await Task.Delay(1800);
            await Command(folder, "show");
            await Task.Delay(2200);
            Require(Clicks(folder) == 2, "Disabling stops further clicks");
            Require(!File.Exists(Path.Combine(folder, "decoy-clicked")), "Unrelated and disabled controls never clicked");
            Console.WriteLine("All automation integration tests passed. No real Antigravity controls were invoked.");
        }
        finally
        {
            if (!host.HasExited)
            {
                File.WriteAllText(Path.Combine(folder, "command"), "exit");
                if (!host.WaitForExit(3000)) host.Kill();
            }
        }
    }

    private static int Clicks(string folder)
    {
        string path = Path.Combine(folder, "clicks");
        return File.Exists(path) ? File.ReadAllLines(path).Length : 0;
    }

    private static async Task Command(string folder, string command)
    {
        File.WriteAllText(Path.Combine(folder, "command"), command);
        await Until(() => !File.Exists(Path.Combine(folder, "command")), "host command " + command);
    }

    private static async Task Until(Func<bool> condition, string description)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(12)) throw new Exception("Timed out: " + description);
            await Task.Delay(100);
        }
        Console.WriteLine("PASS " + description);
    }

    private static void Require(bool value, string message)
    { if (!value) throw new Exception("FAIL " + message); Console.WriteLine("PASS " + message); }

    private static void RunHost(string folder)
    {
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        using var form = new PassiveForm { Text = "Autobahn automation test fixture", ClientSize = new Size(420, 170), ShowInTaskbar = false };
        var submit = new Button { Text = "Submit ↵", Location = new Point(15, 15), Size = new Size(140, 35) };
        var decoy = new Button { Text = "Submit feedback", Location = new Point(15, 65), Size = new Size(140, 35) };
        var disabled = new Button { Text = "Submit", Enabled = false, Location = new Point(175, 15), Size = new Size(140, 35) };
        form.Controls.AddRange([submit, decoy, disabled]);
        submit.Click += (_, _) => File.AppendAllText(Path.Combine(folder, "clicks"), "click\n");
        decoy.Click += (_, _) => File.WriteAllText(Path.Combine(folder, "decoy-clicked"), "yes");
        disabled.Click += (_, _) => File.WriteAllText(Path.Combine(folder, "decoy-clicked"), "yes");
        using var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            string path = Path.Combine(folder, "command");
            if (!File.Exists(path)) return;
            string command = File.ReadAllText(path);
            switch (command)
            {
                case "hide": form.Controls.Remove(submit); break;
                case "show-disabled": submit.Enabled = false; form.Controls.Add(submit); break;
                case "show": submit.Enabled = true; form.Controls.Add(submit); break;
                case "enable": submit.Enabled = true; break;
                case "exit": form.Close(); break;
            }
            File.Delete(path);
        };
        form.Shown += (_, _) => { File.WriteAllText(Path.Combine(folder, "ready"), form.Handle.ToInt64().ToString()); timer.Start(); };
        System.Windows.Forms.Application.Run(form);
        submit.Dispose();
    }

    private sealed class PassiveForm : Form { protected override bool ShowWithoutActivation => true; }

    private static void RunVisualCheck(string folder)
    {
        Directory.CreateDirectory(folder);
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset);
        using var watcher = new SubmitWatcher(() => Array.Empty<IntPtr>());
        var history = new ActivityLog(Path.Combine(folder, Guid.NewGuid().ToString("N")));
        history.Add("submit", "Antigravity · fenêtre de test", DateTimeOffset.Now.AddMinutes(-2));
        using var form = new MainForm(signal, watcher, history);
        using var timer = new System.Windows.Forms.Timer { Interval = 1600 };
        int step = 0;
        IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
        void Capture(string name)
        {
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(Path.Combine(folder, name + ".png"));
            Console.WriteLine("Captured " + name);
        }
        timer.Tick += (_, _) =>
        {
            var controls = Descendants(form).ToList();
            var road = controls.OfType<RoadScene>().Single();
            switch (step++)
            {
                case 0:
                    Require(!watcher.Enabled && !road.Running, "UI starts paused");
                    Require(Lang.Code == "en", "First launch defaults to English");
                    Capture("paused");
                    controls.OfType<Button>().Single(b => b.Name == "toggleAutomation").PerformClick();
                    Require(watcher.Enabled && road.Running, "Enable starts animation and watcher");
                    break;
                case 1:
                    Capture("running");
                    controls.OfType<ComboBox>().Single().SelectedIndex = 1;
                    Require(Lang.Code == "fr" && watcher.Enabled, "Switch to French preserves active automation");
                    Require(controls.OfType<Button>().Single(b => b.Name == "toggleAutomation").Text == "Mettre en pause", "French labels update immediately");
                    break;
                case 2:
                    Capture("running-next-frame");
                    controls.OfType<ComboBox>().Single().SelectedIndex = 2;
                    Require(Lang.Code == "de" && watcher.Enabled, "Switch to German preserves active automation");
                    Require(controls.OfType<Button>().Single(b => b.Name == "toggleAutomation").Text == "Auto-Submit pausieren", "German labels update immediately");
                    controls.OfType<Button>().Single(b => b.Name == "toggleAutomation").PerformClick();
                    Require(!watcher.Enabled && !road.Running, "Pause immediately stops watcher and changes signal");
                    break;
                case 3:
                    Capture("stopped");
                    controls.OfType<Button>().Single(b => b.Name == "hideToTray").PerformClick();
                    Require(!form.Visible, "Hide to tray hides window");
                    signal.Set();
                    break;
                case 4:
                    Require(form.Visible && !watcher.Enabled, "Restore signal reopens window without enabling automation");
                    form.Hide();
                    PostMessage(form.Handle, 0x0312, new IntPtr(2), IntPtr.Zero);
                    break;
                case 5:
                    Require(form.Visible && !watcher.Enabled, "Restore hotkey handler reopens window without enabling automation");
                    form.Size = form.MinimumSize;
                    break;
                case 6:
                    Capture("minimum-size");
                    Require(road.CompactView && road.Visible, "Minimum size keeps compact car visible");
                    Require(!controls.OfType<ListBox>().Single().Visible && !controls.OfType<ComboBox>().Single().Visible, "Compact mode hides logs and header");
                    var compactButton = controls.OfType<Button>().Single(b => b.Name == "toggleAutomation");
                    Require(compactButton.Visible && compactButton.Height >= 40, "Compact action remains visible and usable");
                    foreach (var essential in new Control[] { road, compactButton })
                    {
                        var bounds = form.RectangleToClient(essential.RectangleToScreen(essential.ClientRectangle));
                        Require(form.ClientRectangle.Contains(bounds), "Compact control fits entirely inside the window: " + essential.GetType().Name);
                    }
                    Require(controls.Where(c => c.Visible && c.Controls.Count == 0).All(c => c == road || c == compactButton), "Only car and action are visible in minimal mode");
                    compactButton.PerformClick();
                    Require(watcher.Enabled && road.Running, "Compact action starts automation");
                    break;
                case 7:
                    Capture("compact-running");
                    controls.OfType<Button>().Single(b => b.Name == "toggleAutomation").PerformClick();
                    Require(!watcher.Enabled && !road.Running, "Compact pause stops automation immediately");
                    float scale = form.DeviceDpi / 96f;
                    form.ClientSize = new Size((int)(720 * scale), (int)(580 * scale));
                    break;
                case 8:
                    Capture("dashboard-size");
                    Require(!road.CompactView && controls.Single(c => c.Name == "metrics").Visible, "Medium layout restores counters");
                    Require(!controls.OfType<ListBox>().Single().Visible, "Medium layout keeps logs hidden");
                    float fullScale = form.DeviceDpi / 96f;
                    form.ClientSize = new Size((int)(960 * fullScale), (int)(840 * fullScale));
                    break;
                case 9:
                    Capture("expanded-again");
                    Require(controls.OfType<ListBox>().Single().Visible && controls.OfType<ComboBox>().Single().Visible, "Expanding restores logs and language selector");
                    Require(!watcher.Enabled, "Resizing preserves paused state");
                    form.Close();
                    Console.WriteLine("Visual and window controls check passed.");
                    break;
            }
        };
        form.Shown += (_, _) => timer.Start();
        System.Windows.Forms.Application.Run(form);
        using var restarted = new MainForm(null, new SubmitWatcher(() => Array.Empty<IntPtr>()), new ActivityLog(history.DirectoryPath));
        Require(Lang.Code == "de" && Descendants(restarted).OfType<ComboBox>().Single().SelectedIndex == 2, "Next window startup restores saved German preference");
        Require(new ActivityLog(history.DirectoryPath).Total == 1, "Language changes preserve submission history");
    }

    private static void RunCounterCheck(string folder)
    {
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Directory.CreateDirectory(folder);
        string fixture = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        using var host = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
        {
            ArgumentList = { "--host", fixture }, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        })!;
        try
        {
            var startup = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(fixture, "ready")))
            {
                if (startup.Elapsed.TotalSeconds > 10) throw new Exception("Counter fixture failed to start");
                Thread.Sleep(50);
            }
            var handle = new IntPtr(long.Parse(File.ReadAllText(Path.Combine(fixture, "ready"))));
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            System.Windows.Forms.Application.EnableVisualStyles();
            using var watcher = new SubmitWatcher(() => new[] { handle });
            var history = new ActivityLog(Path.Combine(fixture, "history"));
            using var form = new MainForm(null, watcher, history);
            using var timer = new System.Windows.Forms.Timer { Interval = 100 };
            IEnumerable<Control> Children(Control c) => c.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Children(child)));
            var road = Children(form).OfType<RoadScene>().Single();
            var button = Children(form).OfType<Button>().Single(b => b.Name == "toggleAutomation");
            void Capture(string name)
            {
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(Path.Combine(folder, name + ".png"));
            }
            int step = 0;
            var stage = Stopwatch.StartNew();
            var timeout = Stopwatch.StartNew();
            timer.Tick += (_, _) =>
            {
                if (timeout.Elapsed.TotalSeconds > 20) throw new Exception("Counter test timed out");
                if (step == 0 && road.SubmitCount == 1)
                {
                    Require(history.Total == 1 && Clicks(fixture) == 1, "One real test invocation increments both counters once");
                    Capture("submit-plus-one");
                    stage.Restart(); step++;
                }
                else if (step == 1 && stage.Elapsed.TotalSeconds > 1.6)
                {
                    Require(road.SubmitCount == 1, "Animation frames never increment the counter");
                    Capture("counter-settled");
                    form.Hide();
                    File.WriteAllText(Path.Combine(fixture, "command"), "hide");
                    stage.Restart(); step++;
                }
                else if (step == 2 && stage.Elapsed.TotalSeconds > 2.4)
                {
                    File.WriteAllText(Path.Combine(fixture, "command"), "show");
                    step++;
                }
                else if (step == 3 && road.SubmitCount == 2)
                {
                    Require(history.Total == 2 && Clicks(fixture) >= 2, "Second Submit updates compact counter while window is hidden");
                    form.Show();
                    button.PerformClick();
                    Require(!watcher.Enabled && road.SubmitCount == 2, "Pausing keeps the submission count");
                    Capture("second-submit-paused");
                    step++;
                    form.Close();
                    Console.WriteLine("Counter integration check passed; only test buttons were invoked.");
                }
            };
            form.Shown += (_, _) =>
            {
                form.Size = form.MinimumSize;
                Require(road.SubmitCount == 0, "New session starts at zero");
                Capture("counter-zero");
                button.PerformClick();
                timer.Start();
            };
            System.Windows.Forms.Application.Run(form);
            Require(step == 4, "Counter check completed all stages");
        }
        finally
        {
            if (!host.HasExited)
            {
                File.WriteAllText(Path.Combine(fixture, "command"), "exit");
                if (!host.WaitForExit(3000)) host.Kill();
            }
        }
    }

    private static void RunLanguageTests()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AutobahnLanguageTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Require(new Preferences(folder).Language == "en", "Missing preferences default to English");
        foreach (var code in new[] { "fr", "de", "en" })
        {
            Lang.Set(code);
            Require(Lang.Keys.All(key => !string.IsNullOrWhiteSpace(Lang.T(key))), "Complete translations: " + code);
            Require(new Preferences(folder).SaveLanguage(code), "Preference saves: " + code);
            using var nextBoot = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                ArgumentList = { "--read-language", folder, code }, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            })!;
            Require(nextBoot.WaitForExit(5000) && nextBoot.ExitCode == 0, "Fresh process restores: " + code);
        }
        File.WriteAllText(Path.Combine(folder, "settings.json"), "{ incomplete");
        Require(new Preferences(folder).Language == "en", "Damaged settings fall back to English");
        File.WriteAllText(Path.Combine(folder, "settings.json"), "{\"Language\":\"es\"}");
        Require(new Preferences(folder).Language == "en", "Unsupported saved language falls back to English");
        Console.WriteLine("All language preference tests passed.");
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
