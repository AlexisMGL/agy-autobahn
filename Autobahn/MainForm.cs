using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Autobahn;

public sealed class MainForm : Form
{
    private readonly SubmitWatcher watcher;
    private readonly ActivityLog history;
    private readonly Preferences preferences;
    private readonly Dictionary<Control, string> translations = new();
    private readonly LanguageSelector languages = new() { Name = "languageSelector", Dock = DockStyle.Top, Margin = new Padding(0, 13, 12, 0) };
    private readonly RoadScene road = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 18) };
    private readonly Label state = MakeLabel("EN PAUSE", 10, true);
    private readonly Label status = MakeLabel("", 9);
    private readonly ActionButton toggle = new() { Name = "toggleAutomation", Primary = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 18, 0) };
    private readonly MetricCard session = new() { Caption = "CETTE SESSION", Detail = "Submits envoyés", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
    private readonly MetricCard total = new() { Caption = "AU COMPTEUR", Detail = "Depuis le premier trajet", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
    private readonly MetricCard last = new() { Caption = "DERNIER SUBMIT", Detail = "La route vous attend", Dock = DockStyle.Fill, Margin = new Padding(0) };
    private readonly ActivityFeed logs = new() { Dock = DockStyle.Fill, Margin = new Padding(0) };
    private readonly NotifyIcon tray;
    private readonly Icon appIcon = Theme.CreateIcon();
    private readonly ToolStripMenuItem trayToggle = new("Activer l’auto-submit");
    private readonly ToolStripMenuItem trayRestore = new();
    private readonly ToolStripMenuItem trayQuit = new();
    private readonly RegisteredWaitHandle? restoreWait;
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 1000 };
    private int sessionCount;
    private bool hideHintShown;
    private string scanStatus = "";

    public MainForm(EventWaitHandle? restoreSignal = null, SubmitWatcher? watcherOverride = null, ActivityLog? historyOverride = null)
    {
        SuspendLayout();
        watcher = watcherOverride ?? new SubmitWatcher();
        history = historyOverride ?? new ActivityLog();
        preferences = new Preferences(history.DirectoryPath);
        Lang.Set(preferences.Language);
        Text = "Antigravity Autobahn";
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(960, 840);
        MinimumSize = new Size(320, 230);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = appIcon;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 20, 28, 14), ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int h in new[] { 76, 258, 76, 116, 53 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
        header.RowCount = 1;
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        var brand = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
        brand.Controls.Add(MakeLabel("AUTOBAHN", 24, true));
        brand.Controls.Add(Bind(MakeLabel("", 10), "subtitle"));
        var hide = Bind(new ActionButton { Name = "hideToTray", Dock = DockStyle.Top, Height = 42, Margin = new Padding(0, 9, 0, 0) }, "hide");
        languages.Items.AddRange(["EN · English", "FR · Français", "DE · Deutsch"]);
        languages.SelectedIndex = Lang.Code switch { "fr" => 1, "de" => 2, _ => 0 };
        header.Controls.Add(brand, 0, 0);
        header.Controls.Add(languages, 1, 0);
        header.Controls.Add(hide, 2, 0);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(road, 0, 1);

        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 0, 20) };
        controls.RowCount = 1;
        controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 255));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        controls.Controls.Add(toggle, 0, 0);
        var description = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0, 3, 0, 0) };
        description.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        description.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        state.Dock = status.Dock = DockStyle.Fill;
        status.AutoEllipsis = true;
        description.Controls.Add(state, 0, 0);
        description.Controls.Add(status, 0, 1);
        controls.Controls.Add(description, 1, 0);
        layout.Controls.Add(controls, 0, 2);

        var metrics = new TableLayoutPanel { Name = "metrics", Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 0, 0, 12) };
        metrics.RowCount = 1;
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i=0; i<3; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        metrics.Controls.Add(session, 0, 0);
        metrics.Controls.Add(total, 1, 0);
        metrics.Controls.Add(last, 2, 0);
        layout.Controls.Add(metrics, 0, 3);

        var journalHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        journalHeader.RowCount = 1;
        journalHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        journalHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        journalHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        var journalTitle = Bind(MakeLabel("", 14, true), "journal");
        journalTitle.Margin = new Padding(0, 10, 0, 0);
        journalHeader.Controls.Add(journalTitle, 0, 0);
        var openLogs = Bind(new ActionButton { Dock = DockStyle.Top, Height = 36, Margin = new Padding(0, 4, 0, 0) }, "files");
        journalHeader.Controls.Add(openLogs, 1, 0);
        layout.Controls.Add(journalHeader, 0, 4);
        layout.Controls.Add(logs, 0, 5);
        var footer = Bind(MakeLabel("", 9), "footer");
        footer.Dock = DockStyle.Fill;
        footer.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(footer, 0, 6);
        Controls.Add(layout);

        var menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text };
        trayRestore.Click += (_, _) => RestoreWindow();
        menu.Items.Add(trayRestore);
        menu.Items.Add(trayToggle);
        menu.Items.Add(new ToolStripSeparator());
        trayQuit.Click += (_, _) => Close();
        menu.Items.Add(trayQuit);
        tray = new NotifyIcon { Icon = appIcon, Text = "Autobahn · en pause", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) RestoreWindow(); };
        tray.BalloonTipClicked += (_, _) => RestoreWindow();
        toggle.Click += (_, _) => SetEnabled(!watcher.Enabled);
        trayToggle.Click += (_, _) => SetEnabled(!watcher.Enabled);
        hide.Click += (_, _) => HideToTray();
        openLogs.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(history.DirectoryPath) { UseShellExecute = true }); }
            catch (Exception ex) { AddLog("error", ex.Message, key: "openError"); }
        };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        foreach (var entry in history.Recent.AsEnumerable().Reverse()) logs.Items.Add(entry);
        watcher.StatusChanged += value => OnUi(() => { scanStatus = value; RefreshState(); });
        watcher.Error += value => OnUi(() => { AddLog("error", value, key: "clickError"); RefreshState(); });
        watcher.Submitted += submission => OnUi(() =>
        {
            sessionCount++;
            road.ShowSubmission(sessionCount);
            AddLog("submit", string.IsNullOrWhiteSpace(submission.Window) ? "" : "Antigravity · " + submission.Window, submission.Time);
            RefreshState();
        });
        if (restoreSignal is not null)
            restoreWait = ThreadPool.RegisterWaitForSingleObject(restoreSignal, (_, _) => OnUi(RestoreWindow), null, Timeout.Infinite, false);
        Shown += (_, _) =>
        {
            if (!Native.RegisterHotKey(Handle, 1, 0x4003, (uint)Keys.F8))
                AddLog("info", "", key: "hotkeyToggle");
            if (!Native.RegisterHotKey(Handle, 2, 0x4003, (uint)Keys.F9))
                AddLog("info", "", key: "hotkeyRestore");
            AddLog("started", Lang.T("startedDetail"));
            watcher.Start();
        };
        refreshTimer.Tick += (_, _) => { if (Visible) RefreshState(); };
        refreshTimer.Start();
        languages.SelectedIndexChanged += (_, _) =>
        {
            string code = languages.SelectedIndex switch { 1 => "fr", 2 => "de", _ => "en" };
            bool saved = preferences.SaveLanguage(code);
            Lang.Set(code);
            ApplyLanguage();
            if (!saved) AddLog("error", "", key: "languageError");
        };
        ApplyLanguage();
        ResumeLayout(true);
        ConfigureResponsiveLayout(layout, header, controls, description, metrics, journalHeader, footer);
    }

    private void ConfigureResponsiveLayout(TableLayoutPanel layout, Control header,
        TableLayoutPanel controls, Control description, Control metrics, Control journalHeader, Control footer)
    {
        bool arranging = false;
        void Arrange()
        {
            if (arranging || WindowState == FormWindowState.Minimized) return;
            arranging = true;
            layout.SuspendLayout();
            controls.SuspendLayout();
            try
            {
                float scale = DeviceDpi / 96f;
                float width = ClientSize.Width / scale, height = ClientSize.Height / scale;
                bool full = width >= 740 && height >= 720;
                bool compact = width < 680 || height < 480;
                int Px(int value) => (int)Math.Round(value * scale);
                void Row(int index, float height, SizeType type = SizeType.Absolute)
                {
                    layout.RowStyles[index].SizeType = type;
                    layout.RowStyles[index].Height = type == SizeType.Absolute ? height * scale : height;
                }

                header.Visible = metrics.Visible = description.Visible = !compact;
                logs.Visible = journalHeader.Visible = footer.Visible = full;
                road.CompactView = compact;
                layout.Padding = compact ? new Padding(Px(10)) : new Padding(Px(28), Px(20), Px(28), Px(14));
                road.Margin = new Padding(0, 0, 0, Px(compact ? 10 : 18));
                controls.Margin = new Padding(0, 0, 0, Px(compact ? 0 : 20));
                toggle.Margin = new Padding(0, 0, Px(compact ? 0 : 18), 0);
                // The same button and watcher survive every layout change.
                controls.ColumnStyles[0].SizeType = compact ? SizeType.Percent : SizeType.Absolute;
                controls.ColumnStyles[0].Width = compact ? 100 : Px(255);
                controls.ColumnStyles[1].SizeType = SizeType.Percent;
                controls.ColumnStyles[1].Width = compact ? 0 : 100;
                Row(0, compact ? 0 : 76);
                Row(1, full ? 258 : 100, full ? SizeType.Absolute : SizeType.Percent);
                Row(2, compact ? 48 : 76);
                Row(3, compact ? 0 : 116);
                Row(4, full ? 53 : 0);
                Row(5, full ? 100 : 0, full ? SizeType.Percent : SizeType.Absolute);
                Row(6, full ? 38 : 0);
            }
            finally
            {
                controls.ResumeLayout(true);
                layout.ResumeLayout(true);
                arranging = false;
            }
        }
        ClientSizeChanged += (_, _) => Arrange();
        DpiChanged += (_, _) => { if (IsHandleCreated) BeginInvoke((Action)Arrange); };
        Arrange();
    }

    private T Bind<T>(T control, string key) where T : Control
    { translations.Add(control, key); control.Text = Lang.T(key); return control; }

    private void ApplyLanguage()
    {
        foreach (var (control, key) in translations) control.Text = Lang.T(key);
        languages.AccessibleName = Lang.T("language");
        trayRestore.Text = Lang.T("restore");
        trayQuit.Text = Lang.T("quit");
        session.Caption = Lang.T("session");
        session.Detail = Lang.T("sent");
        total.Caption = Lang.T("total");
        total.Detail = Lang.T("sinceStart");
        last.Caption = Lang.T("last");
        logs.AccessibleName = Lang.T("feed");
        road.AccessibleName = Lang.T(watcher.Enabled ? "carOn" : "carOff");
        road.Invalidate();
        logs.Invalidate();
        RefreshState();
    }

    private static Label MakeLabel(string text, float size, bool bold = false) => new()
    {
        Text = text, AutoSize = true, ForeColor = bold ? Theme.Text : Theme.Muted,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0)
    };

    private static string FriendlyStatus(string value)
    {
        if (value.Length == 0) return Lang.T("ready");
        if (value.Contains("Waiting for Antigravity to open")) return Lang.T("openApp");
        if (value.Contains("Submit detected")) return Lang.T("detected");
        if (value.Contains("waiting for Submit")) return Lang.T("watching");
        if (value.Contains("accessible controls")) return Lang.T("restoreApp");
        if (value.Contains("updating")) return Lang.T("updating");
        if (value.Contains("does not expose")) return Lang.T("noClick");
        return Lang.T("waitingApp");
    }

    private void SetEnabled(bool enabled)
    {
        watcher.SetEnabled(enabled);
        AddLog(enabled ? "enabled" : "paused", Lang.T(enabled ? "enabledDetail" : "pausedDetail"));
        RefreshState();
    }

    private void RefreshState()
    {
        bool active = watcher.Enabled;
        if (road.Running != active) road.Running = active;
        state.Text = Lang.T(active ? "stateOn" : "stateOff");
        state.ForeColor = active ? Theme.Cyan : Theme.Red;
        toggle.Text = trayToggle.Text = Lang.T(active ? "pause" : "enable");
        tray.Text = Lang.T(active ? "trayOn" : "trayOff");
        session.Value = sessionCount.ToString("N0", Lang.Culture);
        total.Value = history.Total.ToString("N0", Lang.Culture);
        last.Detail = history.LastSubmit is { } date ? date.LocalDateTime.ToString("dd MMMM yyyy", Lang.Culture) : Lang.T("roadAwaits");
        last.Value = history.LastSubmit is { } time ? time.LocalDateTime.ToString("HH:mm:ss") : "—";
        status.Text = history.Warning is not null ? Lang.T("historyError") : active ? FriendlyStatus(scanStatus) : Lang.T("pausedReady");
    }

    private void AddLog(string kind, string message, DateTimeOffset? time = null, string? key = null)
    {
        logs.Items.Insert(0, history.Add(kind, message, time, key));
        if (logs.Items.Count > 500) logs.Items.RemoveAt(logs.Items.Count - 1);
        logs.TopIndex = 0;
    }

    private void HideToTray()
    {
        Hide();
        if (hideHintShown) return;
        hideHintShown = true;
        tray.ShowBalloonTip(5000, Lang.T("hideTitle"), Lang.T("hideHint"), ToolTipIcon.Info);
    }

    private void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { }
    }

    private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312)
        {
            if (message.WParam == 1) SetEnabled(!watcher.Enabled);
            if (message.WParam == 2) RestoreWindow();
        }
        base.WndProc(ref message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        watcher.Dispose();
        restoreWait?.Unregister(null);
        refreshTimer.Dispose();
        Native.UnregisterHotKey(Handle, 1);
        Native.UnregisterHotKey(Handle, 2);
        tray.Visible = false;
        tray.Dispose();
        AddLog("stopped", Lang.T("stoppedDetail"));
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) appIcon.Dispose(); }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
