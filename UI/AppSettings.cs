using System.Text.Json;

using AutoHDR.Updates;

namespace AutoHDR.UI;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum WindowCloseBehavior
{
    Ask,
    KeepRunning,
    ExitApplication
}

public sealed class AppSettings
{
    public const string DefaultStableUpdateManifestUrl = "https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/stable.json";
    public const string DefaultCanaryUpdateManifestUrl = "https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/canary.json";
    public const string DefaultDatabaseManifestUrl = "https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/database.json";
    private readonly string _path;
    private readonly FileLogger _logger;

    public AppTheme Theme { get; private set; } = AppTheme.System;
    public bool RunAtStartup { get; private set; }
    public string DatabaseManifestUrl { get; private set; } = DefaultDatabaseManifestUrl;
    public bool OnboardingCompleted { get; private set; }
    public AppUpdateChannel UpdateChannel { get; private set; } = AppUpdateChannel.Stable;
    public string StableUpdateManifestUrl { get; private set; } = DefaultStableUpdateManifestUrl;
    public string CanaryUpdateManifestUrl { get; private set; } = DefaultCanaryUpdateManifestUrl;
    public WindowCloseBehavior CloseBehavior { get; private set; } = WindowCloseBehavior.Ask;
    public event Action<AppTheme>? ThemeChanged;
    public event Action<bool>? RunAtStartupChanged;
    public event Action<string>? DatabaseManifestUrlChanged;
    public event Action<AppUpdateChannel>? UpdateChannelChanged;

    public AppSettings(string path, FileLogger logger)
    {
        _path = path;
        _logger = logger;
        Load();
    }

    public void SetTheme(AppTheme theme)
    {
        if (Theme == theme) return;
        Theme = theme;
        Save();
        ThemeChanged?.Invoke(theme);
    }

    public void SetRunAtStartup(bool enabled)
    {
        if (RunAtStartup == enabled) return;
        RunAtStartup = enabled;
        Save();
        RunAtStartupChanged?.Invoke(enabled);
    }


    public void SetDatabaseManifestUrl(string value)
    {
        value = (value ?? "").Trim();
        if (DatabaseManifestUrl == value) return;
        DatabaseManifestUrl = value;
        Save();
        DatabaseManifestUrlChanged?.Invoke(value);
    }



    public void SetUpdateChannel(AppUpdateChannel channel)
    {
        if (UpdateChannel == channel) return;
        UpdateChannel = channel;
        Save();
        UpdateChannelChanged?.Invoke(channel);
    }

    public void SetAppUpdateManifestUrls(string stable, string canary)
    {
        stable = (stable ?? "").Trim();
        canary = (canary ?? "").Trim();
        if (StableUpdateManifestUrl == stable && CanaryUpdateManifestUrl == canary) return;
        StableUpdateManifestUrl = stable;
        CanaryUpdateManifestUrl = canary;
        Save();
    }

    public string CurrentAppUpdateManifestUrl =>
        UpdateChannel == AppUpdateChannel.Canary ? CanaryUpdateManifestUrl : StableUpdateManifestUrl;

    public void SetCloseBehavior(WindowCloseBehavior behavior)
    {
        if (CloseBehavior == behavior) return;
        CloseBehavior = behavior;
        Save();
    }

    public void CompleteOnboarding()
    {
        if (OnboardingCompleted) return;
        OnboardingCompleted = true;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is not null)
            {
                if (Enum.TryParse<AppTheme>(data.Theme, true, out var parsed))
                    Theme = parsed;
                RunAtStartup = data.RunAtStartup;
                DatabaseManifestUrl = string.IsNullOrWhiteSpace(data.DatabaseManifestUrl)
                    ? DefaultDatabaseManifestUrl
                    : data.DatabaseManifestUrl.Trim();
                OnboardingCompleted = data.OnboardingCompleted;
                if (Enum.TryParse<AppUpdateChannel>(data.UpdateChannel, true, out var updateChannel))
                    UpdateChannel = updateChannel;
                StableUpdateManifestUrl = string.IsNullOrWhiteSpace(data.StableUpdateManifestUrl)
                    ? DefaultStableUpdateManifestUrl
                    : data.StableUpdateManifestUrl.Trim();
                CanaryUpdateManifestUrl = string.IsNullOrWhiteSpace(data.CanaryUpdateManifestUrl)
                    ? DefaultCanaryUpdateManifestUrl
                    : data.CanaryUpdateManifestUrl.Trim();
                if (Enum.TryParse<WindowCloseBehavior>(data.CloseBehavior, true, out var closeBehavior))
                    CloseBehavior = closeBehavior;
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not load settings: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(new SettingsData
            {
                Theme = Theme.ToString(),
                RunAtStartup = RunAtStartup,
                DatabaseManifestUrl = DatabaseManifestUrl,
                OnboardingCompleted = OnboardingCompleted,
                UpdateChannel = UpdateChannel.ToString(),
                StableUpdateManifestUrl = StableUpdateManifestUrl,
                CanaryUpdateManifestUrl = CanaryUpdateManifestUrl,
                CloseBehavior = CloseBehavior.ToString()
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not save settings: {ex.Message}");
        }
    }

    private sealed class SettingsData
    {
        public string Theme { get; set; } = "System";
        public bool RunAtStartup { get; set; }
        public string DatabaseManifestUrl { get; set; } = DefaultDatabaseManifestUrl;
        public bool OnboardingCompleted { get; set; }
        public string UpdateChannel { get; set; } = "Stable";
        public string StableUpdateManifestUrl { get; set; } = DefaultStableUpdateManifestUrl;
        public string CanaryUpdateManifestUrl { get; set; } = DefaultCanaryUpdateManifestUrl;
        public string CloseBehavior { get; set; } = "Ask";
    }
}
