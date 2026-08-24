using AutoHDR.Diagnostics;

namespace AutoHDR.UI;

public sealed class DiagnosticsForm : Form
{
    public DiagnosticsForm(DiagnosticsService diagnostics, AppTheme theme)
    {
        Text = "TrueAuto HDR — Diagnostics";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(620, 440);
        AutoScaleMode = AutoScaleMode.Dpi;

        var report = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
            Text = diagnostics.BuildReport()
        };

        var copy = new Button { Text = "Copy report", AutoSize = true };
        copy.Click += (_, _) => Clipboard.SetText(report.Text);
        var refresh = new Button { Text = "Refresh", AutoSize = true };
        refresh.Click += (_, _) => report.Text = diagnostics.BuildReport();
        var close = new Button { Text = "Close", AutoSize = true };
        close.Click += (_, _) => Close();

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8), Tag = "footer"
        };
        bottom.Controls.Add(close); bottom.Controls.Add(copy); bottom.Controls.Add(refresh);

        Controls.Add(report);
        Controls.Add(bottom);
        ThemeManager.Apply(this, theme);
    }
}
