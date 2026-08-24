using AutoHDR.Models;
using Microsoft.Win32;

namespace AutoHDR.GameWatcher;

public sealed class RockstarGameDetector : IGameSource
{
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<InstalledGame> _games = new();
    private bool _loaded;
    public string StoreName => "Rockstar";

    public RockstarGameDetector(FileLogger logger) => _logger = logger;

    public IReadOnlyList<InstalledGame> GetInstalledGames(bool forceRefresh = false)
    {
        if (forceRefresh || !_loaded) Refresh();
        lock (_sync) return _games.OrderBy(g => g.Name).ToArray();
    }

    public InstalledGame? IdentifyByExecutable(string executablePath)
    {
        if (!_loaded) Refresh();
        var full = Normalize(executablePath);
        lock (_sync) return _games.Where(g => IsUnder(full, g.InstallDirectory))
            .OrderByDescending(g => g.InstallDirectory.Length).FirstOrDefault();
    }

    private void Refresh()
    {
        var result = new Dictionary<string, InstalledGame>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);

                // Rockstar titles commonly expose product keys below SOFTWARE\Rockstar Games.
                using (var rockstarRoot = hklm.OpenSubKey(@"SOFTWARE\Rockstar Games"))
                {
                    if (rockstarRoot is not null)
                    {
                        foreach (var product in rockstarRoot.GetSubKeyNames())
                        {
                            if (product.Contains("Launcher", StringComparison.OrdinalIgnoreCase) ||
                                product.Contains("Social Club", StringComparison.OrdinalIgnoreCase)) continue;
                            using var sub = rockstarRoot.OpenSubKey(product);
                            var install = First(sub, "InstallFolder", "InstallFolderSteam", "InstallLocation", "Path");
                            if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) continue;
                            var name = First(sub, "DisplayName") ?? product;
                            result[product] = new InstalledGame(StoreName, product, name, install);
                        }
                    }
                }

                // Fallback to normal uninstall metadata.
                using var uninstallRoot = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallRoot is null) continue;
                foreach (var subName in uninstallRoot.GetSubKeyNames())
                {
                    using var sub = uninstallRoot.OpenSubKey(subName);
                    var publisher = sub?.GetValue("Publisher")?.ToString();
                    if (publisher is null || !publisher.Contains("Rockstar Games", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = sub?.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name) ||
                        name.Contains("Launcher", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Social Club", StringComparison.OrdinalIgnoreCase)) continue;
                    var install = sub?.GetValue("InstallLocation")?.ToString()?.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) continue;
                    result.TryAdd(subName, new InstalledGame(StoreName, subName, name, install));
                }
            }
        }
        catch (Exception ex) { _logger.Log($"Rockstar index refresh failed: {ex.Message}"); }

        lock (_sync) { _games = result.Values.ToList(); _loaded = true; }
        _logger.Log($"Rockstar index refreshed: {_games.Count} installed games.");
    }

    private static string? First(RegistryKey? key, params string[] names)
    {
        foreach (var name in names)
        {
            var value = key?.GetValue(name)?.ToString()?.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static string Normalize(string p) { try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); } catch { return p; } }
    private static bool IsUnder(string path, string root) { var r = Normalize(root); return path.Equals(r, StringComparison.OrdinalIgnoreCase) || path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
}
