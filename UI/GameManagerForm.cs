using System.Diagnostics;
using AutoHDR.Database;
using AutoHDR.GameWatcher;
using AutoHDR.Updates;
using AutoHDR.Rules;
using AutoHDR.Diagnostics;
using AutoHDR.Models;

namespace AutoHDR.UI;

public sealed class GameManagerForm : Form
{
    private readonly UnifiedGameDetector _games;
    private readonly HdrDatabase _database;
    private readonly CommunityHdrSources _community;
    private readonly SteamStoreHdrClient _steamStore;
    private readonly PcgwHdrListClient _pcgwHdr;
    private readonly DatabaseUpdater _databaseUpdater;
    private readonly HdrSourcesUpdater _hdrSourcesUpdater;
    private readonly FileLogger _logger;
    private readonly AppSettings _settings;
    private readonly StartupManager _startup;
    private readonly AppUpdateService _appUpdates;
    private readonly GameRuleStore _rules;
    private readonly DiagnosticsService _diagnostics;
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly Label _summary = new();
    private readonly Label _scanStatus = new();
    private readonly SlickProgressBar _scanProgress = new();
    private List<InstalledGame> _installed = new();
    private readonly Dictionary<string, string> _sourceHints = new(StringComparer.OrdinalIgnoreCase);

    public event Action? DatabaseChanged;

    public GameManagerForm(UnifiedGameDetector games, HdrDatabase database, CommunityHdrSources community, SteamStoreHdrClient steamStore, PcgwHdrListClient pcgwHdr, DatabaseUpdater databaseUpdater, HdrSourcesUpdater hdrSourcesUpdater, AppUpdateService appUpdates, GameRuleStore rules, DiagnosticsService diagnostics, FileLogger logger, AppSettings settings, StartupManager startup)
    {
        _games = games;
        _database = database;
        _community = community;
        _steamStore = steamStore;
        _pcgwHdr = pcgwHdr;
        _databaseUpdater = databaseUpdater;
        _hdrSourcesUpdater = hdrSourcesUpdater;
        _logger = logger;
        _settings = settings;
        _startup = startup;
        _appUpdates = appUpdates;
        _rules = rules;
        _diagnostics = diagnostics;

        // All layout values in this form are authored at 96 DPI.
        // Explicit DPI scaling avoids Font autoscaling producing a different
        // result when TrueAuto HDR starts with Windows before the desktop/DPI
        // environment has fully settled.
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        // WinForms/native child controls can still paint one default system-color
        // frame while their HWNDs are being created. Build that first frame fully
        // transparent, then reveal the finished themed form on the next UI cycle.
        Opacity = 0d;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();

        Text = "TrueAuto HDR 1.3.1 — Game Manager";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 700);
        ClientSize = new Size(1320, 820);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        _search.Width = 360;
        _search.PlaceholderText = "Search installed games…";
        _search.TextChanged += (_, _) => Populate();

        var settingsButton = CreateToolbarButton("⚙  Settings", (_, _) => ShowSettings());
        settingsButton.Name = "settingsButton";

        var refreshButton = CreateToolbarButton("↻  Refresh Games", async (sender, _) =>
        {
            if (sender is Button button) button.Enabled = false;
            _scanStatus.Text = "Refreshing installed games…";
            _scanProgress.StartIndeterminate();
            try
            {
                _installed = await Task.Run(() => _games.GetInstalledGames(true).ToList());
                ScanCommunityNames();
                Populate();
                _scanStatus.Text = "Installed games refreshed.";
            }
            catch (Exception ex)
            {
                _logger.Log($"Manual game refresh failed: {ex.Message}");
                _scanStatus.Text = "Game refresh failed. See log for details.";
            }
            finally
            {
                _scanProgress.StopAndHide();
                if (sender is Button refresh) refresh.Enabled = true;
            }
        });

        var title = new Label
        {
            Text = "Game Manager",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
            Tag = "header-title"
        };
        var subtitle = new Label
        {
            Text = "Manage HDR detection and your local game database",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            Tag = "muted"
        };
        var titleStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(16, 1, 0, 0),
            Tag = "header-layout"
        };
        titleStack.Controls.Add(title);
        titleStack.Controls.Add(subtitle);

        var searchHost = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "header-layout"
        };
        searchHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370));
        searchHost.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchHost.Controls.Add(new Panel { Dock = DockStyle.Fill, Tag = "header-layout" }, 0, 0);
        _search.Dock = DockStyle.Fill;
        _search.Margin = new Padding(0, 8, 10, 8);
        searchHost.Controls.Add(_search, 1, 0);
        refreshButton.Margin = new Padding(0, 6, 0, 6);
        searchHost.Controls.Add(refreshButton, 2, 0);

        var header = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(18, 14, 18, 12),
            Margin = new Padding(0),
            Tag = "header"
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        settingsButton.Margin = new Padding(0, 5, 0, 5);
        header.Controls.Add(settingsButton, 0, 0);
        header.Controls.Add(titleStack, 1, 0);
        header.Controls.Add(searchHost, 2, 0);

        var discovery = CreateActionSection(
            "◎",
            "Discover / verify",
            "Find HDR information using community sources and storefront metadata.",
            new[]
            {
                CreateActionCard("◉", "Scan community lists", "Match installed games using RHI and HDR Gaming DB.", "purple", async (_, _) => await ScanCommunityNamesWithProgressAsync()),
                CreateActionCard("S", "Verify selected", "Verify the selected game using its storefront or community sources.", "blue", async (_, _) => await VerifySelectedAsync()),
                CreateActionCard("S", "Scan installed games", "Check unknown games using Steam metadata and community sources.", "green", async (_, _) => await ScanInstalledGamesAsync()),
                CreateActionCard("↗", "Open PCGamingWiki", "Open the selected game on PCGamingWiki in your browser.", "neutral", (_, _) => OpenPcgwForSelected())
            });

        var localDb = CreateActionSection(
            "▤",
            "Local database",
            "Manage local HDR decisions and import or export your database.",
            new[]
            {
                CreateActionCard("↓", "Check database update", "Check PCGamingWiki first, then the separately maintained TrueAuto HDR database.", "blue", async (_, _) => await UpdateDatabaseAsync()),
                CreateActionCard("+", "Add standalone EXE", "Register a game executable that is not managed by a supported launcher.", "green", (_, _) => AddStandaloneExecutable()),
                CreateActionCard("✓", "Mark Native HDR", "Enable automatic Windows HDR for the selected game.", "amber", async (_, _) => await MarkSelectedAsync(true)),
                CreateActionCard("−", "Mark SDR / Disabled", "Never auto-enable HDR for the selected game.", "amber", async (_, _) => await MarkSelectedAsync(false)),
                CreateActionCard("⏱", "Per-game rules", "Set HDR enable delay, exit grace, or keep-HDR behavior.", "purple", (_, _) => EditSelectedRules()),
                CreateActionCard("↶", "Clear override", "Return the selected game to the bundled database decision.", "neutral", async (_, _) => await ClearOverrideAsync()),
                CreateActionCard("⇩", "Import JSON", "Import game decisions as local user overrides.", "neutral", async (_, _) => await ImportAsync()),
                CreateActionCard("⇧", "Export DB", "Export the merged HDR database to a JSON file.", "neutral", async (_, _) => await ExportAsync())
            });

        // Use a deterministic table instead of a vertical FlowLayoutPanel here.
        // Anchored fixed-width children inside a TopDown FlowLayoutPanel can collapse
        // or be laid out outside the visible client area on some WinForms/DPI setups.
        var actions = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 12, 16, 8),
            Margin = new Padding(0),
            Tag = "content"
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        discovery.Dock = DockStyle.Fill;
        discovery.Margin = new Padding(0, 0, 0, 6);
        localDb.Dock = DockStyle.Fill;
        localDb.Margin = new Padding(0, 6, 0, 0);
        actions.Controls.Add(discovery, 0, 0);
        actions.Controls.Add(localDb, 0, 1);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 34;
        _grid.Columns.Add("status", "TrueAuto HDR");
        _grid.Columns.Add("name", "Game");
        _grid.Columns.Add("store", "Store");
        _grid.Columns.Add("source", "HDR source");
        _grid.Columns.Add("identity", "Identity match");
        _grid.Columns.Add("confidence", "Confidence");
        _grid.Columns.Add("hint", "Other source hints");
        _grid.Columns[0].FillWeight = 34;
        _grid.Columns[1].FillWeight = 150;
        _grid.Columns[2].FillWeight = 42;
        _grid.Columns[3].FillWeight = 90;
        _grid.Columns[4].FillWeight = 90;
        _grid.Columns[5].FillWeight = 55;
        _grid.Columns[6].FillWeight = 115;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenPcgwForSelected(); };

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 0), Tag = "content" };
        gridHost.Controls.Add(_grid);

        var bottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 9, 18, 8), Tag = "footer" };
        _summary.Dock = DockStyle.Top;
        _summary.Height = 24;
        _summary.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _scanStatus.Dock = DockStyle.Fill;
        _scanStatus.Tag = "muted";
        _scanStatus.Text = "PCGamingWiki HDR list + Steam can verify HDR; other sources remain available as fallback/review.";
        _scanProgress.Dock = DockStyle.Bottom;
        _scanProgress.Margin = new Padding(0);
        bottom.Controls.Add(_scanStatus);
        bottom.Controls.Add(_scanProgress);
        bottom.Controls.Add(_summary);

        var root = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Margin = new Padding(0), Padding = new Padding(0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 380));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bottom, 0, 3);
        Controls.Add(root);

        // Theme the complete visual tree before the form is ever shown.
        // Applying it from Shown allowed one default white WinForms frame.
        ThemeManager.Apply(this, _settings.Theme);

        DpiChanged += (_, e) =>
        {
            _logger.Log($"Game Manager DPI changed: {e.DeviceDpiOld} -> {e.DeviceDpiNew}. Suggested={e.SuggestedRectangle}.");
            SuspendLayout();
            PerformLayout();
            ResumeLayout(true);
            Invalidate(true);
        };

        _settings.ThemeChanged += OnThemeChanged;
        _settings.RunAtStartupChanged += OnRunAtStartupChanged;
        FormClosed += (_, _) =>
        {
            _settings.ThemeChanged -= OnThemeChanged;
            _settings.RunAtStartupChanged -= OnRunAtStartupChanged;
        };
        Shown += async (_, _) =>
        {
            _logger.Log($"Game Manager shown: DeviceDpi={DeviceDpi}, AutoScaleMode={AutoScaleMode}, ClientSize={ClientSize.Width}x{ClientSize.Height}.");

            // Shown occurs after the native window exists, but the form is still
            // completely transparent. Give WinForms one UI turn to finish child
            // HWND creation/DPI layout/theme painting, then reveal the completed
            // frame. This prevents default-white child-control flashes.
            ThemeManager.Apply(this, _settings.Theme);
            PerformLayout();
            Invalidate(true);

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;

                ThemeManager.ApplyWindowChrome(this, _settings.Theme);
                PerformLayout();
                Invalidate(true);
                Update();
                Opacity = 1d;
                _logger.Log("Game Manager first-frame reveal completed.");
            }));

            _scanStatus.Text = "Loading installed games…";
            _scanProgress.StartIndeterminate();
            _grid.Enabled = false;

            try
            {
                var installed = await Task.Run(() => _games.GetInstalledGames().ToList());
                _installed = installed;
                ScanCommunityNames();
                Populate();
                _scanStatus.Text = "PCGamingWiki HDR list + Steam can verify HDR; other sources remain available as fallback/review.";
            }
            catch (Exception ex)
            {
                _logger.Log($"Initial installed-game load failed: {ex.Message}");
                _scanStatus.Text = "Could not load installed games. Use Refresh Games to try again.";
            }
            finally
            {
                _scanProgress.StopAndHide();
                _grid.Enabled = true;
            }
        };
    }

    private void OnThemeChanged(AppTheme theme)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnThemeChanged(theme))); return; }
        ThemeManager.Apply(this, theme);
    }

    private void OnRunAtStartupChanged(bool enabled)
    {
        // The settings dialog subscribes to this event while it is open.
    }

    private async Task UpdateDatabaseAsync()
    {
        _scanStatus.Text = "Checking PCGamingWiki HDR list…";
        _scanProgress.StartDeterminate(Math.Max(1, _installed.Count));

        try
        {
            var result = await _hdrSourcesUpdater.UpdateAsync(
                _settings.DatabaseManifestUrl,
                (current, total, status) =>
                {
                    _scanStatus.Text = status;
                    _scanProgress.SetProgress(current, Math.Max(1, total));
                });

            // Refresh the manager view because PCGW may have added newly-supported
            // installed games before the maintained DB update ran.
            _installed = _games.GetInstalledGames().ToList();
            ScanCommunityNames();
            Populate();
            DatabaseChanged?.Invoke();
            _scanStatus.Text =
                $"Sources update complete: PCGamingWiki added {result.PcgwAdded}; " +
                (result.Database.Updated
                    ? $"TrueAuto HDR DB updated to {result.Database.Version}."
                    : result.Database.Message);

            MessageBox.Show(
                this,
                result.Summary,
                "TrueAuto HDR — HDR sources update",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _scanProgress.StopAndHide();
        }
    }

    private void AddStandaloneExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add standalone game executable",
            Filter = "Windows executables (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var game = _games.AddStandaloneExecutable(dialog.FileName);
            _installed = _games.GetInstalledGames(true).ToList();
            ScanCommunityNames();
            Populate();

            var answer = MessageBox.Show(
                this,
                $"Added {game.Name} as a standalone game.\n\nDoes this game have native HDR support?",
                "TrueAuto HDR",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (answer == DialogResult.Yes)
            {
                _database.PutForInstalledGameAsync(game, true, "Manual standalone", "true").GetAwaiter().GetResult();
                DatabaseChanged?.Invoke();
                Populate();
            }
            else if (answer == DialogResult.No)
            {
                _database.PutForInstalledGameAsync(game, false, "Manual standalone", "false").GetAwaiter().GetResult();
                DatabaseChanged?.Invoke();
                Populate();
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Adding standalone executable failed: {ex}");
            MessageBox.Show(this, ex.Message, "Could not add executable", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowSettings()
    {
        using var settingsForm = new SettingsForm(_settings, _startup, _databaseUpdater, _hdrSourcesUpdater, _database, _appUpdates, _logger);
        ThemeManager.Apply(settingsForm, _settings.Theme);
        settingsForm.ShowDialog(this);
    }

    private static Button CreateToolbarButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 38,
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Tag = "toolbar-button"
        };
        button.FlatAppearance.BorderSize = 1;
        button.Click += click;
        return button;
    }

    private static Panel CreateActionSection(string icon, string title, string subtitle, IReadOnlyList<Panel> cards)
    {
        var section = new BufferedPanel
        {
            Margin = new Padding(0),
            Padding = new Padding(18, 14, 18, 14),
            Tag = "section"
        };
        section.Resize += (_, _) => section.Invalidate(true);
        section.Paint += (_, e) =>
        {
            var dark = ThemeManager.ControlIsDark(section);
            var border = dark ? Color.FromArgb(54, 62, 72) : Color.FromArgb(214, 220, 227);
            using var pen = new Pen(border);
            e.Graphics.DrawRectangle(pen, 0, 0, section.ClientSize.Width - 1, section.ClientSize.Height - 1);
        };

        var heading = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "section-layout"
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));

        var iconLabel = new Label
        {
            Text = icon,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold),
            Tag = "section-icon"
        };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            Tag = "section-title"
        };
        var subtitleLabel = new Label
        {
            Text = subtitle,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F),
            Tag = "muted"
        };
        heading.Controls.Add(iconLabel, 0, 0);
        heading.SetRowSpan(iconLabel, 2);
        heading.Controls.Add(titleLabel, 1, 0);
        heading.Controls.Add(subtitleLabel, 1, 1);

        var cardGrid = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = cards.Count,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 0),
            Margin = new Padding(0),
            Tag = "section-layout"
        };
        for (var i = 0; i < cards.Count; i++)
        {
            cardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / cards.Count));
            cards[i].Dock = DockStyle.Fill;
            cards[i].Margin = new Padding(i == 0 ? 0 : 6, 0, i == cards.Count - 1 ? 0 : 6, 0);
            cardGrid.Controls.Add(cards[i], i, 0);
        }

        section.Controls.Add(cardGrid);
        section.Controls.Add(heading);
        return section;
    }

    private static Panel CreateActionCard(string icon, string title, string subtitle, string accent, EventHandler click)
    {
        var card = new BufferedPanel
        {
            Height = 92,
            Padding = new Padding(14, 10, 12, 10),
            Cursor = Cursors.Hand,
            Tag = $"action-card:{accent}"
        };
        card.Resize += (_, _) => card.Invalidate(true);
        card.Paint += (_, e) =>
        {
            var dark = ThemeManager.ControlIsDark(card);
            var border = dark ? Color.FromArgb(58, 67, 77) : Color.FromArgb(211, 217, 224);
            using var borderPen = new Pen(border);
            e.Graphics.DrawRectangle(borderPen, 0, 0, card.ClientSize.Width - 1, card.ClientSize.Height - 1);
            using var accentPen = new Pen(ThemeManager.Accent(accent, dark), 3F);
            e.Graphics.DrawLine(accentPen, 1, 1, 1, card.ClientSize.Height - 2);
        };

        var layout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "card-layout"
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var iconLabel = new Label
        {
            Text = icon,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Symbol", 17F, FontStyle.Bold),
            Tag = $"card-icon:{accent}",
            Cursor = Cursors.Hand
        };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Tag = "card-title",
            Cursor = Cursors.Hand
        };
        var subtitleLabel = new Label
        {
            Text = subtitle,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 8.5F),
            AutoEllipsis = true,
            Tag = "muted",
            Cursor = Cursors.Hand
        };
        layout.Controls.Add(iconLabel, 0, 0);
        layout.SetRowSpan(iconLabel, 2);
        layout.Controls.Add(titleLabel, 1, 0);
        layout.Controls.Add(subtitleLabel, 1, 1);
        card.Controls.Add(layout);

        void Fire(object? sender, EventArgs e) => click(card, e);
        card.Click += Fire;
        foreach (Control child in layout.Controls) child.Click += Fire;
        layout.Click += Fire;

        card.MouseEnter += (_, _) => ThemeManager.SetCardHover(card, true);
        card.MouseLeave += (_, _) => ThemeManager.SetCardHover(card, false);
        foreach (Control child in layout.Controls)
        {
            child.MouseEnter += (_, _) => ThemeManager.SetCardHover(card, true);
            child.MouseLeave += (_, _) =>
            {
                var p = card.PointToClient(Cursor.Position);
                if (!card.ClientRectangle.Contains(p)) ThemeManager.SetCardHover(card, false);
            };
        }
        return card;
    }

    private void EditSelectedRules()
    {
        var game = SelectedGame();
        if (game is null)
        {
            MessageBox.Show(this, "Select a game first.", "Per-game rules",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new GameRuleForm(game, _rules.Get(game), _settings.Theme);
        ThemeManager.Apply(form, _settings.Theme);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        _rules.Set(game, form.Result);
        _scanStatus.Text = form.Result.EnableDelayMs == 0 &&
                           form.Result.ExitGraceMs == 0 &&
                           !form.Result.KeepHdrAfterExit &&
                           string.IsNullOrWhiteSpace(form.Result.DisplayDeviceName)
            ? $"{game.Name}: using default HDR behavior."
            : $"{game.Name}: per-game rules saved.";
    }

    private void ShowDiagnostics()
    {
        using var form = new DiagnosticsForm(_diagnostics, _settings.Theme);
        ThemeManager.Apply(form, _settings.Theme);
        form.ShowDialog(this);
    }

    private InstalledGame? SelectedGame() => _grid.SelectedRows.Count == 0 ? null : _grid.SelectedRows[0].Tag as InstalledGame;

    private void ScanCommunityNames()
    {
        foreach (var game in _installed)
        {
            var m = _community.Match(game.Name);
            if (m.Any) _sourceHints[game.Key] = m.Label;
        }
    }

    private async Task ScanCommunityNamesWithProgressAsync()
    {
        if (_installed.Count == 0)
        {
            _scanStatus.Text = "No installed games are loaded yet.";
            return;
        }

        _scanStatus.Text = $"Scanning community HDR lists… 0/{_installed.Count}";
        _scanProgress.StartDeterminate(_installed.Count);
        var matched = 0;
        var reviewCandidates = new List<CandidateReviewItem>();

        try
        {
            for (var i = 0; i < _installed.Count; i++)
            {
                var game = _installed[i];
                var m = _community.Match(game.Name);
                if (m.Any)
                {
                    _sourceHints[game.Key] = $"{m.Label}: {m.MatchedTitle} [{m.ConfidenceLabel}, {m.Score}%]";
                    matched++;

                    // Existing local/bundled decisions are informative matches,
                    // not candidates that need another user decision.
                    if (!_database.TryGet(game, out _))
                        reviewCandidates.Add(new CandidateReviewItem(game, m.Label, m.MatchedTitle, m.ConfidenceLabel, $"{m.MatchType} ({m.Score}%)"));
                }

                _scanProgress.SetProgress(i + 1, _installed.Count);
                _scanStatus.Text = $"Scanning community HDR lists… {i + 1}/{_installed.Count}";

                if ((i & 3) == 3) await Task.Yield();
            }

            Populate();
            _logger.Log($"Community HDR scan complete: matches={matched}, review-candidates={reviewCandidates.Count}.");
        }
        finally
        {
            _scanProgress.StopAndHide();
        }

        if (reviewCandidates.Count == 0)
        {
            _scanStatus.Text = matched == 0
                ? "Community scan complete: no HDR matches found."
                : $"Community scan complete: {matched} match{(matched == 1 ? "" : "es")} found, but all already have database decisions.";
            return;
        }

        _scanStatus.Text = $"Community scan complete: {reviewCandidates.Count} candidate{(reviewCandidates.Count == 1 ? "" : "s")} ready for review.";
        using var review = new CandidateReviewForm(reviewCandidates, _settings.Theme);
        if (review.ShowDialog(this) != DialogResult.OK)
        {
            _scanStatus.Text = $"Community candidate review skipped. {reviewCandidates.Count} candidate{(reviewCandidates.Count == 1 ? "" : "s")} remain unconfirmed.";
            return;
        }

        var selected = review.SelectedItems;
        foreach (var candidate in selected)
        {
            await _database.PutForInstalledGameAsync(
                candidate.Game,
                true,
                $"User-approved community candidate: {candidate.Source}",
                "community-candidate-approved");
            _logger.Log($"Community candidate approved: {candidate.Game.Name} [{candidate.Game.Store}:{candidate.Game.StoreId}] -> '{candidate.MatchedTitle}' via {candidate.Source}.");
        }

        if (selected.Count > 0)
        {
            DatabaseChanged?.Invoke();
            Populate();
            _scanStatus.Text = $"Added {selected.Count} reviewed community candidate{(selected.Count == 1 ? "" : "s")} as Native HDR.";
        }
        else
        {
            _scanStatus.Text = "Community candidate review finished; no games were added.";
        }
    }

    private void Populate()
    {
        var selectedId = SelectedGame()?.Key;
        var filter = _search.Text.Trim();
        _grid.Rows.Clear();

        foreach (var game in _installed.Where(g => filter.Length == 0 || g.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            var identity = _database.ResolveIdentity(game, includeMediumCandidates: true);
            var accepted = identity?.SafeForAutomaticUse == true;
            var entry = accepted ? identity!.Entry : null;
            var enabled = entry?.NativeHdr == true;

            var source = entry is null
                ? ""
                : identity!.IsUserEntry ? $"User ({entry.Source})" : entry.Source;

            var identityText = identity is null ? "" : $"{identity.MatchType}: {identity.MatchedName}";
            var confidence = identity?.ConfidenceLabel ?? "";

            _sourceHints.TryGetValue(game.Key, out var hint);

            if (!accepted && identity is not null)
            {
                var dbHint = $"DB candidate: {identity.MatchedName} ({identity.ConfidenceLabel}, {identity.MatchType}, {identity.Score}%)";
                hint = string.IsNullOrWhiteSpace(hint) ? dbHint : $"{hint} | {dbHint}";
            }

            var status = enabled ? "ON" : accepted ? "OFF" : (identity?.Entry.NativeHdr == true || !string.IsNullOrWhiteSpace(hint)) ? "Candidate" : "Unknown";
            var index = _grid.Rows.Add(status, game.Name, game.Store, source, identityText, confidence, hint ?? "");
            _grid.Rows[index].Tag = game;

            if (game.Key == selectedId) _grid.Rows[index].Selected = true;
        }

        var installedHdr = _installed.Count(g =>
        {
            var m = _database.ResolveIdentity(g, includeMediumCandidates: false);
            return m?.Entry.NativeHdr == true;
        });
        var candidates = _installed.Count(g =>
        {
            var m = _database.ResolveIdentity(g, includeMediumCandidates: true);
            return (m is not null && !m.SafeForAutomaticUse && m.Entry.NativeHdr) ||
                   (!_database.TryGet(g, out _) && _sourceHints.ContainsKey(g.Key));
        });

        _summary.Text = $"Installed: {_installed.Count}   •   HDR enabled: {installedHdr}   •   Candidates: {candidates}   •   DB: {_database.BundledCount}   •   User: {_database.UserCount}";
    }

    private async Task VerifySelectedAsync()
    {
        var game = SelectedGame();
        if (game is null) return;

        _scanProgress.StartIndeterminate();
        try
        {
            if (_database.TryGet(game, out var existing) && existing is not null)
            {
                _scanStatus.Text = $"{game.Name} already has a local HDR decision: {(existing.NativeHdr ? "Native HDR" : "disabled")} ({existing.Source}).";
                return;
            }

            var identityCandidate = _database.ResolveIdentity(game, includeMediumCandidates: true);
            if (identityCandidate is not null && !identityCandidate.SafeForAutomaticUse && identityCandidate.Entry.NativeHdr)
            {
                _sourceHints[game.Key] = $"DB candidate: {identityCandidate.MatchedName} ({identityCandidate.ConfidenceLabel}, {identityCandidate.MatchType}, {identityCandidate.Score}%)";
                Populate();
                using var review = new CandidateReviewForm(new[]
                {
                    new CandidateReviewItem(
                        game,
                        $"HDR database: {identityCandidate.Entry.Source}",
                        identityCandidate.MatchedName,
                        identityCandidate.ConfidenceLabel,
                        $"{identityCandidate.MatchType} ({identityCandidate.Score}%)")
                }, _settings.Theme);

                if (review.ShowDialog(this) == DialogResult.OK && review.SelectedItems.Count > 0)
                {
                    await _database.PutForInstalledGameAsync(game, identityCandidate.Entry.NativeHdr,
                        $"User-approved cross-store match: {identityCandidate.Entry.Source}",
                        "cross-store-approved");
                    DatabaseChanged?.Invoke();
                    Populate();
                    _scanStatus.Text = $"Approved cross-store identity for {game.Name}.";
                    return;
                }
            }

            _scanStatus.Text = $"Checking PCGamingWiki HDR list for {game.Name}…";
            var pcgw = await _pcgwHdr.CheckAsync(game.Name);
            if (pcgw.Success && pcgw.IsHdrSupported)
            {
                _sourceHints[game.Key] = $"PCGamingWiki: {pcgw.SupportLabel} — {pcgw.MatchedTitle}";
                await _database.PutForInstalledGameAsync(
                    game, true, $"PCGamingWiki HDR list: {pcgw.SupportLabel}", "pcgw-hdr-list");
                DatabaseChanged?.Invoke();
                Populate();
                _scanStatus.Text = $"PCGamingWiki lists {game.Name} as '{pcgw.SupportLabel}' — added as HDR supported.";
                return;
            }
            if (pcgw.Success && !string.IsNullOrWhiteSpace(pcgw.MatchedTitle))
                _sourceHints[game.Key] = $"PCGamingWiki possible match: {pcgw.MatchedTitle} ({pcgw.Detail})";

            if (game.IsSteam)
            {
                _scanStatus.Text = $"Checking Steam Store for {game.Name}…";
                var result = await _steamStore.CheckAsync(game.StoreId);
                if (result.Success && result.HdrAvailable)
                {
                    _sourceHints[game.Key] = result.Detail;
                    await AddSteamVerifiedAsync(game);
                    _scanStatus.Text = $"Steam marks {game.Name} as HDR available — added to the local database.";
                    Populate();
                    return;
                }
                if (result.Success) _sourceHints[game.Key] = "Steam Store: no HDR category";
            }

            var community = _community.Match(game.Name);
            if (community.Any)
            {
                _sourceHints[game.Key] = $"{community.Label}: {community.MatchedTitle} [{community.ConfidenceLabel}, {community.Score}%]";
                Populate();
                var answer = MessageBox.Show(this,
                    $"{community.Label} matched this installation to:\n\n{community.MatchedTitle}\n\nConfidence: {community.ConfidenceLabel} ({community.Score}%)\nMethod: {community.MatchType}\n\nAdd {game.Name} [{game.Store}] as Native HDR?",
                    "TrueAuto HDR verification", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    await _database.PutForInstalledGameAsync(game, true, $"Community verified: {community.Label}", "community-native-hdr");
                    DatabaseChanged?.Invoke();
                    Populate();
                    _scanStatus.Text = $"Added {game.Name} [{game.Store}] as Native HDR.";
                }
                else _scanStatus.Text = $"Community HDR match found for {game.Name}; left as a candidate.";
                return;
            }

            _scanStatus.Text = $"No automatic HDR verification found for {game.Name} [{game.Store}]. PCGamingWiki can still be checked manually.";
        }
        finally { _scanProgress.StopAndHide(); }
    }

    private async Task ScanInstalledGamesAsync()
    {
        var unknown = _installed.Where(g => !_database.TryGet(g, out _)).ToList();
        if (unknown.Count == 0) { _scanStatus.Text = "All installed games already have a database decision."; return; }

        var added = 0;
        var checkedCount = 0;
        var reviewCandidates = new List<CandidateReviewItem>();
        _scanProgress.StartDeterminate(unknown.Count);
        try
        {
            foreach (var game in unknown)
            {
                _scanStatus.Text = $"Game scan {checkedCount + 1}/{unknown.Count}: {game.Name} [{game.Store}]";
                var autoAdded = false;

                var pcgw = await _pcgwHdr.CheckAsync(game.Name);
                if (pcgw.Success && pcgw.IsHdrSupported)
                {
                    _sourceHints[game.Key] = $"PCGamingWiki: {pcgw.SupportLabel} — {pcgw.MatchedTitle}";
                    await _database.PutForInstalledGameAsync(
                        game, true, $"PCGamingWiki HDR list: {pcgw.SupportLabel}", "pcgw-hdr-list");
                    added++;
                    autoAdded = true;
                }
                else if (pcgw.Success && !string.IsNullOrWhiteSpace(pcgw.MatchedTitle))
                {
                    _sourceHints[game.Key] = $"PCGamingWiki possible match: {pcgw.MatchedTitle} ({pcgw.Detail})";
                }

                if (!autoAdded && game.IsSteam)
                {
                    var result = await _steamStore.CheckAsync(game.StoreId);
                    if (result.Success && result.HdrAvailable)
                    {
                        _sourceHints[game.Key] = result.Detail;
                        await AddSteamVerifiedAsync(game);
                        added++;
                        autoAdded = true;
                    }
                }

                if (!autoAdded)
                {
                    var identityCandidate = _database.ResolveIdentity(game, includeMediumCandidates: true);
                    if (identityCandidate is not null && !identityCandidate.SafeForAutomaticUse && identityCandidate.Entry.NativeHdr)
                    {
                        _sourceHints[game.Key] = $"DB candidate: {identityCandidate.MatchedName} [{identityCandidate.ConfidenceLabel}, {identityCandidate.Score}%]";
                        reviewCandidates.Add(new CandidateReviewItem(
                            game,
                            $"HDR database: {identityCandidate.Entry.Source}",
                            identityCandidate.MatchedName,
                            identityCandidate.ConfidenceLabel,
                            $"{identityCandidate.MatchType} ({identityCandidate.Score}%)"));
                    }
                    else
                    {
                        var community = _community.Match(game.Name);
                        if (community.Any)
                        {
                            _sourceHints[game.Key] = $"{community.Label}: {community.MatchedTitle} [{community.ConfidenceLabel}, {community.Score}%]";
                            reviewCandidates.Add(new CandidateReviewItem(game, community.Label, community.MatchedTitle, community.ConfidenceLabel, $"{community.MatchType} ({community.Score}%)"));
                        }
                    }
                }

                checkedCount++;
                _scanProgress.SetProgress(checkedCount, unknown.Count);
                if (game.IsSteam) await Task.Delay(180);
                else if ((checkedCount & 3) == 0) await Task.Yield();
            }

            _logger.Log($"Multi-store HDR scan complete: checked={checkedCount}, Steam-auto-added={added}, community-candidates={reviewCandidates.Count}.");
            DatabaseChanged?.Invoke();
            Populate();
        }
        finally
        {
            _scanProgress.StopAndHide();
        }

        if (reviewCandidates.Count == 0)
        {
            _scanStatus.Text = $"Scan complete: checked {checkedCount}, added {added}; no community candidates need review.";
            return;
        }

        _scanStatus.Text = $"Scan complete: {reviewCandidates.Count} candidate{(reviewCandidates.Count == 1 ? "" : "s")} ready for review.";
        using var review = new CandidateReviewForm(reviewCandidates, _settings.Theme);
        if (review.ShowDialog(this) != DialogResult.OK)
        {
            _scanStatus.Text = $"Candidate review skipped. {reviewCandidates.Count} candidate{(reviewCandidates.Count == 1 ? "" : "s")} remain unconfirmed.";
            return;
        }

        var selected = review.SelectedItems;
        if (selected.Count == 0)
        {
            _scanStatus.Text = "Candidate review finished; no games were added.";
            return;
        }

        foreach (var candidate in selected)
        {
            await _database.PutForInstalledGameAsync(candidate.Game, true, $"User-approved candidate: {candidate.Source}", "candidate-approved-native-hdr");
            _logger.Log($"Candidate approved as Native HDR: {candidate.Game.Name} [{candidate.Game.Store}:{candidate.Game.StoreId}] via {candidate.Source}.");
        }

        DatabaseChanged?.Invoke();
        Populate();
        _scanStatus.Text = $"Added {selected.Count} reviewed candidate{(selected.Count == 1 ? "" : "s")} as Native HDR.";
    }

    private async Task AddSteamVerifiedAsync(InstalledGame game)
    {
        await _database.PutForInstalledGameAsync(game, true, "Steam Store: HDR available", "steam-hdr-category");
        _logger.Log($"Steam HDR verified: {game.Name} ({game.StoreId}).");
        DatabaseChanged?.Invoke();
    }

    private async Task MarkSelectedAsync(bool nativeHdr)
    {
        var game = SelectedGame(); if (game is null) return;
        var wording = nativeHdr ? "native HDR" : "SDR / do not auto-enable HDR";
        if (MessageBox.Show(this, $"Mark '{game.Name}' [{game.Store}] as {wording}?", "TrueAuto HDR override", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await _database.PutForInstalledGameAsync(game, nativeHdr, "manual", nativeHdr ? "manual-true" : "manual-false");
        _logger.Log($"User override: {game.Name} [{game.Store}:{game.StoreId}] NativeHdr={nativeHdr}.");
        DatabaseChanged?.Invoke(); Populate();
    }

    private async Task ClearOverrideAsync()
    {
        var game = SelectedGame(); if (game is null || !_database.IsUserEntry(game)) return;
        if (await _database.RemoveUserOverrideAsync(game))
        {
            _logger.Log($"Cleared user override: {game.Name} [{game.Store}:{game.StoreId}].");
            DatabaseChanged?.Invoke(); Populate();
        }
    }

    private async Task ImportAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", Title = "Import TrueAuto HDR entries" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var count = await _database.ImportUserEntriesAsync(dialog.FileName); MessageBox.Show(this, $"Imported {count} entries as user overrides.", "TrueAuto HDR"); DatabaseChanged?.Invoke(); Populate(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ExportAsync()
    {
        using var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "trueautohdr_merged_database.json", Title = "Export TrueAuto HDR database" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await _database.ExportMergedAsync(dialog.FileName); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenPcgwForSelected()
    {
        var game = SelectedGame(); if (game is null) return;
        OpenUrl($"https://www.pcgamingwiki.com/w/index.php?search={Uri.EscapeDataString(game.Name)}");
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { _logger.Log($"Could not open browser: {ex.Message}"); }
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

        ThemeManager.ApplyWindowChrome(this, _settings.Theme);
    }

}
