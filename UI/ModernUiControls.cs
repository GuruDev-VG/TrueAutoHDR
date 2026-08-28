using System.Drawing.Drawing2D;

namespace AutoHDR.UI;

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 10;
    public int BorderThickness { get; set; } = 1;
    public string AccentRole { get; set; } = "surface";

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        DoubleBuffered = true;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var dark = ThemeManager.ControlIsDark(this);
        var fillTop = AccentRole switch
        {
            "card" => dark ? Color.FromArgb(18, 26, 36) : Color.White,
            "input" => dark ? Color.FromArgb(20, 29, 39) : Color.White,
            "footer" => dark ? Color.FromArgb(14, 21, 30) : Color.White,
            _ => dark ? Color.FromArgb(16, 24, 34) : Color.White
        };
        var fillBottom = AccentRole switch
        {
            "card" => dark ? Color.FromArgb(14, 21, 30) : Color.FromArgb(248, 250, 252),
            "input" => dark ? Color.FromArgb(18, 26, 36) : Color.FromArgb(249, 251, 253),
            "footer" => dark ? Color.FromArgb(12, 18, 27) : Color.FromArgb(248, 250, 252),
            _ => dark ? Color.FromArgb(13, 20, 29) : Color.FromArgb(248, 250, 252)
        };
        var border = dark ? Color.FromArgb(43, 56, 70) : Color.FromArgb(210, 217, 226);
        using var path = RoundedRect(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f)), Radius);
        using var fillBrush = new LinearGradientBrush(ClientRectangle, fillTop, fillBottom, 90f);
        using var pen = new Pen(border, BorderThickness);
        e.Graphics.FillPath(fillBrush, path);
        if (BorderThickness > 0) e.Graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedRect(new RectangleF(0, 0, Width, Height), Radius);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    internal static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var r = Math.Max(0f, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));
        var d = r * 2f;
        if (d < 1f)
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
}

internal sealed class ModernButton : Button
{
    public string Kind { get; set; } = "secondary";
    public int Radius { get; set; } = 7;

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Height = 46;
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        Padding = new Padding(14, 0, 14, 0);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var dark = ThemeManager.ControlIsDark(this);
        var disabled = !Enabled;
        var hovered = ClientRectangle.Contains(PointToClient(System.Windows.Forms.Cursor.Position)) && !disabled;
        var pressed = Capture && Control.MouseButtons == MouseButtons.Left && !disabled;
        var rect = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f));
        using var path = RoundedPanel.RoundedRect(rect, Radius);

        Color fore;
        Color border;
        if (Kind == "primary")
        {
            var a = dark ? Color.FromArgb(117, 67, 232) : Color.FromArgb(112, 61, 210);
            var b = dark ? Color.FromArgb(148, 87, 255) : Color.FromArgb(130, 73, 225);
            if (pressed) { a = Color.FromArgb(95, 50, 195); b = Color.FromArgb(125, 65, 220); }
            else if (hovered) { a = Color.FromArgb(130, 76, 242); b = Color.FromArgb(160, 100, 255); }
            using var brush = new LinearGradientBrush(rect, a, b, 0f);
            g.FillPath(brush, path);
            border = dark ? Color.FromArgb(164, 108, 255) : Color.FromArgb(102, 55, 188);
            fore = disabled ? Color.FromArgb(165, 165, 175) : Color.White;
        }
        else
        {
            var fill = dark
                ? (hovered ? Color.FromArgb(28, 38, 50) : Color.FromArgb(20, 29, 39))
                : (hovered ? Color.FromArgb(244, 247, 250) : Color.White);
            if (pressed) fill = dark ? Color.FromArgb(32, 43, 57) : Color.FromArgb(235, 240, 245);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
            border = Kind switch
            {
                "purple-outline" => ThemeManager.Accent("purple", dark),
                "positive-outline" => ThemeManager.Accent("green", dark),
                "negative-outline" => dark ? Color.FromArgb(224, 88, 104) : Color.FromArgb(182, 55, 70),
                _ => dark ? Color.FromArgb(49, 63, 79) : Color.FromArgb(205, 214, 224)
            };
            fore = Kind switch
            {
                "purple-outline" => ThemeManager.Accent("purple", dark),
                "positive-outline" => ThemeManager.Accent("green", dark),
                "negative-outline" => dark ? Color.FromArgb(245, 123, 136) : Color.FromArgb(170, 44, 60),
                _ => dark ? Color.FromArgb(238, 242, 247) : Color.FromArgb(31, 38, 46)
            };
            if (disabled) fore = dark ? Color.FromArgb(92, 104, 117) : Color.FromArgb(160, 168, 177);
        }

        using (var pen = new Pen(border, 1f)) g.DrawPath(pen, path);

        var (icon, label) = SplitCaption(Text);
        if (string.IsNullOrEmpty(icon))
        {
            TextRenderer.DrawText(g, label, Font, Rectangle.Round(rect), fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
        else
        {
            using var iconFont = new Font("Segoe UI Symbol", Math.Max(9f, Font.Size + 1.5f), FontStyle.Regular, GraphicsUnit.Point);
            var labelSize = TextRenderer.MeasureText(label, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding);
            var iconSize = TextRenderer.MeasureText(icon, iconFont, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding);
            var gap = 9;
            var total = iconSize.Width + gap + labelSize.Width;
            var left = Math.Max(10, (Width - total) / 2);
            TextRenderer.DrawText(g, icon, iconFont, new Rectangle(left, 0, iconSize.Width + 2, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, label, Font, new Rectangle(left + iconSize.Width + gap, 0, Math.Max(0, Width - left - iconSize.Width - gap - 8), Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    private static (string Icon, string Label) SplitCaption(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (string.Empty, string.Empty);
        var marker = text.IndexOf("  ", StringComparison.Ordinal);
        if (marker <= 0) return (string.Empty, text);
        return (text[..marker].Trim(), text[(marker + 2)..].Trim());
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs mevent) { base.OnMouseDown(mevent); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs mevent) { base.OnMouseUp(mevent); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}

internal sealed class PillLabel : Label
{
    public int Radius { get; set; } = 16;
    public string Kind { get; set; } = "muted";

    public PillLabel()
    {
        AutoSize = false;
        TextAlign = ContentAlignment.MiddleCenter;
        Height = 32;
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedPanel.RoundedRect(new RectangleF(0, 0, Width, Height), Math.Min(Radius, Height / 2f));
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // The Region clips this control to a real pill. Let the parent card show
        // through around the curved corners instead of painting a rectangle.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var dark = ThemeManager.ControlIsDark(this);
        var (fill, border, fore) = Kind switch
        {
            "positive" => (dark ? Color.FromArgb(15, 46, 31) : Color.FromArgb(226, 247, 234), dark ? Color.FromArgb(27, 113, 62) : Color.FromArgb(76, 173, 108), ThemeManager.Accent("green", dark)),
            "negative" => (dark ? Color.FromArgb(48, 24, 30) : Color.FromArgb(253, 235, 238), dark ? Color.FromArgb(116, 45, 57) : Color.FromArgb(209, 95, 112), dark ? Color.FromArgb(255, 105, 120) : Color.FromArgb(178, 46, 67)),
            "hdr10" => (dark ? Color.FromArgb(34, 25, 58) : Color.FromArgb(241, 233, 253), dark ? Color.FromArgb(102, 65, 176) : Color.FromArgb(155, 112, 220), ThemeManager.Accent("purple", dark)),
            "store" => (dark ? Color.FromArgb(29, 37, 49) : Color.FromArgb(238, 242, 246), dark ? Color.FromArgb(29, 37, 49) : Color.FromArgb(221, 228, 235), dark ? Color.White : Color.FromArgb(38, 45, 54)),
            _ => (dark ? Color.FromArgb(31, 40, 52) : Color.FromArgb(235, 239, 244), dark ? Color.FromArgb(45, 56, 70) : Color.FromArgb(215, 222, 230), dark ? Color.FromArgb(198, 207, 218) : Color.FromArgb(78, 88, 99))
        };

        using var path = RoundedPanel.RoundedRect(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f)), Math.Min(Radius, Height / 2f));
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1f);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);

        var textRect = ClientRectangle;
        var icon = string.Empty;
        if (Kind == "positive") icon = "✓";
        else if (Kind == "negative") icon = "—";
        else if (Kind == "hdr10") icon = "✳";
        else if (Kind == "store") icon = "●";

        if (!string.IsNullOrEmpty(icon))
        {
            var circleSize = Kind == "store" ? 8 : 16;
            var cx = 12;
            var cy = (Height - circleSize) / 2;
            var accent = Kind switch
            {
                "positive" => ThemeManager.Accent("green", dark),
                "negative" => dark ? Color.FromArgb(245, 123, 136) : Color.FromArgb(170, 44, 60),
                "hdr10" => ThemeManager.Accent("purple", dark),
                _ => dark ? Color.White : Color.FromArgb(60, 68, 78)
            };

            if (Kind == "store")
            {
                using var dot = new SolidBrush(accent);
                e.Graphics.FillEllipse(dot, cx, cy, circleSize, circleSize);
            }
            else
            {
                using var iconPen = new Pen(accent, 1.3f);
                e.Graphics.DrawEllipse(iconPen, cx, cy, circleSize, circleSize);
                using var iconFont = new Font("Segoe UI Symbol", 7.5F, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, icon, iconFont, new Rectangle(cx, cy, circleSize, circleSize), accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            textRect = new Rectangle(cx + circleSize + 5, 0, Math.Max(0, Width - (cx + circleSize + 11)), Height);
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

internal sealed class ModernSearchBox : UserControl
{
    private readonly TextBox _box = new();
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get => _box.Text; set => _box.Text = value ?? string.Empty; }
    public string PlaceholderText { get => _box.PlaceholderText; set => _box.PlaceholderText = value; }

    public new event EventHandler? TextChanged;

    public ModernSearchBox()
    {
        Height = 44;
        Padding = new Padding(14, 10, 42, 8);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        _box.BorderStyle = BorderStyle.None;
        _box.Tag = "modern-search-inner";
        _box.Dock = DockStyle.Fill;
        _box.Font = new Font("Segoe UI", 9.5F);
        _box.TextChanged += (_, e) => TextChanged?.Invoke(this, e);
        Controls.Add(_box);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var dark = ThemeManager.ControlIsDark(this);
        var fill = dark ? Color.FromArgb(20, 29, 39) : Color.White;
        var border = dark ? Color.FromArgb(49, 63, 79) : Color.FromArgb(205, 214, 224);
        using var path = RoundedPanel.RoundedRect(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f)), 8);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1f);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        var fore = dark ? Color.FromArgb(196, 205, 216) : Color.FromArgb(91, 101, 112);
        using var iconFont = new Font("Segoe Fluent Icons", 12F, FontStyle.Regular);
        TextRenderer.DrawText(e.Graphics, "\uE721", iconFont, new Rectangle(Width - 39, 0, 30, Height), fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        base.OnPaint(e);
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        SyncColors();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        SyncColors();
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        SyncColors();
    }

    private void SyncColors()
    {
        var dark = ThemeManager.ControlIsDark(this);
        _box.BackColor = dark ? Color.FromArgb(20, 29, 39) : Color.White;
        _box.ForeColor = dark ? Color.FromArgb(238, 242, 247) : Color.FromArgb(31, 38, 46);
    }
}

internal enum ArtworkFillMode
{
    Contain,
    Cover
}

internal sealed class ArtworkBox : PictureBox
{
    public ArtworkFillMode FillMode { get; set; } = ArtworkFillMode.Cover;

    public ArtworkBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        DoubleBuffered = true;
        BackColor = Color.Black;
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        pe.Graphics.CompositingQuality = CompositingQuality.HighQuality;

        using (var bg = new SolidBrush(BackColor))
            pe.Graphics.FillRectangle(bg, ClientRectangle);

        if (Image is null)
        {
            base.OnPaint(pe);
            return;
        }

        var iw = Math.Max(1, Image.Width);
        var ih = Math.Max(1, Image.Height);
        var sx = Width / (float)iw;
        var sy = Height / (float)ih;
        var scale = FillMode == ArtworkFillMode.Cover ? Math.Max(sx, sy) : Math.Min(sx, sy);
        var dw = iw * scale;
        var dh = ih * scale;
        var dx = (Width - dw) / 2f;
        var dy = (Height - dh) / 2f;
        pe.Graphics.DrawImage(Image, new RectangleF(dx, dy, dw, dh));
    }
}

internal sealed class MetricStrip : Control
{
    private string[] _items = Array.Empty<string>();

    public MetricStrip()
    {
        Height = 48;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
    }

    public void SetValues(string lastScan, int games, int hdr, int hdr10)
    {
        _items = new[] { $"◷  Last scan: {lastScan}", $"◉  {games} games found", $"✓  {hdr} HDR", $"✳  {hdr10} HDR10+" };
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var dark = ThemeManager.ControlIsDark(this);
        var text = dark ? Color.FromArgb(205, 213, 223) : Color.FromArgb(63, 72, 82);
        var sep = dark ? Color.FromArgb(59, 70, 82) : Color.FromArgb(210, 218, 226);
        var x = 0;
        for (var i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            var color = i switch
            {
                2 => ThemeManager.Accent("green", dark),
                3 => ThemeManager.Accent("purple", dark),
                _ => text
            };
            var size = TextRenderer.MeasureText(item, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, item, Font, new Rectangle(x, 0, size.Width + 4, Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            x += size.Width + 22;
            if (i < _items.Length - 1)
            {
                using var pen = new Pen(sep);
                e.Graphics.DrawLine(pen, x - 10, 13, x - 10, Height - 13);
            }
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = 0;
        foreach (var item in _items)
            width += TextRenderer.MeasureText(item, Font, new Size(int.MaxValue, Height), TextFormatFlags.NoPadding).Width + 22;
        return new Size(Math.Max(1, width), Height);
    }
}


internal sealed class ModernComboBox : Control
{
    private readonly List<object> _items = new();
    private int _selectedIndex = -1;

    public List<object> Items => _items;
    public ComboBoxStyle DropDownStyle { get; set; } = ComboBoxStyle.DropDownList;
    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var normalized = value < -1 ? -1 : Math.Min(value, _items.Count - 1);
            if (_selectedIndex == normalized) return;
            _selectedIndex = normalized;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public ModernComboBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Height = 36;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var dark = ThemeManager.ControlIsDark(this);
        var fill = dark ? Color.FromArgb(20, 29, 39) : Color.White;
        var border = Focused
            ? ThemeManager.Accent("purple", dark)
            : (dark ? Color.FromArgb(49, 63, 79) : Color.FromArgb(205, 214, 224));
        var fore = dark ? Color.FromArgb(238, 242, 247) : Color.FromArgb(31, 38, 46);
        var arrow = dark ? Color.FromArgb(207, 216, 226) : Color.FromArgb(72, 82, 93);

        using var path = RoundedPanel.RoundedRect(new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1f), Math.Max(1, Height - 1f)), 6);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1f);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);

        var text = SelectedItem is null ? string.Empty : SelectedItem.ToString() ?? string.Empty;
        TextRenderer.DrawText(e.Graphics, text, Font,
            new Rectangle(12, 0, Math.Max(0, Width - 48), Height), fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        var cx = Width - 22;
        var cy = Height / 2;
        using var arrowPen = new Pen(arrow, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(arrowPen, cx - 5, cy - 2, cx, cy + 3);
        e.Graphics.DrawLine(arrowPen, cx, cy + 3, cx + 5, cy - 2);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowDropDown();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
        {
            ShowDropDown();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up && _items.Count > 0)
        {
            SelectedIndex = Math.Max(0, _selectedIndex - 1);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    private void ShowDropDown()
    {
        if (!Enabled || _items.Count == 0) return;
        var dark = ThemeManager.ControlIsDark(this);
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = dark ? Color.FromArgb(20, 29, 39) : Color.White,
            ForeColor = dark ? Color.FromArgb(238, 242, 247) : Color.FromArgb(31, 38, 46),
            Font = Font,
            Padding = new Padding(2)
        };
        for (var i = 0; i < _items.Count; i++)
        {
            var idx = i;
            var label = _items[i]?.ToString() ?? string.Empty;
            var item = new ToolStripMenuItem(label)
            {
                Checked = idx == _selectedIndex,
                CheckOnClick = false,
                AutoSize = false,
                Width = Math.Max(Width - 4, 220),
                Height = 32
            };
            item.Click += (_, _) => SelectedIndex = idx;
            menu.Items.Add(item);
        }
        ThemeManager.Apply(menu, dark ? AppTheme.Dark : AppTheme.Light);
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(this, new Point(0, Height + 2));
    }
}
