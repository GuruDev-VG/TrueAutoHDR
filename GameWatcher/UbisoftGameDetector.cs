using AutoHDR.Models;
using Microsoft.Win32;

namespace AutoHDR.GameWatcher;

public sealed class UbisoftGameDetector : IGameSource
{
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<InstalledGame> _games = new();
    private bool _loaded;
    public string StoreName => "Ubisoft";

    public UbisoftGameDetector(FileLogger logger) => _logger = logger;

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
                using var installs = hklm.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");
                if (installs is null) continue;

                foreach (var id in installs.GetSubKeyNames())
                {
                    using var gameKey = installs.OpenSubKey(id);
                    var install = gameKey?.GetValue("InstallDir")?.ToString()?.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) continue;

                    string? name = null;
                    using (var uninstall = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\UPlay Install {id}"))
                        name = uninstall?.GetValue("DisplayName")?.ToString();

                    name ??= Path.GetFileName(install.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (!string.IsNullOrWhiteSpace(name))
                        result[id] = new InstalledGame(StoreName, id, name, install);
                }
            }
        }
        catch (Exception ex) { _logger.Log($"Ubisoft index refresh failed: {ex.Message}"); }

        lock (_sync) { _games = result.Values.ToList(); _loaded = true; }
        _logger.Log($"Ubisoft index refreshed: {_games.Count} installed games.");
    }

    private static string Normalize(string p) { try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); } catch { return p; } }
    private static bool IsUnder(string path, string root) { var r = Normalize(root); return path.Equals(r, StringComparison.OrdinalIgnoreCase) || path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
}
