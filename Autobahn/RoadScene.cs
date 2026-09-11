using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace Autobahn;

// Procedural illustration: no image assets, network requests, or rendering packages.
public sealed class RoadScene : Control
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double previous;
    private float speed, offset, carX = 578, wheelAngle, pulse;
    private bool running;
    private bool compactView;
    private readonly List<double> submissionBursts = new();
    private long submitCount;
    [Browsable(false)]
    public long SubmitCount => submitCount;

    // Called only after a successful Submit invocation; repainting never increments it.
    public void ShowSubmission(long count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count > submitCount)
        {
            submissionBursts.Add(clock.Elapsed.TotalSeconds);
            if (submissionBursts.Count > 8) submissionBursts.RemoveAt(0);
        }
        submitCount = count;
        previous = clock.Elapsed.TotalSeconds;
        timer.Enabled = Visible;
        Invalidate();
    }
    [DefaultValue(false)]
    public bool CompactView
    {
        get => compactView;
        set { if (compactView != value) { compactView = value; Invalidate(); } }
    }
    [DefaultValue(false)]
    public bool Running
    {
        get => running;
        set
        {
            running = value;
            AccessibleName = Lang.T(value ? "carOn" : "carOff");
            previous = clock.Elapsed.TotalSeconds;
            timer.Enabled = Visible;
            Invalidate();
        }
    }

    public RoadScene()
    {
        DoubleBuffered = true;
        BackColor = Theme.Background;
        timer.Tick += (_, _) =>
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Clamp(now - previous, 0, 0.05);
            previous = now;
            speed += ((running ? 1 : 0) - speed) * Math.Min(1, dt * 3.5f);
            carX += ((running ? 280 : 578) - carX) * Math.Min(1, dt * 2.8f);
            offset = (offset + dt * speed * 460) % 1800;
            wheelAngle = (wheelAngle + dt * speed * 1100) % 360;
            pulse += dt;
            bool hadBursts = submissionBursts.Count > 0;
            submissionBursts.RemoveAll(start => now - start >= 1.2);
            if (running || speed > .001f || Math.Abs(carX - 578) > .1f || hadBursts) Invalidate();
            else timer.Stop();
        };
    }

    protected override void OnVisibleChanged(EventArgs e)
    { base.OnVisibleChanged(e); previous = clock.Elapsed.TotalSeconds; timer.Enabled = Visible; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (compactView) { DrawCompact(g); return; }
        g.ScaleTransform(Width / 920f, Height / 260f);
        using var clip = Theme.Round(new RectangleF(0, 0, 919, 259), 18);
        g.SetClip(clip);
        using var sky = new LinearGradientBrush(new Rectangle(0, 0, 920, 260), Color.FromArgb(13, 30, 56), Color.FromArgb(7, 12, 23), 90);
        g.FillRectangle(sky, 0, 0, 920, 260);
        using var stars = new SolidBrush(Color.FromArgb(70, 132, 185));
        for (int i = 0; i < 34; i++)
        {
            float x = ((i * 157 + 23 - offset*.035f) % 920 + 920) % 920;
            g.FillEllipse(stars, x, 14 + i*43%102, i%3 == 0 ? 2 : 1, i%3 == 0 ? 2 : 1);
        }
        using var hills = new SolidBrush(Color.FromArgb(16, 37, 61));
        g.FillPolygon(hills, [new(0,155), new(0,119), new(85,85), new(182,127), new(300,74), new(407,127), new(521,97), new(672,130), new(801,88), new(920,122), new(920,155)]);
        using var city = new SolidBrush(Color.FromArgb(11, 25, 43));
        using var window = new SolidBrush(Color.FromArgb(48, 84, 113));
        for (int i = -1; i < 22; i++)
        {
            float x = i*53 - offset*.12f%53;
            int h = 20 + ((i+22)*31)%46;
            g.FillRectangle(city, x, 151-h, 31, h);
            for (int j=0; j<3; j++) g.FillRectangle(window, x+7+j*7, 158-h, 2, 4);
        }
        using var road = new SolidBrush(Color.FromArgb(24, 32, 46));
        g.FillRectangle(road, 0, 157, 920, 103);
        using var edge = new Pen(Color.FromArgb(69, 103, 134), 2);
        g.DrawLine(edge, 0, 162, 920, 162);
        g.DrawLine(edge, 0, 250, 920, 250);
        using var lane = new Pen(Color.FromArgb(111, 142, 166), 3);
        for (int i=-1; i<13; i++)
        {
            float x = i*100-offset%100;
            g.DrawLine(lane, x, 225, x+49, 225);
        }
        using var rail = new Pen(Color.FromArgb(40, 69, 95), 2);
        g.DrawLine(rail, 0, 147, 920, 147);
        for (int i=-1; i<17; i++)
        {
            float x = i*65-offset*.8f%65;
            g.DrawLine(rail, x, 146, x, 157);
        }
        // The fixed stop line keeps the stopped car visibly behind the red light.
        using var stopLine = new Pen(Color.FromArgb(running ? 45 : 135, 164, 188, 214), 5);
        g.DrawLine(stopLine, 737, 169, 737, 213);
        DrawTrafficLight(g);
        DrawCar(g, carX, 173 + (running ? MathF.Sin(pulse*23)*.6f : 0));
        Theme.FillRound(g, Color.FromArgb(210, 12, 26, 46), new RectangleF(22, 20, 92, 31), 7);
        using var routeFont = new Font("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
        using var titleFont = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel);
        using var small = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        using var white = new SolidBrush(Theme.Text);
        using var muted = new SolidBrush(Theme.Muted);
        using var blue = new SolidBrush(Theme.Cyan);
        g.DrawString("A01  /  AUTO", routeFont, blue, 30, 27);
        g.DrawString(Lang.T(running ? "sceneOn" : "sceneOff"), titleFont, white, 22, 61);
        g.DrawString(Lang.T(running ? "sceneOnDetail" : "sceneOffDetail"), small, muted, 24, 94);
        Theme.FillRound(g, Color.FromArgb(200, 10, 19, 33), new RectangleF(759, 20, 137, 30), 15);
        using var indicator = new SolidBrush(running ? Theme.Cyan : Theme.Red);
        g.FillEllipse(indicator, 772, 31, 7, 7);
        g.DrawString(Lang.T(running ? "onRoad" : "redLight"), routeFont, white, 789, 26);
        g.ResetClip();
        using var border = new Pen(Theme.Border);
        g.DrawPath(border, clip);
    }

    private void DrawCompact(Graphics g)
    {
        // Keep the car's proportions when the window becomes narrow or shallow.
        using var frame = Theme.Round(new RectangleF(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12);
        using var background = new SolidBrush(Color.FromArgb(12, 24, 43));
        g.FillPath(background, frame);
        var saved = g.Save();
        g.SetClip(frame);
        float scale = Math.Min(Width / 420f, Height / 160f);
        g.TranslateTransform((Width - 420 * scale) / 2, (Height - 160 * scale) / 2);
        g.ScaleTransform(scale, scale);
        using var skyline = new SolidBrush(Color.FromArgb(17, 36, 58));
        using var windows = new SolidBrush(Color.FromArgb(44, 78, 105));
        for (int i = -1; i < 13; i++)
        {
            float x = i * 40 - offset * .10f % 40;
            int h = 15 + ((i + 13) * 19) % 36;
            g.FillRectangle(skyline, x, 90 - h, 27, h);
            g.FillRectangle(windows, x + 6, 98 - h, 3, 4);
            g.FillRectangle(windows, x + 16, 98 - h, 3, 4);
        }
        using var asphalt = new SolidBrush(Color.FromArgb(24, 32, 46));
        g.FillRectangle(asphalt, 0, 90, 420, 70);
        using var edge = new Pen(Color.FromArgb(62, 96, 127), 1.5f);
        g.DrawLine(edge, 0, 91, 420, 91);
        g.DrawLine(edge, 0, 157, 420, 157);
        using var lane = new Pen(Color.FromArgb(105, 139, 167), 2);
        for (int i = -1; i < 9; i++)
        {
            float x = i * 65 - offset % 65;
            g.DrawLine(lane, x, 143, x + 30, 143);
        }
        using var stopLine = new Pen(Color.FromArgb(running ? 45 : 135, 164, 188, 214), 3);
        g.DrawLine(stopLine, 332, 97, 332, 137);
        var lightTransform = g.Save();
        g.TranslateTransform(-406, -55);
        DrawTrafficLight(g);
        g.Restore(lightTransform);
        float progress = Math.Clamp((carX - 280) / 298, 0, 1);
        var carTransform = g.Save();
        g.TranslateTransform(70 + progress * 130, 100 + (running ? MathF.Sin(pulse * 23) * .5f : 0));
        g.ScaleTransform(.85f, .85f);
        DrawCar(g, 0, 0);
        g.Restore(carTransform);
        DrawSubmitCounter(g, 70 + progress * 130);
        g.Restore(saved);
        using var border = new Pen(Theme.Border);
        g.DrawPath(border, frame);
    }

    private void DrawSubmitCounter(Graphics g, float compactCarX)
    {
        double now = clock.Elapsed.TotalSeconds;
        float age = submissionBursts.Count > 0 ? (float)(now - submissionBursts[^1]) : 2;
        float bounce = age < .45f ? MathF.Sin(age / .45f * MathF.PI) : 0;
        using var numberFont = new Font("Segoe UI", 25 + bounce * 4, FontStyle.Bold, GraphicsUnit.Pixel);
        using var labelFont = new Font("Segoe UI", 10, FontStyle.Bold, GraphicsUnit.Pixel);
        string value = submitCount.ToString("N0", Lang.Culture);
        float numberWidth = g.MeasureString(value, numberFont).Width;
        float badgeWidth = Math.Max(114, numberWidth + 82);
        var badge = new RectangleF(12, 10, badgeWidth, 44);
        Theme.FillRound(g, Color.FromArgb(235, 12, 27, 47), badge, 12);
        using var outline = Theme.Round(badge, 12);
        using var border = new Pen(Color.FromArgb((int)(90 + bounce * 150), Theme.Blue), 1 + bounce);
        g.DrawPath(border, outline);
        using var numberBrush = new SolidBrush(bounce > 0 ? Theme.Cyan : Theme.Text);
        using var labelBrush = new SolidBrush(Theme.Muted);
        g.DrawString(value, numberFont, numberBrush, 21, 13 - bounce * 2);
        g.DrawString("SUBMITS", labelFont, labelBrush, 26 + numberWidth, 26);

        for (int i = 0; i < submissionBursts.Count; i++)
        {
            float elapsed = (float)(now - submissionBursts[i]);
            if (elapsed < 0 || elapsed >= 1.2f) continue;
            float t = elapsed / 1.2f;
            int alpha = (int)(255 * Math.Min(1, (1 - t) * 1.8f));
            float x = compactCarX + 62 + i * 12;
            float y = 54 - 30 * t;
            using var popupFont = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel);
            using var glow = new SolidBrush(Color.FromArgb(alpha / 7, Theme.Cyan));
            using var popup = new SolidBrush(Color.FromArgb(alpha, Theme.Cyan));
            g.FillEllipse(glow, x - 10, y - 3, 53, 34);
            g.DrawString("+1", popupFont, popup, x, y);
        }
    }

    private void DrawTrafficLight(Graphics g)
    {
        using var pole = new Pen(Color.FromArgb(70, 91, 115), 5);
        g.DrawLine(pole, 762, 115, 762, 173);
        Theme.FillRound(g, Color.FromArgb(6, 12, 22), new RectangleF(747, 78, 30, 65), 9);
        for (int i=0; i<3; i++)
        {
            bool lit = running ? i == 2 : i == 0;
            Color color = i == 0 ? Theme.Red : i == 1 ? Color.Orange : Color.FromArgb(74, 239, 173);
            if (lit)
            {
                using var halo = new SolidBrush(Color.FromArgb(25, color));
                g.FillEllipse(halo, 744, 80+i*19-5, 36, 36);
            }
            using var bulb = new SolidBrush(lit ? color : Color.FromArgb(32, 44, 57));
            g.FillEllipse(bulb, 755, 87+i*18, 14, 14);
        }
    }

    private void DrawCar(Graphics g, float x, float y)
    {
        var saved = g.Save();
        g.TranslateTransform(x, y);
        using var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        g.FillEllipse(shadow, -8, 27, 159, 17);
        using var glow = new Pen(Color.FromArgb((int)(30+speed*50), Theme.Blue), 7);
        g.DrawLine(glow, 11, 36, 130, 36);
        if (speed > .05f)
        {
            for (int i=0; i<4; i++)
            {
                using var trail = new Pen(Color.FromArgb((int)(speed*(100-i*20)), Theme.Cyan), 2);
                g.DrawLine(trail, -20-i*16, 4+i*8, -75-i*24, 4+i*8);
            }
        }
        using var body = new LinearGradientBrush(new Rectangle(0,-18,150,55), Color.FromArgb(77,164,255), Color.FromArgb(24,75,201), 90);
        using var shape = new GraphicsPath();
        shape.AddLines([new PointF(0,12), new(7,2), new(34,-2), new(53,-20), new(92,-20), new(116,-1), new(141,6), new(148,19), new(143,31), new(4,31)]);
        shape.CloseFigure();
        g.FillPath(body, shape);
        using var outline = new Pen(Color.FromArgb(106,189,255), 1.2f);
        g.DrawPath(outline, shape);
        using var glass = new SolidBrush(Color.FromArgb(12,34,64));
        g.FillPolygon(glass, [new(42,-1), new(57,-15), new(70,-15), new(70,-1)]);
        g.FillPolygon(glass, [new(76,-15), new(91,-15), new(109,-1), new(76,-1)]);
        using var trim = new Pen(Color.FromArgb(17,68,152), 1);
        g.DrawLine(trim, 74, 3, 74, 27);
        g.DrawLine(outline, 80, 6, 88, 6);
        using var headlight = new SolidBrush(Color.FromArgb(213,244,255));
        g.FillRectangle(headlight, 135, 9, 10, 5);
        using var beam = new SolidBrush(Color.FromArgb(16,157,215,255));
        g.FillPolygon(beam, [new(145,9), new(219,-2), new(219,32), new(145,15)]);
        using var brake = new SolidBrush(running ? Color.FromArgb(169,49,78) : Theme.Red);
        g.FillRectangle(brake, 1, 10, 8, 6);
        foreach (int wx in new[] { 31, 119 })
        {
            using var tire = new SolidBrush(Color.FromArgb(6,10,18));
            g.FillEllipse(tire, wx-13, 17, 26, 26);
            using var rim = new Pen(Color.FromArgb(127,160,193), 2.5f);
            g.DrawEllipse(rim, wx-8, 22, 16, 16);
            float angle = wheelAngle*MathF.PI/180;
            g.DrawLine(rim, wx-7*MathF.Cos(angle), 30-7*MathF.Sin(angle), wx+7*MathF.Cos(angle), 30+7*MathF.Sin(angle));
        }
        g.Restore(saved);
    }

    protected override void Dispose(bool disposing)
    { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
