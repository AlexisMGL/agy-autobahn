using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace Autobahn;

public sealed record Submission(DateTimeOffset Time, string Window, string Button);

// All UI Automation calls run on a worker thread, never on the controls' UI thread.
public sealed class SubmitWatcher : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Func<IEnumerable<IntPtr>> windows;
    private readonly Dictionary<string, int> handled = new();
    private int enabled;
    private int generation;
    private string lastStatus = "";
    private DateTime lastAction = DateTime.MinValue;
    private Task? worker;
    public event Action<Submission>? Submitted;
    public event Action<string>? StatusChanged;
    public event Action<string>? Error;
    public bool Enabled => Volatile.Read(ref enabled) != 0;

    public SubmitWatcher(Func<IEnumerable<IntPtr>>? windowProvider = null)
        => windows = windowProvider ?? AntigravityWindows;

    public void SetEnabled(bool value)
    {
        Interlocked.Increment(ref generation);
        Volatile.Write(ref enabled, value ? 1 : 0);
    }

    public void Start() => worker ??= Task.Run(Run);

    public static bool IsSubmitLabel(string? name) => name is not null &&
        Regex.IsMatch(name.Trim(), @"^Submit(?:\s*[↵⏎↩])?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static IEnumerable<IntPtr> AntigravityWindows()
    {
        var processes = Process.GetProcessesByName("Antigravity");
        try
        {
            var ids = processes.Select(p => p.Id).ToHashSet();
            var result = new List<IntPtr>();
            Native.EnumWindows((handle, _) =>
            {
                Native.GetWindowThreadProcessId(handle, out int pid);
                if (ids.Contains(pid) && Native.IsWindowVisible(handle)) result.Add(handle);
                return true;
            }, IntPtr.Zero);
            return result;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private async Task Run()
    {
        while (!stop.IsCancellationRequested)
        {
            if (Enabled)
            {
                try { Scan(Volatile.Read(ref generation)); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                { SetStatus("Unable to inspect Antigravity: " + ex.Message); }
            }
            try { await Task.Delay(750, stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void Scan(int scanGeneration)
    {
        var seen = new HashSet<string>();
        bool anyWindow = false, accessible = false, anySubmit = false, incomplete = false;
        var buttonsCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
        foreach (var handle in windows())
        {
            if (!CanAct(scanGeneration)) return;
            anyWindow = true;
            try
            {
                var window = AutomationElement.FromHandle(handle);
                var buttons = window.FindAll(TreeScope.Descendants, buttonsCondition);
                accessible |= buttons.Count > 3;
                foreach (AutomationElement button in buttons)
                {
                    if (!CanAct(scanGeneration)) return;
                    if (!IsSubmitLabel(button.Current.Name)) continue;
                    anySubmit = true;
                    var key = handle + ":" + string.Join(".", button.GetRuntimeId());
                    // Keep a handled element latched even while temporarily disabled/offscreen.
                    seen.Add(key);
                    if (handled.ContainsKey(key)) { handled[key] = 0; continue; }
                    if (!button.Current.IsEnabled || button.Current.IsOffscreen) continue;
                    if (DateTime.UtcNow - lastAction < TimeSpan.FromSeconds(2)) continue;
                    if (!button.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
                    { SetStatus("Submit found, but Antigravity does not expose a click action."); continue; }
                    string title = window.Current.Name;
                    string label = button.Current.Name;
                    if (!CanAct(scanGeneration)) return;
                    // Latch before invoking: ambiguous provider failures must not cause repeated approvals.
                    handled[key] = 0;
                    lastAction = DateTime.UtcNow;
                    try
                    {
                        ((InvokePattern)pattern).Invoke();
                        Submitted?.Invoke(new Submission(DateTimeOffset.Now, title, label));
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        SetEnabled(false);
                        Error?.Invoke("Click outcome is uncertain; auto-submit paused. Check Antigravity. " + ex.Message);
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
            { incomplete = true; SetStatus("Antigravity is updating; waiting for its controls."); }
        }
        if (incomplete) return;
        // Require two complete scans without the element before considering it a new prompt.
        foreach (var key in handled.Keys.ToArray())
            if (!seen.Contains(key) && ++handled[key] >= 2) handled.Remove(key);
        SetStatus(!anyWindow ? "Waiting for Antigravity to open." : anySubmit ?
            "Watching Antigravity · Submit detected" : accessible ? "Watching Antigravity · waiting for Submit" :
            "Waiting for accessible controls. If this persists, restore the Antigravity window.");
    }

    private bool CanAct(int scanGeneration) => Enabled && !stop.IsCancellationRequested &&
        scanGeneration == Volatile.Read(ref generation);

    private void SetStatus(string value)
    {
        if (lastStatus == value) return;
        lastStatus = value;
        StatusChanged?.Invoke(value);
    }

    public void Dispose() { SetEnabled(false); stop.Cancel(); }
}

internal static class Native
{
    internal delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr handle, out int processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr handle, int id);
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(int processId);
}
