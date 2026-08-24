namespace AutoHDR.UI;

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        DoubleBuffered = true;
        UpdateStyles();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Invalidate(true);
    }
}

internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        UpdateStyles();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Invalidate(true);
    }
}


internal sealed class SlickProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer;
    private bool _indeterminate;
    private float _progress;
    private float _phase;

    public SlickProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        DoubleBuffered = true;
        Height = 7;
        Visible = false;
        TabStop = false;
        Tag = "scan-progress";

        _animationTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _animationTimer.Tick += (_, _) =>
        {
            _phase += 0.035f;
            if (_phase > 1f) _phase -= 1f;
            Invalidate();
        };
    }

    public void StartIndeterminate()
    {
        _indeterminate = true;
        _progress = 0f;
        _phase = 0f;
        Visible = true;
        _animationTimer.Start();
        Invalidate();
    }

    public void StartDeterminate(int maximum)
    {
        _animationTimer.Stop();
        _indeterminate = false;
        _progress = maximum <= 0 ? 0f : 0f;
        Visible = true;
        Invalidate();
    }

    public void SetProgress(int value, int maximum)
    {
        if (maximum <= 0) return;
        _animationTimer.Stop();
        _indeterminate = false;
        _progress = Math.Clamp((float)value / maximum, 0f, 1f);
        Visible = true;
        Invalidate();
    }

    public void StopAndHide()
    {
        _animationTimer.Stop();
        _indeterminate = false;
        _progress = 0f;
        Visible = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 2 || ClientSize.Height <= 2) return;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var dark = ThemeManager.ControlIsDark(this);
        var track = dark ? Color.FromArgb(42, 49, 57) : Color.FromArgb(222, 227, 233);
        var accentA = ThemeManager.Accent("purple", dark);
        var accentB = ThemeManager.Accent("blue", dark);
        var rect = new RectangleF(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

        using (var path = RoundedRect(rect, rect.Height / 2f))
        using (var brush = new SolidBrush(track))
            e.Graphics.FillPath(brush, path);

        if (_indeterminate)
        {
            var width = Math.Max(46f, ClientSize.Width * 0.24f);
            var travel = ClientSize.Width + width;
            var x = (_phase * travel) - width;
            var barRect = new RectangleF(x, 0, width, rect.Height);
            using var clipPath = RoundedRect(rect, rect.Height / 2f);
            var state = e.Graphics.Save();
            e.Graphics.SetClip(clipPath);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(barRect, accentA, accentB, 0f);
            e.Graphics.FillRectangle(brush, barRect);
            e.Graphics.Restore(state);
        }
        else if (_progress > 0f)
        {
            var width = Math.Max(rect.Height, rect.Width * _progress);
            var barRect = new RectangleF(0, 0, width, rect.Height);
            using var path = RoundedRect(barRect, rect.Height / 2f);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(barRect, accentA, accentB, 0f);
            e.Graphics.FillPath(brush, path);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var d = radius * 2f;
        if (d <= 0f)
        {
            path.AddRectangle(rect);
            return path;
        }
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animationTimer.Dispose();
        base.Dispose(disposing);
    }
}
