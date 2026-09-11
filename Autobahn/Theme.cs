using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Autobahn;

internal static class Theme
{
    internal static readonly Color Background = Color.FromArgb(9, 13, 22);
    internal static readonly Color Surface = Color.FromArgb(16, 23, 37);
    internal static readonly Color Border = Color.FromArgb(33, 46, 66);
    internal static readonly Color Blue = Color.FromArgb(56, 130, 255);
    internal static readonly Color Cyan = Color.FromArgb(99, 206, 255);
    internal static readonly Color Text = Color.FromArgb(233, 241, 255);
    internal static readonly Color Muted = Color.FromArgb(139, 158, 185);
    internal static readonly Color Red = Color.FromArgb(255, 103, 122);

    internal static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void FillRound(Graphics g, Color color, RectangleF rect, float radius)
    {
        using var path = Round(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    internal static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        FillRound(g, Blue, new RectangleF(0, 0, 32, 32), 8);
        using var pen = new Pen(Color.White, 2.5f);
        g.DrawLine(pen, 9, 26, 13, 6);
        g.DrawLine(pen, 23, 26, 19, 6);
        g.DrawLine(pen, 16, 9, 16, 13);
        g.DrawLine(pen, 16, 18, 16, 23);
        var handle = bitmap.GetHicon();
        using var borrowed = Icon.FromHandle(handle);
        var icon = (Icon)borrowed.Clone();
        DestroyIcon(handle);
        return icon;
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}

internal sealed class ActionButton : Button
{
    [DefaultValue(false)] internal bool Primary { get; set; }
    private bool hovered;
    public ActionButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        ForeColor = Theme.Text;
        BackColor = Theme.Background;
        Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Height = 44;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        Theme.FillRound(g, Primary ? (hovered ? Color.FromArgb(79, 151, 255) : Theme.Blue) :
            (hovered ? Color.FromArgb(30, 43, 64) : Theme.Surface), rect, 10);
        using var path = Theme.Round(rect, 10);
        using var pen = new Pen(Focused ? Theme.Cyan : Primary ? Theme.Blue : Theme.Border);
        g.DrawPath(pen, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class LanguageSelector : ComboBox
{
    public LanguageSelector()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        ItemHeight = 26;
    }

    protected override void OnHandleCreated(EventArgs e)
    { base.OnHandleCreated(e); ItemHeight = (int)(26 * DeviceDpi / 96f); }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(26, 48, 78) : Theme.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        string text = e.Index >= 0 ? Items[e.Index]?.ToString() ?? "" : Text;
        var bounds = e.Bounds;
        bounds.X += (int)(6 * DeviceDpi / 96f);
        TextRenderer.DrawText(e.Graphics, text, Font, bounds, Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
}

internal sealed class MetricCard : Control
{
    private string value = "0";
    [DefaultValue("")] internal string Caption { get; set; } = "";
    [DefaultValue("")] internal string Detail { get; set; } = "";
    [DefaultValue("0")] internal string Value { get => value; set { this.value = value; AccessibleName = Caption + ": " + value; Invalidate(); } }
    public MetricCard() { DoubleBuffered = true; BackColor = Theme.Background; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        Theme.FillRound(g, Theme.Surface, new RectangleF(0, 0, Width - 1, Height - 1), 12 * s);
        using var label = new Font("Segoe UI", 9);
        using var large = new Font("Segoe UI", 23, FontStyle.Bold);
        using var small = new Font("Segoe UI", 8);
        TextRenderer.DrawText(g, Caption, label, new Point((int)(18*s), (int)(12*s)), Theme.Muted);
        TextRenderer.DrawText(g, Value, large, new Point((int)(15*s), (int)(30*s)), Theme.Text);
        TextRenderer.DrawText(g, Detail, small, new Point((int)(18*s), (int)(78*s)), Theme.Muted);
    }
}

internal sealed class ActivityFeed : ListBox
{
    public ActivityFeed()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 72;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        IntegralHeight = false;
        AccessibleName = Lang.T("feed");
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    { base.OnDpiChangedAfterParent(e); ItemHeight = (int)(72 * DeviceDpi / 96f); }

    protected override void OnHandleCreated(EventArgs e)
    { base.OnHandleCreated(e); ItemHeight = (int)(72 * DeviceDpi / 96f); }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || Items[e.Index] is not ActivityEntry entry) return;
        float s = DeviceDpi / 96f;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(24, 37, 57) : Theme.Surface);
        g.FillRectangle(bg, e.Bounds);
        var (title, detail, glyph, accent) = Describe(entry);
        int x = e.Bounds.X, y = e.Bounds.Y;
        Theme.FillRound(g, Color.FromArgb(35, accent), new RectangleF(x + 14*s, y + 16*s, 36*s, 36*s), 11*s);
        using var bold = new Font("Segoe UI", 10, FontStyle.Bold);
        using var small = new Font("Segoe UI", 9);
        using var icon = new Font("Segoe UI Symbol", 13, FontStyle.Bold);
        TextRenderer.DrawText(g, glyph, icon, new Rectangle(x+(int)(14*s), y+(int)(16*s), (int)(36*s), (int)(36*s)), accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        int inset = (int)(64*s), timeWidth = (int)(120*s);
        TextRenderer.DrawText(g, title, bold, new Rectangle(x+inset, y+(int)(12*s), e.Bounds.Width-inset-timeWidth, (int)(24*s)), Theme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, detail, small, new Rectangle(x+inset, y+(int)(37*s), e.Bounds.Width-inset-(int)(20*s), (int)(22*s)), Theme.Muted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        string time = entry.Time.LocalDateTime.Date == DateTime.Today ? entry.Time.ToLocalTime().ToString("HH:mm:ss") : entry.Time.ToLocalTime().ToString("dd/MM HH:mm");
        TextRenderer.DrawText(g, time, small, new Rectangle(e.Bounds.Right-timeWidth, y+(int)(15*s), timeWidth-(int)(16*s), (int)(22*s)), Theme.Muted, TextFormatFlags.Right);
        using var line = new Pen(Theme.Border);
        g.DrawLine(line, x+inset, e.Bounds.Bottom-1, e.Bounds.Right-16*s, e.Bounds.Bottom-1);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }

    private static (string, string, string, Color) Describe(ActivityEntry e)
    {
        string detail = e.MessageKey is not null && Lang.Keys.Contains(e.MessageKey) ? Lang.T(e.MessageKey) + e.Message : e.Message;
        if (e.Kind == "submit") return (Lang.T("submitTitle"), string.IsNullOrWhiteSpace(detail) || detail == "Envoyé à Antigravity." ? Lang.T("submitDetail") : detail, "✓", Theme.Cyan);
        if (e.Kind == "enabled" || e.Message == "Auto-submit enabled.") return (Lang.T("enabledTitle"), Lang.T("enabledDetail"), "▶", Theme.Blue);
        if (e.Kind == "paused" || e.Message == "Auto-submit disabled.") return (Lang.T("pausedTitle"), Lang.T("pausedDetail"), "Ⅱ", Theme.Red);
        if (e.Kind == "error") return (Lang.T("errorTitle"), detail, "!", Theme.Red);
        if (e.Kind == "started" || e.Message.StartsWith("Started paused.")) return (Lang.T("startedTitle"), Lang.T("startedDetail"), "↗", Theme.Cyan);
        if (e.Kind == "stopped" || e.Message == "Stopped.") return (Lang.T("stoppedTitle"), Lang.T("stoppedDetail"), "•", Theme.Muted);
        return (Lang.T("infoTitle"), detail, "i", Theme.Muted);
    }
}
