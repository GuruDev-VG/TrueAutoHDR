using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

public sealed class UnifiedGameDetector
{
    private readonly SteamGameDetector _steam;
    private readonly CustomGameDetector _custom;
    private readonly IReadOnlyList<IGameSource> _sources;
    private readonly FileLogger _logger;

    public UnifiedGameDetector(SteamGameDetector steam, CustomGameDetector custom, FileLogger logger)
    {
        _steam = steam;
        _custom = custom;
        _logger = logger;
        _sources = new IGameSource[]
        {
            new EpicGameDetector(logger),
            new GogGameDetector(logger),
            new XboxGameDetector(logger),
            new UbisoftGameDetector(logger),
            new RockstarGameDetector(logger),
            new EaGameDetector(logger),
            custom
        };
    }

    public IReadOnlyList<InstalledGame> GetInstalledGames(bool forceRefresh = false)
    {
        var games = new List<InstalledGame>();
        games.AddRange(_steam.GetInstalledGames(forceRefresh).Select(g => new InstalledGame("Steam", g.AppId, g.Name, g.InstallDirectory)));
        foreach (var source in _sources)
        {
            try { games.AddRange(source.GetInstalledGames(forceRefresh)); }
            catch (Exception ex) { _logger.Log($"{source.StoreName} installed-game scan failed: {ex.Message}"); }
        }
        return games.GroupBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(g => g.Name).ToArray();
    }

    public InstalledGame? IdentifyByExecutable(string executablePath)
    {
        var steam = _steam.IdentifyByExecutable(executablePath);
        if (steam is not null) return new InstalledGame("Steam", steam.AppId, steam.Name, steam.InstallDirectory);
        foreach (var source in _sources)
        {
            var match = source.IdentifyByExecutable(executablePath);
            if (match is not null) return match;
        }
        return null;
    }

    public InstalledGame AddStandaloneExecutable(string exePath) => _custom.AddExecutable(exePath);
}
