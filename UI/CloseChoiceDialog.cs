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
        ClientSize = new Size(470, 258);
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

        var choiceButtons = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Anchor = AnchorStyles.None,
            Tag = "content",
            Margin = new Padding(0)
        };
        choiceButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        choiceButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        keep.Margin = new Padding(0, 0, 8, 0);
        exit.Margin = new Padding(8, 0, 0, 0);
        choiceButtons.Controls.Add(keep, 0, 0);
        choiceButtons.Controls.Add(exit, 1, 0);

        var cancelRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Tag = "content",
            Margin = new Padding(0)
        };
        cancelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cancelRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cancelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cancel.Anchor = AnchorStyles.None;
        cancel.Margin = new Padding(0);
        cancelRow.Controls.Add(cancel, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20),
            Tag = "content"
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(message, 0, 1);
        layout.Controls.Add(_remember, 0, 2);
        layout.Controls.Add(choiceButtons, 0, 4);
        layout.Controls.Add(cancelRow, 0, 5);

        Controls.Add(layout);
        CancelButton = cancel;
        ThemeManager.Apply(this, theme);
    }
}
