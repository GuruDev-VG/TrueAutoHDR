using AutoHDR.Database;
using AutoHDR.Updates;

namespace AutoHDR.UI;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly StartupManager _startup;
    private readonly DatabaseUpdater _databaseUpdater;
    private readonly HdrSourcesUpdater _hdrSourcesUpdater;
    private readonly HdrDatabase _database;
    private readonly FileLogger _logger;
    private readonly AppUpdateService _appUpdates;
    private readonly ComboBox _updateChannel = new();
    private readonly TextBox _stableUpdateUrl = new();
    private readonly TextBox _canaryUpdateUrl = new();
    private readonly Button _checkAppUpdate = new();
    private readonly Button _rollbackUpdate = new();
    private readonly ComboBox _themeCombo = new();
    private readonly CheckBox _startupToggle = new();
    private readonly ComboBox _closeBehavior = new();
    private readonly TextBox _databaseUrl = new();
    private readonly Label _databaseVersion = new();
    private readonly Button _checkDatabase = new();
    private bool _syncing;

    public SettingsForm(AppSettings settings, StartupManager startup, DatabaseUpdater databaseUpdater, HdrSourcesUpdater hdrSourcesUpdater, HdrDatabase database, AppUpdateService appUpdates, FileLogger logger)
    {
        _settings = settings;
        _startup = startup;
        _databaseUpdater = databaseUpdater;
        _hdrSourcesUpdater = hdrSourcesUpdater;
        _database = database;
        _logger = logger;
        _appUpdates = appUpdates;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "TrueAuto HDR Settings";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 610);

        var title = new Label
        {
            Text = "Settings",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };

        var themeLabel = new Label { Text = "Theme", AutoSize = true, Anchor = AnchorStyles.Left };
        _themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _themeCombo.Dock = DockStyle.Fill;
        _themeCombo.Items.AddRange(Enum.GetNames<AppTheme>());
        _themeCombo.SelectedItem = _settings.Theme.ToString();
        _themeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing) return;
            if (Enum.TryParse<AppTheme>(_themeCombo.SelectedItem?.ToString(), out var theme))
                _settings.SetTheme(theme);
        };

        _startupToggle.Text = "Run TrueAuto HDR when I sign in to Windows";
        _startupToggle.AutoSize = true;
        _startupToggle.Checked = _settings.RunAtStartup;
        _startupToggle.CheckedChanged += (_, _) => ChangeStartupSetting();

        var startupHint = new Label
        {
            Text = "Startup uses the lightweight --startup mode: no manager window is created and game scanning waits briefly for the Windows desktop to settle.",
            AutoSize = true,
            MaximumSize = new Size(535, 0),
            Tag = "muted"
        };

        _closeBehavior.DropDownStyle = ComboBoxStyle.DropDownList;
        _closeBehavior.Dock = DockStyle.Fill;
        _closeBehavior.Items.AddRange(new object[]
        {
            "Ask every time",
            "Keep running in background",
            "Exit application"
        });
        _closeBehavior.SelectedIndex = _settings.CloseBehavior switch
        {
            WindowCloseBehavior.KeepRunning => 1,
            WindowCloseBehavior.ExitApplication => 2,
            _ => 0
        };
        _closeBehavior.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing) return;
            _settings.SetCloseBehavior(_closeBehavior.SelectedIndex switch
            {
                1 => WindowCloseBehavior.KeepRunning,
                2 => WindowCloseBehavior.ExitApplication,
                _ => WindowCloseBehavior.Ask
            });
        };

        var appUpdateTitle = new Label
        {
            Text = "Program updates",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold)
        };
        var appUpdateHint = new Label
        {
            Text = "Stable receives normal releases and hotfixes. Canary is opt-in and may contain experimental features or regressions.",
            AutoSize = true,
            MaximumSize = new Size(585, 0),
            Tag = "muted"
        };
        _updateChannel.DropDownStyle = ComboBoxStyle.DropDownList;
        _updateChannel.Items.AddRange(Enum.GetNames<AppUpdateChannel>());
        _updateChannel.SelectedItem = _settings.UpdateChannel.ToString();
        _updateChannel.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing) return;
            if (!Enum.TryParse<AppUpdateChannel>(_updateChannel.SelectedItem?.ToString(), out var channel)) return;
            if (channel == AppUpdateChannel.Canary &&
                MessageBox.Show(this,
                    "Canary builds are experimental and may contain regressions.\n\nSwitch to Canary updates?",
                    "Enable Canary channel",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                _syncing = true;
                _updateChannel.SelectedItem = _settings.UpdateChannel.ToString();
                _syncing = false;
                return;
            }
            _settings.SetUpdateChannel(channel);
        };

        _stableUpdateUrl.Dock = DockStyle.Fill;
        _stableUpdateUrl.PlaceholderText = "Stable manifest URL";
        _stableUpdateUrl.Text = _settings.StableUpdateManifestUrl;
        _canaryUpdateUrl.Dock = DockStyle.Fill;
        _canaryUpdateUrl.PlaceholderText = "Canary manifest URL";
        _canaryUpdateUrl.Text = _settings.CanaryUpdateManifestUrl;

        _checkAppUpdate.Text = "Check for app update";
        _checkAppUpdate.AutoSize = true;
        _checkAppUpdate.Anchor = AnchorStyles.Right;
        _checkAppUpdate.Click += async (_, _) => await CheckAppUpdateAsync();

        _rollbackUpdate.Text = "Rollback last update";
        _rollbackUpdate.AutoSize = true;
        _rollbackUpdate.Enabled = _appUpdates.CanRollback;
        _rollbackUpdate.Click += (_, _) => RollbackLastUpdate();

        var dbTitle = new Label
        {
            Text = "HDR source updates",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold)
        };
        var dbHint = new Label
        {
            Text = "Check for update first compares installed games with the live PCGamingWiki HDR list, then checks the separately maintained TrueAuto HDR JSON database below.",
            AutoSize = true,
            MaximumSize = new Size(535, 0),
            Tag = "muted"
        };

        _databaseUrl.Dock = DockStyle.Fill;
        _databaseUrl.PlaceholderText = "https://…/database-manifest.json";
        _databaseUrl.Text = _settings.DatabaseManifestUrl;
        _databaseUrl.Leave += (_, _) => _settings.SetDatabaseManifestUrl(_databaseUrl.Text);

        _databaseVersion.Text = $"Current database: {_databaseUpdater.CurrentVersion}  •  {_database.BundledCount} native-HDR entries";
        _databaseVersion.AutoSize = true;
        _databaseVersion.Anchor = AnchorStyles.Left;
        _databaseVersion.Tag = "muted";

        _checkDatabase.Text = "Check database update";
        _checkDatabase.AutoSize = true;
        _checkDatabase.Anchor = AnchorStyles.Right;
        _checkDatabase.Click += async (_, _) => await CheckDatabaseAsync();

        var close = new Button
        {
            Text = "Close",
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right
        };
        close.Click += (_, _) => _settings.SetDatabaseManifestUrl(_databaseUrl.Text);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 17,
            Padding = new Padding(18, 14, 18, 14)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 16; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, i is 3 or 7 or 14 ? 48 : 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        grid.Controls.Add(title, 0, 0); grid.SetColumnSpan(title, 2);
        grid.Controls.Add(themeLabel, 0, 1); grid.Controls.Add(_themeCombo, 1, 1);
        grid.Controls.Add(_startupToggle, 0, 2); grid.SetColumnSpan(_startupToggle, 2);
        grid.Controls.Add(startupHint, 0, 3); grid.SetColumnSpan(startupHint, 2);
        grid.Controls.Add(new Label { Text = "Closing X", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        grid.Controls.Add(_closeBehavior, 1, 4);

        grid.Controls.Add(appUpdateTitle, 0, 5); grid.SetColumnSpan(appUpdateTitle, 2);
        grid.Controls.Add(appUpdateHint, 0, 6); grid.SetColumnSpan(appUpdateHint, 2);
        grid.Controls.Add(new Label { Text = "Update channel", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        grid.Controls.Add(_updateChannel, 1, 7);
        grid.Controls.Add(new Label { Text = "Stable manifest", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        grid.Controls.Add(_stableUpdateUrl, 1, 8);
        grid.Controls.Add(new Label { Text = "Canary manifest", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        grid.Controls.Add(_canaryUpdateUrl, 1, 9);
        var updateButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Tag = "content"
        };
        updateButtons.Controls.Add(_checkAppUpdate);
        updateButtons.Controls.Add(_rollbackUpdate);
        grid.Controls.Add(updateButtons, 1, 10);

        grid.Controls.Add(dbTitle, 0, 11); grid.SetColumnSpan(dbTitle, 2);
        grid.Controls.Add(dbHint, 0, 12); grid.SetColumnSpan(dbHint, 2);
        grid.Controls.Add(new Label { Text = "HDR DB manifest", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 13);
        grid.Controls.Add(_databaseUrl, 1, 13);
        grid.Controls.Add(_databaseVersion, 0, 14);
        grid.Controls.Add(_checkDatabase, 1, 14);
        grid.Controls.Add(close, 1, 16);
        Controls.Add(grid);

        AcceptButton = close;
        CancelButton = close;

        _settings.ThemeChanged += OnThemeChanged;
        _settings.RunAtStartupChanged += OnStartupChanged;
        FormClosed += (_, _) =>
        {
            _settings.SetDatabaseManifestUrl(_databaseUrl.Text);
            _settings.SetAppUpdateManifestUrls(_stableUpdateUrl.Text, _canaryUpdateUrl.Text);
            _settings.ThemeChanged -= OnThemeChanged;
            _settings.RunAtStartupChanged -= OnStartupChanged;
        };
        Shown += (_, _) => ThemeManager.Apply(this, _settings.Theme);
    }


    private async Task CheckAppUpdateAsync()
    {
        _settings.SetAppUpdateManifestUrls(_stableUpdateUrl.Text, _canaryUpdateUrl.Text);
        _checkAppUpdate.Enabled = false;
        _checkAppUpdate.Text = $"Checking {_settings.UpdateChannel}…";
        try
        {
            var update = await _appUpdates.CheckAsync(_settings.UpdateChannel, _settings.CurrentAppUpdateManifestUrl);
            if (!update.Success || !update.UpdateAvailable)
            {
                MessageBox.Show(this, update.Message, "TrueAuto HDR — App update",
                    MessageBoxButtons.OK, update.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }

            var notes = string.IsNullOrWhiteSpace(update.Notes) ? "" : $"\n\n{update.Notes}";
            if (MessageBox.Show(this,
                $"{update.ReleaseType} {update.Version} is available.{notes}\n\nDownload, verify and apply this update now?",
                "TrueAuto HDR — App update",
                MessageBoxButtons.YesNo,
                _settings.UpdateChannel == AppUpdateChannel.Canary ? MessageBoxIcon.Warning : MessageBoxIcon.Information)
                != DialogResult.Yes) return;

            _checkAppUpdate.Text = "Downloading update…";
            var staged = await _appUpdates.DownloadAndStageAsync(update);
            MessageBox.Show(this, staged.Message, "TrueAuto HDR — App update",
                MessageBoxButtons.OK, staged.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            if (staged.Success) Application.Exit();
        }
        finally
        {
            _checkAppUpdate.Text = "Check for app update";
            _checkAppUpdate.Enabled = true;
        }
    }

    private void RollbackLastUpdate()
    {
        if (!_appUpdates.CanRollback)
        {
            MessageBox.Show(this, "No previous application backup is available.",
                "TrueAuto HDR — Rollback", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this,
            "Restore the application files from the most recent update backup?\n\n" +
            "Your settings, game rules, logs and HDR database will not be rolled back.",
            "TrueAuto HDR — Rollback",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var result = _appUpdates.StartRollback();
        MessageBox.Show(this, result.Message, "TrueAuto HDR — Rollback",
            MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (result.Success) Application.Exit();
    }

    private async Task CheckDatabaseAsync()
    {
        _settings.SetDatabaseManifestUrl(_databaseUrl.Text);
        _checkDatabase.Enabled = false;
        _checkDatabase.Text = "Checking PCGamingWiki…";
        try
        {
            var result = await _hdrSourcesUpdater.UpdateAsync(_settings.DatabaseManifestUrl);
            _databaseVersion.Text = $"Current database: {_databaseUpdater.CurrentVersion}  •  {_database.BundledCount} native-HDR entries";
            MessageBox.Show(
                this,
                result.Summary,
                "TrueAuto HDR — HDR sources update",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _checkDatabase.Text = "Check database update";
            _checkDatabase.Enabled = true;
        }
    }

    private void ChangeStartupSetting()
    {
        if (_syncing || _startupToggle.Checked == _settings.RunAtStartup) return;

        var desired = _startupToggle.Checked;
        if (_startup.SetEnabled(desired))
        {
            _settings.SetRunAtStartup(desired);
            return;
        }

        _syncing = true;
        _startupToggle.Checked = _settings.RunAtStartup;
        _syncing = false;
        MessageBox.Show(this,
            "TrueAuto HDR could not change the Windows startup setting. Check trueautohdr.log for details.",
            "TrueAuto HDR",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnThemeChanged(theme))); return; }
        _syncing = true;
        _themeCombo.SelectedItem = theme.ToString();
        _syncing = false;
        ThemeManager.Apply(this, theme);
    }

    private void OnStartupChanged(bool enabled)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnStartupChanged(enabled))); return; }
        _syncing = true;
        _startupToggle.Checked = enabled;
        _syncing = false;
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
