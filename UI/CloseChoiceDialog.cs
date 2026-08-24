namespace AutoHDR.UI;

public enum CloseChoice
{
    KeepRunning,
    ExitApplication
}

public sealed class CloseChoiceDialog : Form
{
    private readonly CheckBox _remember = new();

    public CloseChoice Choice { get; private set; } = CloseChoice.KeepRunning;
    public bool RememberChoice => _remember.Checked;

    public CloseChoiceDialog(AppTheme theme)
    {
        Text = "Close TrueAuto HDR?";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, 220);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);

        var title = new Label
        {
            Text = "What should TrueAuto HDR do?",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold)
        };

        var message = new Label
        {
            Text = "TrueAuto HDR needs to keep running in the background to automatically switch HDR when games start or exit.",
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Tag = "muted"
        };

        _remember.Text = "Remember my choice";
        _remember.AutoSize = true;

        var keep = new Button
        {
            Text = "Keep running in background",
            AutoSize = true,
            Padding = new Padding(10, 2, 10, 2)
        };
        keep.Click += (_, _) =>
        {
            Choice = CloseChoice.KeepRunning;
            DialogResult = DialogResult.OK;
            Close();
        };

        var exit = new Button
        {
            Text = "Exit TrueAuto HDR",
            AutoSize = true,
            Padding = new Padding(10, 2, 10, 2)
        };
        exit.Click += (_, _) =>
        {
            Choice = CloseChoice.ExitApplication;
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Tag = "content"
        };
        buttons.Controls.Add(exit);
        buttons.Controls.Add(keep);
        buttons.Controls.Add(cancel);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20),
            Tag = "content"
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(message, 0, 1);
        layout.Controls.Add(_remember, 0, 2);
        layout.Controls.Add(buttons, 0, 4);

        Controls.Add(layout);
        CancelButton = cancel;
        ThemeManager.Apply(this, theme);
    }
}
