using AutoHDR.Models;
using Microsoft.Win32;

namespace AutoHDR.GameWatcher;

public sealed class GogGameDetector : IGameSource
{
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<InstalledGame> _games = new();
    private bool _loaded;
    public string StoreName => "GOG";

    public GogGameDetector(FileLogger logger) => _logger = logger;

    public IReadOnlyList<InstalledGame> GetInstalledGames(bool forceRefresh = false)
    {
        if (forceRefresh || !_loaded) Refresh();
        lock (_sync) return _games.OrderBy(g => g.Name).ToArray();
    }

    public InstalledGame? IdentifyByExecutable(string executablePath)
    {
        if (!_loaded) Refresh();
        var full = Normalize(executablePath);
        lock (_sync) return _games.Where(g => IsUnder(full, g.InstallDirectory)).OrderByDescending(g => g.InstallDirectory.Length).FirstOrDefault();
    }

    private void Refresh()
    {
        var result = new Dictionary<string, InstalledGame>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var path in new[] { @"SOFTWARE\GOG.com\Games", @"SOFTWARE\WOW6432Node\GOG.com\Games" })
            {
                using var root = hive.OpenSubKey(path);
                if (root is null) continue;
                foreach (var subName in root.GetSubKeyNames())
                {
                    using var sub = root.OpenSubKey(subName);
                    var install = sub?.GetValue("path") as string;
                    var name = sub?.GetValue("gameName") as string ?? sub?.GetValue("gameNameLocalized") as string;
                    var id = sub?.GetValue("gameID")?.ToString() ?? subName;
                    if (!string.IsNullOrWhiteSpace(install) && !string.IsNullOrWhiteSpace(name) && Directory.Exists(install))
                        result[id] = new InstalledGame(StoreName, id, name, install);
                }
            }
            lock (_sync) { _games = result.Values.ToList(); _loaded = true; }
            _logger.Log($"GOG index refreshed: {_games.Count} installed games.");
        }
        catch (Exception ex) { _logger.Log($"GOG index refresh failed: {ex.Message}"); }
    }

    private static string Normalize(string p) { try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); } catch { return p; } }
    private static bool IsUnder(string path, string root) { var r = Normalize(root); return path.Equals(r, StringComparison.OrdinalIgnoreCase) || path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
}
