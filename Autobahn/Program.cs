namespace Autobahn;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\AntigravityAutobahn", out bool first);
        if (!first)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(@"Local\AntigravityAutobahn.Restore");
                foreach (var process in System.Diagnostics.Process.GetProcessesByName("AntigravityAutobahn"))
                {
                    using (process)
                        if (process.Id != Environment.ProcessId) Native.AllowSetForegroundWindow(process.Id);
                }
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Lang.Set(new Preferences().Language);
                MessageBox.Show(Lang.T("alreadyOpen"), "Antigravity Autobahn");
            }
            return;
        }
        using var restoreSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\AntigravityAutobahn.Restore");
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new MainForm(restoreSignal));
    }
}
