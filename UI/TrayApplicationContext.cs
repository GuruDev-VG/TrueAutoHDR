using AutoHDR.Database;
using AutoHDR.GameWatcher;
using AutoHDR.Updates;

namespace AutoHDR.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly Icon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _dbItem;
    private readonly GameProcessWatcher _watcher;
    private readonly HdrDatabase _database;
    private readonly UnifiedGameDetector _games;
    private readonly FileLogger _logger;
    private readonly Lazy<CommunityHdrSources> _community;
    private readonly Lazy<SteamStoreHdrClient> _steamStore;
    private readonly Lazy<PcgwHdrListClient> _pcgwHdr;
    private readonly DatabaseUpdater _databaseUpdater;
    private readonly HdrSourcesUpdater _hdrSourcesUpdater;
    private readonly AppSettings _settings;
    private readonly AppUpdateService _appUpdates;
    private readonly StartupManager _startup;
    private GameManagerForm? _manager;
    private System.Windows.Forms.Timer? _onboardingTimer;

    public TrayApplicationContext(GameProcessWatcher watcher, UnifiedGameDetector games, HdrDatabase database, Lazy<CommunityHdrSources> community, Lazy<SteamStoreHdrClient> steamStore, Lazy<PcgwHdrListClient> pcgwHdr, DatabaseUpdater databaseUpdater, HdrSourcesUpdater hdrSourcesUpdater, AppUpdateService appUpdates, FileLogger logger, AppSettings settings, StartupManager startup, bool startupMode)
    {
        _watcher = watcher; _games = games; _database = database; _community = community; _steamStore = steamStore; _pcgwHdr = pcgwHdr; _databaseUpdater = databaseUpdater; _hdrSourcesUpdater = hdrSourcesUpdater; _appUpdates = appUpdates; _logger = logger; _settings = settings; _startup = startup;
        _statusItem = new ToolStripMenuItem("Starting…") { Enabled = false };
        _dbItem = new ToolStripMenuItem($"HDR database: {database.Count} games") { Enabled = false };
        var manage = new ToolStripMenuItem("Manage games…", null, (_, _) => ShowManager());
        var openDb = new ToolStripMenuItem("Open local database", null, (_, _) => OpenFile(_database.UserDatabasePath));
        var openLog = new ToolStripMenuItem("Open log", null, (_, _) => OpenFile(_logger.Path));
        var updateApp = new ToolStripMenuItem("Check for app update", null, async (_, _) => await CheckAppUpdateAsync());
        var updateDb = new ToolStripMenuItem("Check database update", null, async (_, _) => await UpdateDatabaseAsync());
        var firstRunItem = new ToolStripMenuItem("Run setup wizard…", null, (_, _) => ShowOnboarding(force: true));
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings());
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_statusItem); _menu.Items.Add(_dbItem); _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(manage); _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(openDb); _menu.Items.Add(updateApp); _menu.Items.Add(updateDb); _menu.Items.Add(openLog); _menu.Items.Add(firstRunItem); _menu.Items.Add(settingsItem); _menu.Items.Add(new ToolStripSeparator()); _menu.Items.Add(exit);
        _menu.CreateControl();

        _settings.ThemeChanged += theme => RunOnUi(() =>
        {
            ThemeManager.Apply(_menu, theme);
            if (_manager is not null && !_manager.IsDisposed) ThemeManager.Apply(_manager, theme);
        });
        ThemeManager.Apply(_menu, _settings.Theme);

        _trayIcon = AppIcon.Create();
        _tray = new NotifyIcon { Icon = _trayIcon, Text = "TrueAuto HDR 1.2.3", Visible = true, ContextMenuStrip = _menu };
        _tray.DoubleClick += (_, _) => ShowManager();
        watcher.StatusChanged += text => RunOnUi(() => _statusItem.Text = text);
        watcher.Start(startupMode);
        if (startupMode)
            _logger.Log("TrueAuto HDR launched with --startup (headless startup mode).");
        else if (!_settings.OnboardingCompleted)
        {
            // Do not BeginInvoke the onboarding form from the ApplicationContext
            // constructor. On a completely fresh portable run the WinForms
            // message loop is not guaranteed to be pumping yet.
            _onboardingTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _onboardingTimer.Tick += (_, _) =>
            {
                _onboardingTimer?.Stop();
                _onboardingTimer?.Dispose();
                _onboardingTimer = null;
                try
                {
                    ShowOnboarding(force: false);
                    if (_settings.OnboardingCompleted) ShowManager();
                }
                catch (Exception ex)
                {
                    _logger.Log($"First-run onboarding failed: {ex}");
                    MessageBox.Show(
                        $"TrueAuto HDR could not open the first-run setup.\n\n{ex.Message}\n\nThe error was written to trueautohdr.log.",
                        "TrueAuto HDR",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };
            _onboardingTimer.Start();
        }
    }

    private void ShowManager()
    {
        if (!_settings.OnboardingCompleted)
        {
            ShowOnboarding(force: false);
            if (!_settings.OnboardingCompleted) return;
        }

        if (_manager is null || _manager.IsDisposed)
        {
            _manager = new GameManagerForm(_games, _database, _community.Value, _steamStore.Value, _pcgwHdr.Value, _databaseUpdater, _hdrSourcesUpdater, _appUpdates, _logger, _settings, _startup);
            _manager.DatabaseChanged += () => RunOnUi(() => _dbItem.Text = $"HDR database: {_database.Count} games");
        }
        ThemeManager.Apply(_manager, _settings.Theme);
        _manager.Show(); _manager.BringToFront(); _manager.Activate();
    }

    private void ShowOnboarding(bool force)
    {
        if (!force && _settings.OnboardingCompleted) return;

        try
        {
            using var wizard = new FirstRunWizardForm(
                _games,
                _database,
                _community.Value,
                _steamStore.Value,
                _pcgwHdr.Value,
                _startup,
                _settings,
                _logger);
            ThemeManager.Apply(wizard, _settings.Theme);
            wizard.ShowDialog();
            _dbItem.Text = $"HDR database: {_database.Count} games";
        }
        catch (Exception ex)
        {
            _logger.Log($"Onboarding UI failed but tray will remain running: {ex}");
            MessageBox.Show(
                $"The setup wizard encountered an error, but TrueAuto HDR will keep running in the tray.\n\n{ex.Message}\n\nOpen the log from the tray menu for details.",
                "TrueAuto HDR setup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }



    private async Task CheckAppUpdateAsync()
    {
        _statusItem.Text = $"Checking {_settings.UpdateChannel} app updates…";
        var update = await _appUpdates.CheckAsync(_settings.UpdateChannel, _settings.CurrentAppUpdateManifestUrl);
        _statusItem.Text = "Watching for games…";

        if (!update.Success || !update.UpdateAvailable)
        {
            MessageBox.Show(update.Message, "TrueAuto HDR — App update",
                MessageBoxButtons.OK, update.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return;
        }

        var notes = string.IsNullOrWhiteSpace(update.Notes) ? "" : $"\n\n{update.Notes}";
        if (MessageBox.Show(
            $"{update.ReleaseType} {update.Version} is available.{notes}\n\nDownload, verify and apply this update now?",
            "TrueAuto HDR — App update",
            MessageBoxButtons.YesNo,
            _settings.UpdateChannel == AppUpdateChannel.Canary ? MessageBoxIcon.Warning : MessageBoxIcon.Information)
            != DialogResult.Yes) return;

        _statusItem.Text = "Downloading app update…";
        var staged = await _appUpdates.DownloadAndStageAsync(update);
        MessageBox.Show(staged.Message, "TrueAuto HDR — App update",
            MessageBoxButtons.OK, staged.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (staged.Success) Application.Exit();
        else _statusItem.Text = "Watching for games…";
    }

    private async Task UpdateDatabaseAsync()
    {
        _statusItem.Text = "Checking PCGamingWiki HDR list…";
        var result = await _hdrSourcesUpdater.UpdateAsync(
            _settings.DatabaseManifestUrl,
            (current, total, status) => RunOnUi(() => _statusItem.Text = status));

        RunOnUi(() =>
        {
            _dbItem.Text = $"HDR database: {_database.Count} games";
            _statusItem.Text = "Watching for games…";
            MessageBox.Show(
                result.Summary,
                "TrueAuto HDR — HDR sources update",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        });
    }

    private void ShowSettings()
    {
        using var settings = new SettingsForm(_settings, _startup, _databaseUpdater, _hdrSourcesUpdater, _database, _appUpdates, _logger);
        ThemeManager.Apply(settings, _settings.Theme);
        settings.ShowDialog();
    }

    private void RunOnUi(Action action)
    {
        try
        {
            if (_menu.IsDisposed) return;
            if (_menu.InvokeRequired) _menu.BeginInvoke(action); else action();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException ex) { _logger.Log($"UI marshal failed: {ex.Message}"); }
    }

    private void OpenFile(string path)
    {
        try
        {
            if (!File.Exists(path)) File.WriteAllText(path, path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "{}" : "");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { _logger.Log($"Could not open file: {ex.Message}"); }
    }

    protected override void ExitThreadCore()
    {
        _onboardingTimer?.Stop(); _onboardingTimer?.Dispose(); _watcher.Dispose(); _manager?.Dispose(); _tray.Visible = false; _tray.Dispose(); _trayIcon.Dispose(); _menu.Dispose(); base.ExitThreadCore();
    }
}
