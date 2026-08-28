using System.Diagnostics;
using AutoHDR.Database;
using AutoHDR.GameWatcher;
using AutoHDR.Updates;
using AutoHDR.Rules;
using AutoHDR.Diagnostics;
using AutoHDR.Models;
using AutoHDR.Mods;

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
    private readonly SteamArtworkService _artwork;
#if CANARY
    private readonly HdrModDiscoveryService _hdrMods;
#endif

    private readonly DataGridView _grid = new();
    private readonly ModernSearchBox _search = new();
    private readonly Label _summary = new();
    private readonly Label _scanStatus = new();
    private readonly SlickProgressBar _scanProgress = new();
    private readonly ArtworkBox _cover = new();
    private readonly Label _selectedTitle = new();
    private readonly PillLabel _selectedStore = new();
    private readonly PillLabel _nativeHdrBadge = new();
    private readonly PillLabel _hdr10Badge = new();
    private readonly ModernButton _overrideAuto = new();
    private readonly ModernButton _overrideHdr = new();
    private readonly ModernButton _overrideSdr = new();
    private readonly MetricStrip _metrics = new();
    private CancellationTokenSource? _artworkCts;
    private DateTime? _lastScanUtc;
    private List<InstalledGame> _installed = new();
    private readonly Dictionary<string, string> _sourceHints = new(StringComparer.OrdinalIgnoreCase);

    public event Action? DatabaseChanged;
    public event Action? ExitRequested;

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
        _artwork = new SteamArtworkService(logger);
#if CANARY
        _hdrMods = new HdrModDiscoveryService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueAutoHDR"), logger);
#endif

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Opacity = 0d;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();

        Text = $"TrueAuto HDR {Application.ProductVersion} — Game Manager";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 720);
        ClientSize = new Size(1640, 940);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var settingsButton = CreateModernButton("⚙  Settings", "secondary", (_, _) => ShowSettings());
        settingsButton.Width = 112;
        settingsButton.Height = 44;

        var title = new Label
        {
            Text = "Game Manager",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            Tag = "header-title",
            Margin = new Padding(0)
        };
        var subtitle = new Label
        {
            Text = "Manage HDR detection and your local game database",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            Tag = "muted",
            Margin = new Padding(0, 3, 0, 0)
        };
        var titleStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(24, 0, 0, 0),
            Tag = "header-layout"
        };
        titleStack.Controls.Add(title);
        titleStack.Controls.Add(subtitle);

        _search.PlaceholderText = "Search installed games…";
        _search.Dock = DockStyle.Fill;
        _search.Height = 44;
        _search.Margin = new Padding(0, 8, 10, 8);
        _search.TextChanged += (_, _) => Populate();

        var refreshGames = CreateModernButton("↻  Refresh", "secondary", async (sender, _) =>
        {
            if (sender is Button b) b.Enabled = false;
            try
            {
                _scanStatus.Text = "Refreshing installed games…";
                _installed = await Task.Run(() => _games.GetInstalledGames().ToList());
                ScanCommunityNames();
                Populate();
                _scanStatus.Text = "";
            }
            catch (Exception ex)
            {
                _logger.Log($"Installed-game refresh failed: {ex.Message}");
                _scanStatus.Text = "Refresh failed. See log for details.";
            }
            finally
            {
                if (sender is Button bb) bb.Enabled = true;
            }
        });
        refreshGames.Width = 118;
        refreshGames.Height = 44;
        refreshGames.Margin = new Padding(0, 8, 0, 8);

        var header = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(20, 13, 20, 12),
            Margin = new Padding(0),
            Tag = "header"
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        settingsButton.Margin = new Padding(0, 8, 0, 8);
        header.Controls.Add(settingsButton, 0, 0);
        header.Controls.Add(titleStack, 1, 0);
        header.Controls.Add(_search, 2, 0);
        header.Controls.Add(refreshGames, 3, 0);

        var scanLibrary = CreateModernButton("◎  Scan Library", "primary", async (sender, _) =>
        {
            if (sender is Button b) b.Enabled = false;
            try
            {
                _scanStatus.Text = "Scanning installed library…";
                _scanProgress.StartIndeterminate();
                _installed = await Task.Run(() => _games.GetInstalledGames(true).ToList());
                ScanCommunityNames();
                _lastScanUtc = DateTime.UtcNow;
                Populate();
                await ScanInstalledGamesAsync();
                _lastScanUtc = DateTime.UtcNow;
                UpdateMetrics();
            }
            catch (Exception ex)
            {
                _logger.Log($"Library scan failed: {ex}");
                _scanStatus.Text = "Library scan failed. See log for details.";
            }
            finally
            {
                _scanProgress.StopAndHide();
                if (sender is Button bb) bb.Enabled = true;
            }
        });
        scanLibrary.Width = 154;

        var verify = CreateModernButton("♢  Verify Selected", "secondary", async (_, _) => await VerifySelectedAsync());
        verify.Width = 168;
        var refreshData = CreateModernButton("↻  Refresh HDR Data", "secondary", async (_, _) => await UpdateDatabaseAsync());
        refreshData.Width = 190;
        var pcgw = CreateModernButton("↗  Open PCGamingWiki", "secondary", (_, _) => OpenPcgwForSelected());
        pcgw.Width = 204;

        _metrics.Dock = DockStyle.Fill;
        _metrics.Font = new Font("Segoe UI", 9F);
        _metrics.Margin = new Padding(28, 0, 0, 0);
        _metrics.Tag = "metric-strip";

        var discoveryButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "modern-surface-layout"
        };
        foreach (var b in new[] { scanLibrary, verify, refreshData, pcgw })
        {
            b.Margin = new Padding(0, 0, 12, 0);
            discoveryButtons.Controls.Add(b);
        }

        var discovery = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 13, 20, 13),
            Margin = new Padding(18, 8, 18, 8),
            Radius = 9,
            AccentRole = "surface",
            Tag = "section"
        };
        var discoveryLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "modern-surface-layout"
        };
        discoveryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        discoveryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        discoveryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        discoveryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var discoverLabel = new Label
        {
            Text = "DISCOVER & VERIFY",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular),
            Tag = "muted",
            Margin = new Padding(0)
        };
        discoveryLayout.Controls.Add(discoverLabel, 0, 0);
        discoveryLayout.SetColumnSpan(discoverLabel, 2);
        discoveryLayout.Controls.Add(discoveryButtons, 0, 1);
        discoveryLayout.Controls.Add(_metrics, 1, 1);
        discovery.Controls.Add(discoveryLayout);

        _cover.Dock = DockStyle.Fill;
        _cover.FillMode = ArtworkFillMode.Cover;
        _cover.Margin = new Padding(0);
        _cover.Tag = "artwork";
        _cover.Paint += (_, e) =>
        {
            if (_cover.Image is not null) return;
            var dark = ThemeManager.ControlIsDark(_cover);
            using var titleFont = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 7.5F, FontStyle.Regular);
            using var brush = new SolidBrush(ThemeManager.Accent("purple", dark));
            using var muted = new SolidBrush(dark ? Color.FromArgb(132, 141, 151) : Color.FromArgb(105, 112, 121));
            var hdr = "HDR";
            var size = e.Graphics.MeasureString(hdr, titleFont);
            e.Graphics.DrawString(hdr, titleFont, brush, (_cover.ClientSize.Width - size.Width) / 2F, Math.Max(10, (_cover.ClientSize.Height - size.Height) / 2F - 8));
            var sub = "GAME ART";
            var subSize = e.Graphics.MeasureString(sub, subFont);
            e.Graphics.DrawString(sub, subFont, muted, (_cover.ClientSize.Width - subSize.Width) / 2F, Math.Max(40, (_cover.ClientSize.Height + size.Height) / 2F - 4));
        };

        var coverHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            Margin = new Padding(0),
            Radius = 7,
            BorderThickness = 0,
            AccentRole = "input",
            Tag = "artwork-host"
        };
        coverHost.Controls.Add(_cover);

        _selectedTitle.Text = "Select a game";
        _selectedTitle.AutoEllipsis = true;
        _selectedTitle.Dock = DockStyle.Fill;
        _selectedTitle.TextAlign = ContentAlignment.MiddleLeft;
        _selectedTitle.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);

        _selectedStore.Text = "—";
        _selectedStore.Kind = "store";
        _selectedStore.Width = 88;
        _selectedStore.Height = 30;
        _selectedStore.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _selectedStore.Margin = new Padding(12, 8, 0, 0);

        ConfigureBadge(_nativeHdrBadge, "Unknown", "muted-badge");
        ConfigureBadge(_hdr10Badge, "—", "muted-badge");

        var titleRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "modern-card-layout"
        };
        _selectedTitle.AutoSize = true;
        _selectedTitle.Dock = DockStyle.None;
        _selectedTitle.Margin = new Padding(0, 2, 12, 0);
        _selectedStore.Margin = new Padding(0, 4, 0, 0);
        titleRow.Controls.Add(_selectedTitle);
        titleRow.Controls.Add(_selectedStore);

        var nativeLabel = new Label
        {
            Text = "Native HDR",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 9F),
            Tag = "muted"
        };
        var hdr10Label = new Label
        {
            Text = "HDR10+ Gaming",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 9F),
            Tag = "muted"
        };

        _nativeHdrBadge.Width = 104;
        _nativeHdrBadge.Height = 30;
        _nativeHdrBadge.Margin = new Padding(0, 4, 0, 0);
        _hdr10Badge.Width = 92;
        _hdr10Badge.Height = 30;
        _hdr10Badge.Margin = new Padding(0, 4, 0, 0);

        var nativeStatus = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "transparent-layout", BackColor = Color.Transparent, Padding = new Padding(0), Margin = new Padding(0) };
        nativeStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        nativeStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        nativeStatus.Controls.Add(nativeLabel, 0, 0);
        nativeStatus.Controls.Add(_nativeHdrBadge, 0, 1);

        var hdr10Status = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Tag = "transparent-layout", BackColor = Color.Transparent, Padding = new Padding(0), Margin = new Padding(0) };
        hdr10Status.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        hdr10Status.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        hdr10Status.Controls.Add(hdr10Label, 0, 0);
        hdr10Status.Controls.Add(_hdr10Badge, 0, 1);

        var statusDivider = new Panel
        {
            Dock = DockStyle.Fill,
            Width = 1,
            Margin = new Padding(0, 9, 20, 9),
            Tag = "status-divider"
        };

        var statusRow = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Tag = "transparent-layout",
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusRow.Controls.Add(nativeStatus, 0, 0);
        statusRow.Controls.Add(statusDivider, 1, 0);
        statusRow.Controls.Add(hdr10Status, 2, 0);

        var selectedInfo = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(20, 2, 8, 0),
            Margin = new Padding(0),
            Tag = "modern-card-layout"
        };
        selectedInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        selectedInfo.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        selectedInfo.Controls.Add(titleRow, 0, 0);
        selectedInfo.Controls.Add(statusRow, 0, 1);

        var hdrOptions = CreateModernButton("✳  HDR Options", "purple-outline", (_, _) => ShowHdrOptions());
        hdrOptions.Width = 170;
        hdrOptions.Height = 50;
        var rulesButton = CreateModernButton("☷  Per-game Rules", "secondary", (_, _) => EditSelectedRules());
        rulesButton.Width = 170;
        rulesButton.Height = 50;

        var selectedActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Tag = "modern-card-layout",
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        hdrOptions.Margin = new Padding(0, 39, 14, 0);
        rulesButton.Margin = new Padding(0, 39, 0, 0);
        selectedActions.Controls.Add(hdrOptions);
        selectedActions.Controls.Add(rulesButton);

        ConfigureOverrideButton(_overrideHdr, "☀  Set HDR", 138);
        ConfigureOverrideButton(_overrideSdr, "▣  Set SDR", 138);
        _overrideAuto.Text = "Clear override";
        _overrideAuto.Kind = "secondary";
        _overrideAuto.Width = 112;
        _overrideAuto.Height = 28;
        _overrideAuto.Radius = 6;
        _overrideAuto.Font = new Font("Segoe UI", 8.5F, FontStyle.Underline);
        _overrideAuto.Padding = new Padding(4, 0, 4, 0);
        _overrideAuto.Click += async (_, _) => await ApplyOverrideAsync(0);
        _overrideHdr.Click += async (_, _) => await ApplyOverrideAsync(1);
        _overrideSdr.Click += async (_, _) => await ApplyOverrideAsync(2);

        var overrideTitle = new Label
        {
            Text = "HDR override",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            Tag = "muted",
            Margin = new Padding(0, 0, 0, 8)
        };

        var overrideButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0),
            Tag = "transparent-layout",
            BackColor = Color.Transparent
        };
        _overrideHdr.Margin = new Padding(0, 0, 10, 0);
        _overrideSdr.Margin = new Padding(0);
        overrideButtons.Controls.Add(_overrideHdr);
        overrideButtons.Controls.Add(_overrideSdr);

        var overrideHint = new Label
        {
            Text = "Set how TrueAuto HDR should handle this game when it runs.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 8.5F),
            Tag = "muted",
            Margin = new Padding(0, 3, 0, 0)
        };

        _overrideAuto.Margin = new Padding(0, 0, 0, 0);

        var overridePanel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18, 14, 18, 6),
            Tag = "transparent-layout",
            BackColor = Color.Transparent
        };
        overridePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        overridePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        overridePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        overridePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        overridePanel.Controls.Add(overrideTitle, 0, 0);
        overridePanel.Controls.Add(overrideButtons, 0, 1);
        overridePanel.Controls.Add(_overrideAuto, 0, 2);
        overridePanel.Controls.Add(overrideHint, 0, 3);

        var selectedCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(18, 0, 18, 10),
            Radius = 9,
            AccentRole = "card",
            Tag = "selected-card"
        };
        var selectedLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "modern-card-layout"
        };
        selectedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        selectedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
        selectedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));
        selectedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        selectedLayout.Controls.Add(coverHost, 0, 0);
        selectedLayout.Controls.Add(selectedInfo, 1, 0);
        selectedLayout.Controls.Add(selectedActions, 2, 0);
        selectedLayout.Controls.Add(overridePanel, 3, 0);
        selectedCard.Controls.Add(selectedLayout);

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
        _grid.ColumnHeadersHeight = 40;
        _grid.RowTemplate.Height = 37;
        _grid.DefaultCellStyle.Padding = new Padding(12, 0, 12, 0);
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 12, 0);
        _grid.Columns.Add("status", "HDR");
        _grid.Columns.Add("name", "Game");
        _grid.Columns.Add("store", "Store");
        _grid.Columns.Add("source", "HDR source");
        _grid.Columns.Add("hdr10", "HDR10+ Gaming");
        _grid.Columns.Add("hints", "Other source hints");
        _grid.Columns[0].FillWeight = 22;
        _grid.Columns[1].FillWeight = 96;
        _grid.Columns[2].FillWeight = 50;
        _grid.Columns[3].FillWeight = 94;
        _grid.Columns[4].FillWeight = 58;
        _grid.Columns[5].FillWeight = 78;
        _grid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowHdrOptions(); };
        _grid.SelectionChanged += async (_, _) => await UpdateSelectedCardAsync();
        _grid.CellPainting += PaintStatusCell;

        var gridCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            Margin = new Padding(18, 0, 18, 8),
            Radius = 9,
            AccentRole = "card",
            Tag = "selected-card"
        };
        gridCard.Controls.Add(_grid);

        var addExe = CreateModernButton("⊕  Add EXE", "secondary", (_, _) => AddStandaloneExecutable());
        addExe.Width = 118;
        var import = CreateModernButton("⇩  Import", "secondary", async (_, _) => await ImportAsync());
        import.Width = 104;
        var export = CreateModernButton("↥  Export", "secondary", async (_, _) => await ExportAsync());
        export.Width = 104;
        var dbInfo = CreateModernButton("▤  Database Info", "secondary", (_, _) => ShowDatabaseInfo());
        dbInfo.Width = 142;

        var footerTools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Tag = "modern-footer-layout",
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        foreach (var b in new[] { addExe, import, export, dbInfo })
        {
            b.Height = 36;
            b.Margin = new Padding(0, 3, 10, 3);
            footerTools.Controls.Add(b);
        }

        var footerTitle = new Label
        {
            Text = "DATABASE & ADVANCED",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9F),
            Tag = "footer-heading-opaque",
            Margin = new Padding(0)
        };

        _summary.Dock = DockStyle.Fill;
        _summary.AutoSize = false;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Tag = "muted";
        _summary.Font = new Font("Segoe UI", 8.5F);
        _summary.Margin = new Padding(0);

        var readyBadge = new PillLabel
        {
            Text = "Ready",
            Kind = "positive",
            Width = 88,
            Height = 28,
            Font = new Font("Segoe UI Semibold", 8.5F),
            Margin = new Padding(8, 5, 0, 0)
        };

        var help = CreateModernButton("?", "purple-outline", (_, _) => ShowDatabaseInfo());
        help.Width = 42;
        help.Height = 36;
        help.Margin = new Padding(8, 3, 0, 3);

        _scanStatus.Text = "";
        _scanStatus.Dock = DockStyle.Fill;
        _scanStatus.TextAlign = ContentAlignment.MiddleRight;
        _scanStatus.Tag = "muted";
        _scanStatus.AutoEllipsis = true;
        _scanStatus.Font = new Font("Segoe UI", 8.5F);
        _scanStatus.Margin = new Padding(12, 0, 0, 0);

        var footer = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 5, 16, 5),
            Margin = new Padding(18, 0, 18, 8),
            Radius = 9,
            AccentRole = "footer",
            Tag = "selected-card"
        };
        var footerLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
            Tag = "modern-footer-layout"
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 500));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        footerLayout.Controls.Add(footerTitle, 0, 0);
        footerLayout.Controls.Add(footerTools, 1, 0);
        footerLayout.Controls.Add(readyBadge, 3, 0);
        footerLayout.Controls.Add(help, 4, 0);

        footerLayout.Controls.Add(_summary, 0, 1);
        footerLayout.SetColumnSpan(_summary, 2);
        footerLayout.Controls.Add(_scanStatus, 2, 1);
        footerLayout.SetColumnSpan(_scanStatus, 3);

        footer.Controls.Add(footerLayout);

        _scanProgress.Dock = DockStyle.Bottom;
        _scanProgress.Height = 3;
        footer.Controls.Add(_scanProgress);

        var root = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "content"
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(discovery, 0, 1);
        root.Controls.Add(selectedCard, 0, 2);
        root.Controls.Add(gridCard, 0, 3);
        root.Controls.Add(footer, 0, 4);
        Controls.Add(root);

        ThemeManager.Apply(this, _settings.Theme);
        UpdateMetrics();

        DpiChanged += (_, e) =>
        {
            _logger.Log($"Game Manager DPI changed: {e.DeviceDpiOld} -> {e.DeviceDpiNew}. Suggested={e.SuggestedRectangle}.");
            SuspendLayout(); PerformLayout(); ResumeLayout(true); Invalidate(true);
        };
        _settings.ThemeChanged += OnThemeChanged;
        _settings.RunAtStartupChanged += OnRunAtStartupChanged;
        FormClosing += OnManagerFormClosing;
        FormClosed += (_, _) =>
        {
            _settings.ThemeChanged -= OnThemeChanged;
            _settings.RunAtStartupChanged -= OnRunAtStartupChanged;
            _artworkCts?.Cancel();
            _artwork.Dispose();
#if CANARY
            _hdrMods.Dispose();
#endif
            _cover.Image?.Dispose();
        };

        Shown += async (_, _) =>
        {
            _logger.Log($"Game Manager shown: DeviceDpi={DeviceDpi}, AutoScaleMode={AutoScaleMode}, ClientSize={ClientSize.Width}x{ClientSize.Height}.");
            ThemeManager.Apply(this, _settings.Theme);
            PerformLayout(); Invalidate(true);
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                ThemeManager.ApplyWindowChrome(this, _settings.Theme);
                PerformLayout(); Invalidate(true); Update(); Opacity = 1d;
                _logger.Log("Game Manager first-frame reveal completed.");
            }));

            _scanStatus.Text = "Loading installed games…";
            _scanProgress.StartIndeterminate();
            _grid.Enabled = false;
            try
            {
                _installed = await Task.Run(() => _games.GetInstalledGames().ToList());
                ScanCommunityNames();
                Populate();
                _scanStatus.Text = "";
            }
            catch (Exception ex)
            {
                _logger.Log($"Initial installed-game load failed: {ex.Message}");
                _scanStatus.Text = "Could not load installed games. Use Scan Library to try again.";
            }
            finally
            {
                _scanProgress.StopAndHide();
                _grid.Enabled = true;
            }
        };
    }

    private static ModernButton CreateModernButton(string text, string kind, EventHandler click)
    {
        var button = new ModernButton
        {
            Text = text,
            Kind = kind,
            AutoSize = false,
            Height = 44,
            Tag = kind == "primary" ? "primary-button" : "toolbar-button"
        };
        button.Click += click;
        return button;
    }

    private static void ConfigureBadge(PillLabel label, string text, string tag)
    {
        label.Text = text;
        label.AutoSize = false;
        label.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        label.Tag = tag;
        label.Kind = tag switch
        {
            "positive-badge" => "positive",
            "negative-badge" => "negative",
            "hdr10-badge" => "hdr10",
            _ => "muted"
        };
    }

    private void SetBadge(PillLabel label, string text, string tag)
    {
        label.Text = text;
        label.Tag = tag;
        label.Kind = tag switch
        {
            "positive-badge" => "positive",
            "negative-badge" => "negative",
            "hdr10-badge" => "hdr10",
            _ => "muted"
        };
        label.Invalidate();
    }

    private async Task UpdateSelectedCardAsync()
    {
        var game = SelectedGame();
        _artworkCts?.Cancel();
        _artworkCts?.Dispose();
        _artworkCts = new CancellationTokenSource();
        var artworkCts = _artworkCts;

        if (game is null)
        {
            _selectedTitle.Text = "Select a game";
            _selectedStore.Text = "—";
            SetBadge(_nativeHdrBadge, "Unknown", "muted-badge");
            SetBadge(_hdr10Badge, "—", "muted-badge");
            SetOverrideButtonsEnabled(false);
            UpdateOverrideButtons(0);
            _cover.Image?.Dispose(); _cover.Image = null;
            return;
        }

        _selectedTitle.Text = game.Name;
        _selectedStore.Text = StoreDisplay(game.Store);
        _selectedStore.Width = Math.Clamp(TextRenderer.MeasureText(_selectedStore.Text, _selectedStore.Font).Width + 22, 78, 168);
        var identity = _database.ResolveIdentity(game, includeMediumCandidates: true);
        var accepted = identity?.SafeForAutomaticUse == true;
        var entry = identity?.Entry;
        if (accepted && entry is not null)
            SetBadge(_nativeHdrBadge, entry.NativeHdr ? "ON" : "OFF", entry.NativeHdr ? "positive-badge" : "negative-badge");
        else
            SetBadge(_nativeHdrBadge, "Unknown", "muted-badge");

        var hasHdr10Plus = _database.TryGetHdr10PlusGaming(game, out var hdr10Metadata);
        if (hasHdr10Plus)
            SetBadge(_hdr10Badge, "Supported", "hdr10-badge");
        else
            SetBadge(_hdr10Badge, "—", "muted-badge");

        SetOverrideButtonsEnabled(true);
        var overrideMode = _database.IsUserEntry(game)
            ? (_database.TryGet(game, out var local) && local?.NativeHdr == true ? 1 : 2)
            : 0;
        UpdateOverrideButtons(overrideMode);

        var old = _cover.Image;
        _cover.Image = null;
        old?.Dispose();
        if (game.IsSteam)
        {
            var image = await _artwork.GetAsync(game, artworkCts.Token);
            if (!artworkCts.IsCancellationRequested && !IsDisposed)
            {
                old = _cover.Image;
                _cover.Image = image;
                old?.Dispose();
            }
            else image?.Dispose();
        }
    }

    private void ConfigureOverrideButton(ModernButton button, string text, int width)
    {
        button.Text = text;
        button.Kind = "secondary";
        button.Width = width;
        button.Height = 42;
        button.Radius = 7;
        button.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        button.Padding = new Padding(12, 0, 12, 0);
    }

    private void SetOverrideButtonsEnabled(bool enabled)
    {
        _overrideAuto.Enabled = enabled;
        _overrideHdr.Enabled = enabled;
        _overrideSdr.Enabled = enabled;
    }

    private void UpdateOverrideButtons(int mode)
    {
        _overrideAuto.Kind = "secondary";
        _overrideHdr.Kind = mode == 1 ? "positive-outline" : "secondary";
        _overrideSdr.Kind = mode == 2 ? "purple-outline" : "secondary";
        _overrideAuto.Enabled = mode != 0 && _overrideHdr.Enabled;
        _overrideAuto.Invalidate();
        _overrideHdr.Invalidate();
        _overrideSdr.Invalidate();
    }

    private async Task ApplyOverrideAsync(int mode)
    {
        var game = SelectedGame();
        if (game is null) return;

        SetOverrideButtonsEnabled(false);
        try
        {
            if (mode == 0)
            {
                if (_database.IsUserEntry(game))
                    await _database.RemoveUserOverrideAsync(game);
                _logger.Log($"HDR override set to Automatic: {game.Name} [{game.Store}:{game.StoreId}].");
            }
            else
            {
                var nativeHdr = mode == 1;
                await _database.PutForInstalledGameAsync(
                    game,
                    nativeHdr,
                    "manual",
                    nativeHdr ? "manual-true" : "manual-false");
                _logger.Log($"HDR override changed: {game.Name} [{game.Store}:{game.StoreId}] NativeHdr={nativeHdr}.");
            }

            DatabaseChanged?.Invoke();
            Populate();
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not change HDR override: {ex}");
            MessageBox.Show(this, ex.Message, "HDR override", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetOverrideButtonsEnabled(true);
        }
    }

    private void PaintStatusCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || (e.ColumnIndex != 0 && e.ColumnIndex != 4))
            return;

        e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border | DataGridViewPaintParts.SelectionBackground);
        e.Handled = true;

        var graphics = e.Graphics;
        if (graphics is null) return;

        var value = Convert.ToString(e.FormattedValue) ?? "—";
        var supported = value == "✓";
        var dark = ThemeManager.ControlIsDark(_grid);

        if (!supported)
        {
            var muted = dark ? Color.FromArgb(150, 161, 173) : Color.FromArgb(105, 115, 126);
            TextRenderer.DrawText(
                graphics,
                "—",
                _grid.Font,
                e.CellBounds,
                muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return;
        }

        var accent = e.ColumnIndex == 4
            ? ThemeManager.Accent("purple", dark)
            : ThemeManager.Accent("green", dark);

        var diameter = Math.Min(18, Math.Max(14, e.CellBounds.Height - 18));
        var x = e.CellBounds.Left + (e.CellBounds.Width - diameter) / 2;
        var y = e.CellBounds.Top + (e.CellBounds.Height - diameter) / 2;
        var circle = new Rectangle(x, y, diameter, diameter);

        using var pen = new Pen(accent, 1.8f);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.DrawEllipse(pen, circle);

        if (e.ColumnIndex == 0)
        {
            var p1 = new PointF(x + diameter * 0.28f, y + diameter * 0.53f);
            var p2 = new PointF(x + diameter * 0.44f, y + diameter * 0.68f);
            var p3 = new PointF(x + diameter * 0.73f, y + diameter * 0.34f);
            using var checkPen = new Pen(accent, 1.8f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };
            graphics.DrawLines(checkPen, new[] { p1, p2, p3 });
        }
        else
        {
            using var iconFont = new Font("Segoe UI Symbol", 8.5F, FontStyle.Bold);
            TextRenderer.DrawText(
                graphics,
                "✳",
                iconFont,
                circle,
                accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private void ShowHdrOptions()
    {
        var game = SelectedGame();
        if (game is null) return;
        var match = _database.ResolveIdentity(game, includeMediumCandidates: true);
        _database.TryGetHdr10PlusGaming(game, out var hdr10Metadata);
#if CANARY
        IReadOnlyList<HdrModMatch>? mods = _hdrMods.GetMatches(game);
#else
        IReadOnlyList<HdrModMatch>? mods = null;
#endif
        using var form = new HdrOptionsForm(game, match, hdr10Metadata, mods, _settings.Theme, _logger);
        ThemeManager.Apply(form, _settings.Theme);
        form.ShowDialog(this);
    }

    private void ShowDatabaseInfo()
    {
        MessageBox.Show(this,
            $"Bundled entries: {_database.BundledCount}\nUser overrides: {_database.UserCount}\nMerged entries: {_database.Count}\n\nUser database:\n{_database.UserDatabasePath}\n\nBundled database:\n{_database.BundledDatabasePath ?? "(embedded/default)"}",
            "TrueAuto HDR — Database Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateMetrics()
    {
        var hdr = 0;
        var hdr10 = 0;
        foreach (var game in _installed)
        {
            var match = _database.ResolveIdentity(game, includeMediumCandidates: false);
            if (match?.Entry.NativeHdr == true) hdr++;
            if (_database.TryGetHdr10PlusGaming(game, out _)) hdr10++;
        }
        var scan = _lastScanUtc.HasValue ? _lastScanUtc.Value.ToLocalTime().ToString("HH:mm") : "—";
        _metrics.SetValues(scan, _installed.Count, hdr, hdr10);
    }

    private void OnManagerFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Only intercept an actual user click on the window close button.
        // Application shutdown, updater shutdown, Windows logoff, etc. must
        // continue without prompting or blocking termination.
        if (e.CloseReason != CloseReason.UserClosing)
            return;

        var behavior = _settings.CloseBehavior;

        if (behavior == WindowCloseBehavior.KeepRunning)
        {
            e.Cancel = true;
            Hide();
            _logger.Log("Game Manager closed to tray using remembered close behavior.");
            return;
        }

        if (behavior == WindowCloseBehavior.ExitApplication)
        {
            e.Cancel = true;
            _logger.Log("Game Manager requested full application exit using remembered close behavior.");
            BeginInvoke(new Action(() => ExitRequested?.Invoke()));
            return;
        }

        using var dialog = new CloseChoiceDialog(_settings.Theme);
        ThemeManager.Apply(dialog, _settings.Theme);
        var result = dialog.ShowDialog(this);

        if (result != DialogResult.OK)
        {
            e.Cancel = true;
            return;
        }

        if (dialog.RememberChoice)
        {
            _settings.SetCloseBehavior(dialog.Choice == CloseChoice.KeepRunning
                ? WindowCloseBehavior.KeepRunning
                : WindowCloseBehavior.ExitApplication);
        }

        e.Cancel = true;

        if (dialog.Choice == CloseChoice.KeepRunning)
        {
            Hide();
            _logger.Log("Game Manager closed to tray.");
        }
        else
        {
            _logger.Log("Game Manager requested full application exit.");
            BeginInvoke(new Action(() => ExitRequested?.Invoke()));
        }
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
            var extraSourceSummary = "";
#if CANARY
            var modRefresh = await _hdrMods.RefreshAsync();
            extraSourceSummary = $"\n\nCanary HDR mods: {modRefresh.Message}";
#endif
            _scanStatus.Text =
                $"Sources update complete: PCGamingWiki added {result.PcgwAdded}; " +
                (result.Database.Updated
                    ? $"TrueAuto HDR DB updated to {result.Database.Version}."
                    : result.Database.Message);

            MessageBox.Show(
                this,
                result.Summary + extraSourceSummary,
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
                           string.IsNullOrWhiteSpace(form.Result.DisplayDeviceName) &&
                           form.Result.DisplayRecovery == DisplayRecoveryMode.Off
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
            var source = entry is null ? "—" : identity!.IsUserEntry ? $"User · {entry.Source}" : entry.Source;
            var hdr10Supported = _database.TryGetHdr10PlusGaming(game, out var hdr10Metadata);
            var hdr10Text = hdr10Supported ? "✓" : "—";

            _sourceHints.TryGetValue(game.Key, out var hint);
            if (!accepted && identity is not null)
            {
                var dbHint = $"DB candidate: {identity.MatchedName} ({identity.ConfidenceLabel}, {identity.MatchType}, {identity.Score}%)";
                hint = string.IsNullOrWhiteSpace(hint) ? dbHint : $"{hint} | {dbHint}";
            }

            var status = enabled ? "✓" : "—";
            var index = _grid.Rows.Add(status, game.Name, StoreDisplay(game.Store), source, hdr10Text, hint ?? "—");
            _grid.Rows[index].Tag = game;
            var dark = ThemeManager.ControlIsDark(_grid);
            _grid.Rows[index].Cells[0].Style.ForeColor = enabled
                ? ThemeManager.Accent("green", dark)
                : (dark ? Color.FromArgb(154, 165, 177) : Color.FromArgb(100, 110, 122));
            if (hdr10Supported)
            {
                _grid.Rows[index].Cells[4].Style.ForeColor = ThemeManager.Accent("purple", dark);
            }
            _grid.Rows[index].Cells[0].ToolTipText = accepted
                ? $"Native HDR: {(enabled ? "supported" : "disabled/SDR")}. {source}"
                : hint ?? "No verified HDR identity yet.";
            _grid.Rows[index].Cells[3].ToolTipText = identity is null
                ? hint ?? ""
                : $"{identity.MatchType}: {identity.MatchedName} · {identity.ConfidenceLabel} · {identity.Score}%";
            if (hdr10Supported)
                _grid.Rows[index].Cells[4].ToolTipText = string.IsNullOrWhiteSpace(hdr10Metadata?.Hdr10PlusSource) ? "HDR10+ Gaming supported" : hdr10Metadata!.Hdr10PlusSource;
            _grid.Rows[index].Cells[5].ToolTipText = hint ?? "";

            if (game.Key == selectedId) _grid.Rows[index].Selected = true;
        }

        var installedHdr = _installed.Count(g => _database.ResolveIdentity(g, includeMediumCandidates: false)?.Entry.NativeHdr == true);
        var hdr10 = _installed.Count(g => _database.TryGetHdr10PlusGaming(g, out _));
        _summary.Text = $"Installed: {_installed.Count}   •   HDR enabled: {installedHdr}   •   HDR10+: {hdr10}   •   DB: {_database.Count}";
        UpdateMetrics();

        if (_grid.Rows.Count > 0 && _grid.SelectedRows.Count == 0)
            _grid.Rows[0].Selected = true;
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

    private static string StoreDisplay(string store)
    {
        if (store.Equals("Steam", StringComparison.OrdinalIgnoreCase)) return "●  Steam";
        if (store.Equals("Xbox", StringComparison.OrdinalIgnoreCase)) return "◉  Xbox";
        if (store.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return "▣  Microsoft Store";
        if (store.Equals("Epic", StringComparison.OrdinalIgnoreCase)) return "◆  Epic";
        if (store.Equals("Ubisoft", StringComparison.OrdinalIgnoreCase)) return "◌  Ubisoft";
        if (store.Equals("EA", StringComparison.OrdinalIgnoreCase)) return "EA";
        if (store.Equals("GOG", StringComparison.OrdinalIgnoreCase)) return "GOG";
        return store;
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
