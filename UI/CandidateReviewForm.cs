using AutoHDR.Models;

namespace AutoHDR.UI;

public sealed record CandidateReviewItem(InstalledGame Game, string Source, string MatchedTitle = "", string Confidence = "Medium", string MatchType = "Title match");

public sealed class CandidateReviewForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly List<CandidateReviewItem> _items;
    private readonly AppTheme _theme;

    public IReadOnlyList<CandidateReviewItem> SelectedItems
        => _grid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => Convert.ToBoolean(row.Cells[0].Value ?? false))
            .Select(row => (CandidateReviewItem)row.Tag!)
            .ToList();

    public CandidateReviewForm(IReadOnlyList<CandidateReviewItem> items, AppTheme theme)
    {
        _items = items.ToList();
        _theme = theme;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "Review HDR candidates — TrueAuto HDR";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 500);
        ClientSize = new Size(940, 590);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        ShowInTaskbar = false;

        var title = new Label
        {
            Text = "Review HDR candidates",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 3)
        };
        var subtitle = new Label
        {
            Text = "Review cross-store and community matches before enabling HDR. High/Verified matches are strongest; Medium/Low matches always require your confirmation.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 42,
            Font = new Font("Segoe UI", 9.5F),
            Tag = "muted"
        };

        var heading = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 16, 20, 8),
            Tag = "header"
        };
        var headingLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "header-layout"
        };
        headingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        headingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        headingLayout.Controls.Add(title, 0, 0);
        headingLayout.Controls.Add(subtitle, 0, 1);
        heading.Controls.Add(headingLayout);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 36;

        var includeColumn = new DataGridViewCheckBoxColumn
        {
            Name = "include",
            HeaderText = "Add",
            FillWeight = 28,
            TrueValue = true,
            FalseValue = false
        };
        _grid.Columns.Add(includeColumn);
        _grid.Columns.Add("game", "Game");
        _grid.Columns.Add("store", "Store");
        _grid.Columns.Add("matched", "Matched community entry");
        _grid.Columns.Add("confidence", "Confidence");
        _grid.Columns.Add("method", "Match method");
        _grid.Columns.Add("source", "Match source");
        for (var i = 1; i < _grid.Columns.Count; i++) _grid.Columns[i].ReadOnly = true;
        _grid.Columns[1].FillWeight = 125;
        _grid.Columns[2].FillWeight = 45;
        _grid.Columns[3].FillWeight = 120;
        _grid.Columns[4].FillWeight = 55;
        _grid.Columns[5].FillWeight = 80;
        _grid.Columns[6].FillWeight = 90;

        foreach (var item in _items.OrderBy(i => i.Game.Name, StringComparer.OrdinalIgnoreCase))
        {
            var row = _grid.Rows.Add(false, item.Game.Name, item.Game.Store, string.IsNullOrWhiteSpace(item.MatchedTitle) ? item.Game.Name : item.MatchedTitle, item.Confidence, item.MatchType, item.Source);
            _grid.Rows[row].Tag = item;
            _grid.Rows[row].Cells[4].ToolTipText = item.Confidence switch
            {
                "Verified" => "Exact storefront or database ID.",
                "High" => "Exact normalized title or explicit alias. Safe for automatic matching.",
                "Medium" => "Likely same game after edition/storefront normalization. Review recommended.",
                "Low" => "Fuzzy title similarity only. Verify carefully.",
                _ => "Unclassified match."
            };
        }

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 8, 20, 8),
            Tag = "content"
        };
        gridHost.Controls.Add(_grid);

        var selectedLabel = new Label
        {
            AutoSize = true,
            Text = $"0 of {_items.Count} selected",
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = "muted",
            Margin = new Padding(0, 11, 10, 0)
        };

        void UpdateSelectedCount()
        {
            _grid.EndEdit();
            selectedLabel.Text = $"{SelectedItems.Count} of {_items.Count} selected";
        }

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0) UpdateSelectedCount();
        };

        var selectAll = MakeButton("Select all", (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.Rows) row.Cells[0].Value = true;
            UpdateSelectedCount();
        });
        var clear = MakeButton("Clear", (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.Rows) row.Cells[0].Value = false;
            UpdateSelectedCount();
        });
        var skip = MakeButton("Skip", (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        });
        var add = MakeButton("✓  Add selected as Native HDR", (_, _) =>
        {
            UpdateSelectedCount();
            if (SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Select at least one candidate first.", "TrueAuto HDR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        });
        add.Tag = "candidate-primary";
        add.AutoSize = true;
        add.Padding = new Padding(12, 0, 12, 0);

        var leftActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Tag = "footer"
        };
        leftActions.Controls.Add(selectAll);
        leftActions.Controls.Add(clear);
        leftActions.Controls.Add(selectedLabel);

        var rightActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Tag = "footer"
        };
        rightActions.Controls.Add(add);
        rightActions.Controls.Add(skip);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 10, 20, 12),
            Tag = "footer"
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        footer.Controls.Add(leftActions, 0, 0);
        footer.Controls.Add(rightActions, 1, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);

        AcceptButton = add;
        CancelButton = skip;
        ThemeManager.Apply(this, theme);
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(9, 0, 9, 0)
        };
        button.Click += click;
        return button;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Force WinForms to recalculate layout from the actual monitor DPI attached
        // to this HWND. This is especially important for apps launched during
        // Windows sign-in and opened from the tray later.
        if (AutoScaleMode == AutoScaleMode.Dpi)
        {
            SuspendLayout();
            PerformAutoScale();
            PerformLayout();
            ResumeLayout(true);
        }

        ThemeManager.ApplyWindowChrome(this, _theme);
    }

}
