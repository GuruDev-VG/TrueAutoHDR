using AutoHDR.Models;
using AutoHDR.Rules;

namespace AutoHDR.UI;

public sealed class GameRuleForm : Form
{
    private readonly NumericUpDown _enableDelay = new();
    private readonly NumericUpDown _exitGrace = new();
    private readonly CheckBox _keepHdr = new();
    private readonly ComboBox _display = new();

    public GameRule Result { get; private set; }

    public GameRuleForm(InstalledGame game, GameRule current, AppTheme theme)
    {
        Result = current;
        Text = $"Rules — {game.Name}";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(510, 310);
        MinimumSize = MaximumSize = new Size(526, 349);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);

        _enableDelay.Minimum = 0; _enableDelay.Maximum = 30000; _enableDelay.Increment = 250;
        _enableDelay.Value = Math.Clamp(current.EnableDelayMs, 0, 30000);
        _enableDelay.Dock = DockStyle.Fill;

        _exitGrace.Minimum = 0; _exitGrace.Maximum = 30000; _exitGrace.Increment = 250;
        _exitGrace.Value = Math.Clamp(current.ExitGraceMs, 0, 30000);
        _exitGrace.Dock = DockStyle.Fill;

        _keepHdr.Text = "Keep HDR enabled after this game exits";
        _keepHdr.Checked = current.KeepHdrAfterExit;
        _keepHdr.AutoSize = true;

        _display.DropDownStyle = ComboBoxStyle.DropDownList;
        _display.Dock = DockStyle.Fill;
        _display.Items.Add("Windows main display");
        foreach (var screen in Screen.AllScreens)
            _display.Items.Add($"{screen.DeviceName}{(screen.Primary ? " (Main)" : "")}");
        _display.SelectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(current.DisplayDeviceName))
        {
            for (var i = 1; i < _display.Items.Count; i++)
            {
                if (_display.Items[i]?.ToString()?.StartsWith(current.DisplayDeviceName,
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    _display.SelectedIndex = i;
                    break;
                }
            }
        }

        var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            Result = new GameRule
            {
                EnableDelayMs = (int)_enableDelay.Value,
                ExitGraceMs = (int)_exitGrace.Value,
                KeepHdrAfterExit = _keepHdr.Checked,
                DisplayDeviceName = _display.SelectedIndex <= 0
                    ? ""
                    : _display.SelectedItem?.ToString()?.Split(' ')[0] ?? ""
            };
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7,
            Padding = new Padding(18), Tag = "content"
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var title = new Label { Text = game.Name, AutoSize = true, Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold) };
        grid.Controls.Add(title, 0, 0); grid.SetColumnSpan(title, 2);
        grid.Controls.Add(new Label { Text = "HDR enable delay (ms)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(_enableDelay, 1, 1);
        grid.Controls.Add(new Label { Text = "Exit grace period (ms)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        grid.Controls.Add(_exitGrace, 1, 2);
        grid.Controls.Add(new Label { Text = "HDR display", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        grid.Controls.Add(_display, 1, 3);
        grid.Controls.Add(_keepHdr, 0, 4); grid.SetColumnSpan(_keepHdr, 2);

        var hint = new Label
        {
            Text = "Delays are event-driven Tasks; they do not add an idle polling loop or background timer.",
            AutoSize = true, MaximumSize = new Size(455, 0), Tag = "muted"
        };
        grid.Controls.Add(hint, 0, 5); grid.SetColumnSpan(hint, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "content" };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        grid.Controls.Add(buttons, 0, 6); grid.SetColumnSpan(buttons, 2);
        Controls.Add(grid);

        AcceptButton = save; CancelButton = cancel;
        ThemeManager.Apply(this, theme);
    }
}
