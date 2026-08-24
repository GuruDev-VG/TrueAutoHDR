using System.Text.RegularExpressions;
using Microsoft.Win32;
using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

public sealed class SteamGameDetector
{
    private readonly FileLogger _logger;
    private bool _loaded;
    private DateTime _lastMissRefresh = DateTime.MinValue;
    private List<SteamGame> _games = new();
    private readonly object _sync = new();

    public SteamGameDetector(FileLogger logger) => _logger = logger;

    public SteamGame? IdentifyByExecutable(string executablePath)
    {
        EnsureLoaded();
        var full = Normalize(executablePath);
        var match = FindMatch(full);
        if (match is not null) return match;

        // If a newly installed game starts while AutoHDR is already running, refresh
        // only on an actual Steam-library miss (and at most once every 30 seconds).
        // This replaces the old unconditional two-minute manifest rescan.
        if (LooksLikeSteamGamePath(full) && DateTime.UtcNow - _lastMissRefresh > TimeSpan.FromSeconds(30))
        {
            _lastMissRefresh = DateTime.UtcNow;
            Refresh();
            return FindMatch(full);
        }

        return null;
    }

    public IReadOnlyList<SteamGame> GetInstalledGames(bool forceRefresh = false)
    {
        if (forceRefresh) Refresh();
        else EnsureLoaded();
        lock (_sync) return _games.OrderBy(g => g.Name).ToArray();
    }

    private void EnsureLoaded()
    {
        lock (_sync)
        {
            if (_loaded) return;
        }
        Refresh();
    }

    private SteamGame? FindMatch(string fullPath)
    {
        lock (_sync)
        {
            return _games
                .Where(g => fullPath.StartsWith(Normalize(g.InstallDirectory) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fullPath, Normalize(g.InstallDirectory), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(g => g.InstallDirectory.Length)
                .FirstOrDefault();
        }
    }

    private static bool LooksLikeSteamGamePath(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}common{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private void Refresh()
    {
        try
        {
            var roots = FindSteamLibraries().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var games = new List<SteamGame>();
            foreach (var root in roots)
            {
                var steamApps = Path.Combine(root, "steamapps");
                if (!Directory.Exists(steamApps)) continue;
                foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
                {
                    try
                    {
                        var text = File.ReadAllText(manifest);
                        var appId = Capture(text, "appid");
                        var name = Capture(text, "name");
                        var installDir = Capture(text, "installdir");
                        if (appId is null || installDir is null) continue;
                        games.Add(new SteamGame(appId, name ?? appId, Path.Combine(steamApps, "common", installDir)));
                    }
                    catch { }
                }
            }
            lock (_sync)
            {
                _games = games;
                _loaded = true;
            }
            _logger.Log($"Steam index refreshed: {games.Count} installed games.");
        }
        catch (Exception ex) { _logger.Log($"Steam index refresh failed: {ex.Message}"); }
    }

    private IEnumerable<string> FindSteamLibraries()
    {
        var steamPath = GetSteamPath();
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath)) yield break;
        yield return steamPath;
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        var text = File.ReadAllText(vdf);
        foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"(?<p>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
        {
            var path = match.Groups["p"].Value.Replace("\\\\", "\\");
            if (Directory.Exists(path)) yield return path;
        }
    }

    private static string? GetSteamPath()
    {
        static string? Read(RegistryKey hive, string subKey, string name)
        {
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(name) as string;
        }
        return Read(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")
            ?? Read(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
            ?? Read(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
    }

    private static string? Capture(string text, string key)
    {
        var match = Regex.Match(text, $"\\\"{Regex.Escape(key)}\\\"\\s+\\\"(?<v>[^\\\"]*)\\\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value : null;
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
