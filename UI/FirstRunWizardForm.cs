using AutoHDR.Database;
using AutoHDR.GameWatcher;
using AutoHDR.Models;

namespace AutoHDR.UI;

public sealed class FirstRunWizardForm : Form
{
    private readonly UnifiedGameDetector _games;
    private readonly HdrDatabase _database;
    private readonly CommunityHdrSources _community;
    private readonly SteamStoreHdrClient _steamStore;
    private readonly PcgwHdrListClient _pcgwHdr;
    private readonly StartupManager _startup;
    private readonly AppSettings _settings;
    private readonly FileLogger _logger;

    private readonly Panel _content = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private readonly Label _stepLabel = new();
    private readonly SlickProgressBar _progress = new();
    private readonly DataGridView _candidateGrid = new();
    private readonly Label _scanStatus = new();
    private readonly Label _scanSummary = new();
    private readonly CheckBox _startupCheck = new();

    private readonly List<CandidateReviewItem> _candidates = new();
    private int _step;
    private bool _scanFinished;
    private int _installedCount;
    private int _knownHdrCount;
    private int _steamVerifiedCount;

    public FirstRunWizardForm(
        UnifiedGameDetector games,
        HdrDatabase database,
        CommunityHdrSources community,
        SteamStoreHdrClient steamStore,
        PcgwHdrListClient pcgwHdr,
        StartupManager startup,
        AppSettings settings,
        FileLogger logger)
    {
        _games = games;
        _database = database;
        _community = community;
        _steamStore = steamStore;
        _pcgwHdr = pcgwHdr;
        _startup = startup;
        _settings = settings;
        _logger = logger;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "Welcome to TrueAuto HDR";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 600);
        ClientSize = new Size(920, 650);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var header = BuildHeader();
        var footer = BuildFooter();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "content"
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_content, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);

        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(28, 20, 28, 16);
        _content.Tag = "content";

        _back.Text = "Back";
        _back.AutoSize = true;
        _back.Enabled = false;
        _back.Click += (_, _) => ShowStep(Math.Max(0, _step - 1));

        _next.Text = "Start setup";
        _next.AutoSize = true;
        _next.Padding = new Padding(12, 0, 12, 0);
        _next.Tag = "candidate-primary";
        _next.Click += async (_, _) => await NextAsync();

        Shown += (_, _) =>
        {
            ThemeManager.Apply(this, _settings.Theme);
            ShowStep(0);
        };
    }

    private Control BuildHeader()
    {
        var iconBox = new PictureBox
        {
            Size = new Size(54, 54),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = AppIcon.Create().ToBitmap(),
            Margin = new Padding(0, 3, 14, 0)
        };

        var title = new Label
        {
            Text = "TrueAuto HDR",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 1),
            Tag = "header-title"
        };

        var subtitle = new Label
        {
            Text = "Native HDR, enabled only when the game needs it.",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            Tag = "muted"
        };

        var titleStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Tag = "header-layout"
        };
        titleStack.Controls.Add(title);
        titleStack.Controls.Add(subtitle);

        _stepLabel.Text = "Step 1 of 3";
        _stepLabel.AutoSize = true;
        _stepLabel.Anchor = AnchorStyles.Right;
        _stepLabel.Tag = "muted";
        _stepLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(24, 17, 24, 14),
            Margin = new Padding(0),
            Tag = "header"
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(iconBox, 0, 0);
        header.Controls.Add(titleStack, 1, 0);
        header.Controls.Add(_stepLabel, 2, 0);
        return header;
    }

    private Control BuildFooter()
    {
        var skip = new Button
        {
            Text = "Skip setup",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0)
        };
        skip.Click += (_, _) =>
        {
            if (MessageBox.Show(this,
                "Skip the first-run scan? You can scan your games later from Game Manager.",
                "TrueAuto HDR", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _settings.CompleteOnboarding();
            DialogResult = DialogResult.OK;
            Close();
        };

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Tag = "footer"
        };
        left.Controls.Add(skip);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0),
            Tag = "footer"
        };
        right.Controls.Add(_next);
        right.Controls.Add(_back);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 14, 24, 14),
            Margin = new Padding(0),
            Tag = "footer"
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.Controls.Add(left, 0, 0);
        footer.Controls.Add(right, 1, 0);
        return footer;
    }

    private async Task NextAsync()
    {
        if (_step == 0)
        {
            ShowStep(1);
            if (!_scanFinished)
                await RunCuratedScanAsync();
            return;
        }

        if (_step == 1)
        {
            if (!_scanFinished) return;
            ShowStep(2);
            return;
        }

        await FinishAsync();
    }

    private void ShowStep(int step)
    {
        _step = step;
        _stepLabel.Text = $"Step {step + 1} of 3";
        _back.Enabled = step > 0;
        _content.SuspendLayout();
        _content.Controls.Clear();

        switch (step)
        {
            case 0:
                _content.Controls.Add(BuildWelcomePage());
                _next.Text = "Scan my games  →";
                _next.Enabled = true;
                break;
            case 1:
                _content.Controls.Add(BuildScanPage());
                _next.Text = "Continue  →";
                _next.Enabled = _scanFinished;
                break;
            default:
                _content.Controls.Add(BuildFinishPage());
                _next.Text = "Finish";
                _next.Enabled = true;
                break;
        }

        _content.ResumeLayout(true);
        ThemeManager.Apply(this, _settings.Theme);
    }

    private Control BuildWelcomePage()
    {
        var title = Heading("A small utility with one job");
        var body = Body(
            "TrueAuto HDR watches for games that have native HDR support. When one starts, it enables Windows HDR on your Main Display only. When the last HDR game closes, it restores the display state you had before.");

        var feature1 = Feature("◉", "Main display only", "Secondary monitors are never changed.");
        var feature2 = Feature("✓", "Native HDR decisions", "Verified database/store matches can trigger automatically; uncertain matches require your approval.");
        var feature3 = Feature("⚡", "Quiet in the background", "The low-power watcher stays in the tray and avoids repeated launcher or disk scans.");

        var note = new Label
        {
            Text = "Next, TrueAuto HDR can scan your installed Steam, Epic, GOG, Xbox, Ubisoft, Rockstar, EA and standalone games. It will show you anything uncertain before adding it.",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 54,
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(12, 10, 12, 8),
            Tag = "section"
        };

        var stack = Stack();
        stack.Controls.Add(title);
        stack.Controls.Add(body);
        stack.Controls.Add(Spacer(9));
        stack.Controls.Add(feature1);
        stack.Controls.Add(feature2);
        stack.Controls.Add(feature3);
        stack.Controls.Add(Spacer(12));
        stack.Controls.Add(note);
        return stack;
    }

    private Control BuildScanPage()
    {
        var title = Heading("Scan installed games");
        var body = Body("HDR games found during setup appear below as they are discovered. Verified/database matches and Steam-confirmed games are already enabled; uncertain matches stay unchecked for you to review.");

        _scanStatus.AutoSize = false;
        _scanStatus.Height = 26;
        _scanStatus.Dock = DockStyle.Top;
        _scanStatus.Tag = "muted";

        _progress.Dock = DockStyle.Top;
        _progress.Height = 8;
        _progress.Margin = new Padding(0, 3, 0, 10);

        _scanSummary.AutoSize = false;
        _scanSummary.Height = 28;
        _scanSummary.Dock = DockStyle.Top;
        _scanSummary.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        ConfigureCandidateGrid();
        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
            Tag = "content"
        };
        gridHost.Controls.Add(_candidateGrid);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 148,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "content"
        };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        title.Dock = DockStyle.Fill;
        body.Dock = DockStyle.Fill;
        _scanStatus.Dock = DockStyle.Fill;
        _progress.Dock = DockStyle.Fill;
        _scanSummary.Dock = DockStyle.Fill;
        top.Controls.Add(title, 0, 0);
        top.Controls.Add(body, 0, 1);
        top.Controls.Add(_scanStatus, 0, 2);
        top.Controls.Add(_progress, 0, 3);
        top.Controls.Add(_scanSummary, 0, 4);

        var page = new Panel { Dock = DockStyle.Fill, Tag = "content" };
        page.Controls.Add(gridHost);
        page.Controls.Add(top);
        return page;
    }

    private Control BuildFinishPage()
    {
        var title = Heading("Ready to use");
        var approved = CheckedCandidateItems().Count;
        var body = Body(
            $"Setup found {_installedCount} installed games. {_knownHdrCount} already matched the native-HDR database and {_steamVerifiedCount} were confirmed by Steam during this scan. You selected {approved} additional candidate{(approved == 1 ? "" : "s")} to approve.");

        _startupCheck.Text = "Run TrueAuto HDR when I sign in to Windows";
        _startupCheck.AutoSize = true;
        _startupCheck.Checked = _settings.RunAtStartup;
        _startupCheck.Margin = new Padding(0, 8, 0, 5);

        var startupHint = new Label
        {
            Text = "Recommended. Startup mode is headless and intentionally delays background scanning for a few seconds while Windows finishes signing in.",
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Tag = "muted"
        };

        var usage = new Label
        {
            Text = "After setup, TrueAuto HDR lives in the notification area. Double-click its color-wheel icon to open Game Manager at any time.",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 54,
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(12, 10, 12, 8),
            Tag = "section"
        };

        var stack = Stack();
        stack.Controls.Add(title);
        stack.Controls.Add(body);
        stack.Controls.Add(Spacer(12));
        stack.Controls.Add(_startupCheck);
        stack.Controls.Add(startupHint);
        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(usage);
        return stack;
    }

    private async Task RunCuratedScanAsync()
    {
        _next.Enabled = false;
        _back.Enabled = false;
        _candidateGrid.Enabled = false;
        _candidates.Clear();
        _candidateGrid.Rows.Clear();
        _knownHdrCount = 0;
        _steamVerifiedCount = 0;

        try
        {
            _scanStatus.Text = "Finding installed games…";
            _progress.StartIndeterminate();

            var installed = await Task.Run(() => _games.GetInstalledGames(true).ToList());
            _installedCount = installed.Count;
            _progress.StartDeterminate(Math.Max(1, installed.Count));

            var processed = 0;
            foreach (var game in installed)
            {
                _scanStatus.Text = $"Checking {game.Name} [{game.Store}]… {processed + 1}/{installed.Count}";

                var identity = _database.ResolveIdentity(game, includeMediumCandidates: true);
                if (identity?.SafeForAutomaticUse == true)
                {
                    if (identity.Entry.NativeHdr)
                    {
                        _knownHdrCount++;
                        AddKnownHdrResult(
                            game,
                            identity.MatchedName,
                            identity.ConfidenceLabel,
                            identity.IsUserEntry ? $"User: {identity.Entry.Source}" : identity.Entry.Source,
                            $"Native HDR • {identity.MatchType}");
                    }
                }
                else
                {
                    var resultAdded = false;

                    if (identity is not null && identity.Entry.NativeHdr)
                    {
                        AddCandidate(new CandidateReviewItem(
                            game,
                            $"HDR database: {identity.Entry.Source}",
                            identity.MatchedName,
                            identity.ConfidenceLabel,
                            $"{identity.MatchType} ({identity.Score}%)"));
                        resultAdded = true;
                    }

                    if (!resultAdded)
                    {
                        var pcgw = await _pcgwHdr.CheckAsync(game.Name);
                        if (pcgw.Success && pcgw.IsHdrSupported)
                        {
                            await _database.PutForInstalledGameAsync(
                                game, true, $"PCGamingWiki HDR list: {pcgw.SupportLabel}", "pcgw-hdr-list");
                            _knownHdrCount++;
                            AddKnownHdrResult(
                                game,
                                pcgw.MatchedTitle,
                                "Verified",
                                "PCGamingWiki HDR list",
                                $"Native HDR • {pcgw.SupportLabel}");
                            resultAdded = true;
                        }
                    }

                    if (!resultAdded && game.IsSteam)
                    {
                        var steam = await _steamStore.CheckAsync(game.StoreId);
                        if (steam.Success && steam.HdrAvailable)
                        {
                            await _database.PutForInstalledGameAsync(
                                game, true, "Steam Store: HDR available", "steam-hdr-category");
                            _steamVerifiedCount++;
                            AddKnownHdrResult(
                                game,
                                game.Name,
                                "Verified",
                                "Steam Store",
                                "Native HDR • Steam verified");
                            resultAdded = true;
                        }
                    }

                    if (!resultAdded)
                    {
                        var community = _community.Match(game.Name);
                        if (community.Any)
                        {
                            AddCandidate(new CandidateReviewItem(
                                game,
                                community.Label,
                                community.MatchedTitle,
                                community.ConfidenceLabel,
                                $"{community.MatchType} ({community.Score}%)"));
                        }
                    }
                }

                processed++;
                _progress.SetProgress(processed, installed.Count);
                if (game.IsSteam) await Task.Delay(140);
                else if ((processed & 3) == 0) await Task.Yield();
            }

            _scanFinished = true;
            var positive = _knownHdrCount + _steamVerifiedCount;
            _scanStatus.Text = _candidates.Count == 0
                ? $"Scan complete — {positive} HDR game{(positive == 1 ? "" : "s")} ready."
                : $"Scan complete — {positive} HDR game{(positive == 1 ? "" : "s")} ready; review {_candidates.Count} candidate{(_candidates.Count == 1 ? "" : "s")}.";
            _scanSummary.Text = $"Installed: {_installedCount}   •   HDR ready: {positive}   •   Review: {_candidates.Count}";
            _logger.Log($"First-run curated scan complete: installed={_installedCount}, DB-known-HDR={_knownHdrCount}, Steam-verified={_steamVerifiedCount}, review={_candidates.Count}.");
        }
        catch (Exception ex)
        {
            _logger.Log($"First-run scan failed: {ex}");
            _scanStatus.Text = "The first-run scan did not finish. You can continue and scan again later from Game Manager.";
            _scanFinished = true;
        }
        finally
        {
            _progress.StopAndHide();
            _candidateGrid.Enabled = true;
            _back.Enabled = true;
            _next.Enabled = true;
        }
    }

    private void ConfigureCandidateGrid()
    {
        if (_candidateGrid.Columns.Count > 0) return;

        _candidateGrid.Dock = DockStyle.Fill;
        _candidateGrid.ReadOnly = false;
        _candidateGrid.AllowUserToAddRows = false;
        _candidateGrid.AllowUserToDeleteRows = false;
        _candidateGrid.AllowUserToResizeRows = false;
        _candidateGrid.RowHeadersVisible = false;
        _candidateGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _candidateGrid.MultiSelect = false;
        _candidateGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _candidateGrid.BorderStyle = BorderStyle.None;
        _candidateGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _candidateGrid.ColumnHeadersHeight = 36;
        _candidateGrid.RowTemplate.Height = 34;

        _candidateGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "approve",
            HeaderText = "Use",
            FillWeight = 24,
            TrueValue = true,
            FalseValue = false
        });
        _candidateGrid.Columns.Add("game", "Installed game");
        _candidateGrid.Columns.Add("store", "Store");
        _candidateGrid.Columns.Add("result", "Result");
        _candidateGrid.Columns.Add("matched", "Matched HDR entry");
        _candidateGrid.Columns.Add("confidence", "Confidence");
        _candidateGrid.Columns.Add("source", "Source");

        for (var i = 1; i < _candidateGrid.Columns.Count; i++) _candidateGrid.Columns[i].ReadOnly = true;
        _candidateGrid.Columns[1].FillWeight = 125;
        _candidateGrid.Columns[2].FillWeight = 42;
        _candidateGrid.Columns[3].FillWeight = 82;
        _candidateGrid.Columns[4].FillWeight = 120;
        _candidateGrid.Columns[5].FillWeight = 55;
        _candidateGrid.Columns[6].FillWeight = 88;

        _candidateGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_candidateGrid.IsCurrentCellDirty)
                _candidateGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    private void AddKnownHdrResult(
        InstalledGame game,
        string matchedTitle,
        string confidence,
        string source,
        string result)
    {
        var row = _candidateGrid.Rows.Add(
            true,
            game.Name,
            game.Store,
            result,
            string.IsNullOrWhiteSpace(matchedTitle) ? game.Name : matchedTitle,
            confidence,
            source);

        // Already verified/known HDR entries are informational and cannot be
        // accidentally unchecked during setup.
        _candidateGrid.Rows[row].Cells[0].ReadOnly = true;
        _candidateGrid.Rows[row].Cells[0].ToolTipText = "Already enabled by a verified or high-confidence HDR source.";
        _candidateGrid.Rows[row].Tag = null;
    }

    private void AddCandidate(CandidateReviewItem item)
    {
        if (_candidates.Any(c => c.Game.Key.Equals(item.Game.Key, StringComparison.OrdinalIgnoreCase))) return;
        _candidates.Add(item);
        var row = _candidateGrid.Rows.Add(
            false,
            item.Game.Name,
            item.Game.Store,
            "Review",
            string.IsNullOrWhiteSpace(item.MatchedTitle) ? item.Game.Name : item.MatchedTitle,
            item.Confidence,
            item.Source);
        _candidateGrid.Rows[row].Tag = item;
        _candidateGrid.Rows[row].Cells[5].ToolTipText = item.MatchType;
    }

    private IReadOnlyList<CandidateReviewItem> CheckedCandidateItems()
    {
        _candidateGrid.EndEdit();
        return _candidateGrid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is CandidateReviewItem &&
                        Convert.ToBoolean(r.Cells[0].Value ?? false))
            .Select(r => (CandidateReviewItem)r.Tag!)
            .ToList();
    }

    private async Task FinishAsync()
    {
        _next.Enabled = false;
        try
        {
            foreach (var candidate in CheckedCandidateItems())
            {
                await _database.PutForInstalledGameAsync(
                    candidate.Game,
                    true,
                    $"First-run approved: {candidate.Source}",
                    "first-run-approved");
            }

            if (_startupCheck.Checked != _settings.RunAtStartup)
            {
                if (_startup.SetEnabled(_startupCheck.Checked))
                    _settings.SetRunAtStartup(_startupCheck.Checked);
                else
                    MessageBox.Show(this,
                        "TrueAuto HDR could not change the Windows startup setting. You can try again later in Settings.",
                        "TrueAuto HDR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _settings.CompleteOnboarding();
            _logger.Log($"First-run setup completed. User-approved candidates={CheckedCandidateItems().Count}; startup={_settings.RunAtStartup}.");
            DialogResult = DialogResult.OK;
            Close();
        }
        finally
        {
            _next.Enabled = true;
        }
    }

    private static FlowLayoutPanel Stack() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Margin = new Padding(0),
        Padding = new Padding(0),
        Tag = "content"
    };

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 8),
        Tag = "header-title"
    };

    private static Label Body(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(800, 0),
        Font = new Font("Segoe UI", 10F),
        Margin = new Padding(0, 0, 0, 8),
        Tag = "muted"
    };

    private static Panel Feature(string icon, string title, string subtitle)
    {
        var p = new Panel
        {
            Width = 800,
            Height = 68,
            Padding = new Padding(12, 9, 12, 8),
            Margin = new Padding(0, 4, 0, 4),
            Tag = "section"
        };
        var iconLabel = new Label
        {
            Text = icon,
            Width = 42,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Symbol", 17F, FontStyle.Bold)
        };
        var t = new Label
        {
            Text = title,
            AutoSize = true,
            Location = new Point(52, 9),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
        };
        var st = new Label
        {
            Text = subtitle,
            AutoSize = true,
            Location = new Point(52, 34),
            Tag = "muted"
        };
        p.Controls.Add(st);
        p.Controls.Add(t);
        p.Controls.Add(iconLabel);
        return p;
    }

    private static Panel Spacer(int height) => new() { Width = 1, Height = height, Margin = new Padding(0), Tag = "content" };
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

        ThemeManager.ApplyWindowChrome(this, _settings.Theme);
    }

}
