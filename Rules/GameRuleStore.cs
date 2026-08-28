using System.Text.Json;
using AutoHDR.Models;

namespace AutoHDR.Rules;

public enum DisplayRecoveryMode
{
    Off = 0,
    ReapplyCurrentMode = 1,
    ForceRefreshRateReset = 2
}

public sealed class GameRule
{
    // All values default to old behavior, so simply having the rules system
    // installed costs nothing at runtime for games without overrides.
    public int EnableDelayMs { get; set; }
    public int ExitGraceMs { get; set; }
    public bool KeepHdrAfterExit { get; set; }
    public string DisplayDeviceName { get; set; } = "";
    public DisplayRecoveryMode DisplayRecovery { get; set; } = DisplayRecoveryMode.Off;
}

public sealed class GameRuleStore
{
    private readonly string _path;
    private readonly FileLogger _logger;
    private readonly Dictionary<string, GameRule> _rules = new(StringComparer.OrdinalIgnoreCase);

    public GameRuleStore(string path, FileLogger logger)
    {
        _path = path;
        _logger = logger;
        Load();
    }

    public GameRule Get(InstalledGame game)
    {
        lock (_rules)
            return _rules.TryGetValue(game.Key, out var rule)
                ? new GameRule
                {
                    EnableDelayMs = rule.EnableDelayMs,
                    ExitGraceMs = rule.ExitGraceMs,
                    KeepHdrAfterExit = rule.KeepHdrAfterExit,
                    DisplayDeviceName = rule.DisplayDeviceName ?? "",
                    DisplayRecovery = rule.DisplayRecovery
                }
                : new GameRule();
    }

    public void Set(InstalledGame game, GameRule rule)
    {
        rule.EnableDelayMs = Math.Clamp(rule.EnableDelayMs, 0, 30000);
        rule.ExitGraceMs = Math.Clamp(rule.ExitGraceMs, 0, 30000);

        lock (_rules)
        {
            rule.DisplayDeviceName = (rule.DisplayDeviceName ?? "").Trim();
            if (rule.EnableDelayMs == 0 && rule.ExitGraceMs == 0 &&
                !rule.KeepHdrAfterExit && string.IsNullOrWhiteSpace(rule.DisplayDeviceName) &&
                rule.DisplayRecovery == DisplayRecoveryMode.Off)
                _rules.Remove(game.Key);
            else
                _rules[game.Key] = rule;
            SaveLocked();
        }
    }

    public int Count
    {
        get { lock (_rules) return _rules.Count; }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, GameRule>>(File.ReadAllText(_path));
            if (data is null) return;
            foreach (var pair in data) _rules[pair.Key] = pair.Value;
            _logger.Log($"Game rules loaded: {_rules.Count} override(s).");
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not load game rules: {ex}");
        }
    }

    private void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_rules,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _path, true);
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not save game rules: {ex}");
        }
    }
}
