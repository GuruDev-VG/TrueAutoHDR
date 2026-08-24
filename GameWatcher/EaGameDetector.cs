using AutoHDR.Models;
using Microsoft.Win32;

namespace AutoHDR.GameWatcher;

public sealed class EaGameDetector : IGameSource
{
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<InstalledGame> _games = new();
    private bool _loaded;
    public string StoreName => "EA";

    public EaGameDetector(FileLogger logger) => _logger = logger;

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
                using var root = hklm.OpenSubKey(@"SOFTWARE\Origin Games");
                if (root is not null)
                {
                    foreach (var id in root.GetSubKeyNames())
                    {
                        using var sub = root.OpenSubKey(id);
                        var install = First(sub, "Install Dir", "InstallDir", "InstallLocation");
                        var name = First(sub, "DisplayName", "GameName", "Title");
                        if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) continue;
                        name ??= Path.GetFileName(install.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        result[id] = new InstalledGame(StoreName, id, name ?? id, install);
                    }
                }

                // Fallback for newer EA app installs that expose normal uninstall metadata.
                using var uninstallRoot = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallRoot is null) continue;
                foreach (var subName in uninstallRoot.GetSubKeyNames())
                {
                    using var sub = uninstallRoot.OpenSubKey(subName);
                    var publisher = sub?.GetValue("Publisher")?.ToString();
                    if (publisher is null || !publisher.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = sub?.GetValue("DisplayName")?.ToString();
                    var install = sub?.GetValue("InstallLocation")?.ToString()?.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(name) || name.Equals("EA app", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) continue;
                    result.TryAdd(subName, new InstalledGame(StoreName, subName, name, install));
                }
            }
        }
        catch (Exception ex) { _logger.Log($"EA index refresh failed: {ex.Message}"); }

        lock (_sync) { _games = result.Values.ToList(); _loaded = true; }
        _logger.Log($"EA index refreshed: {_games.Count} installed games.");
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
