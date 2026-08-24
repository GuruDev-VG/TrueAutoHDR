using System.Text.Json;
using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

public sealed class EpicGameDetector : IGameSource
{
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<InstalledGame> _games = new();
    private bool _loaded;
    public string StoreName => "Epic";

    public EpicGameDetector(FileLogger logger) => _logger = logger;

    public IReadOnlyList<InstalledGame> GetInstalledGames(bool forceRefresh = false)
    {
        if (forceRefresh || !_loaded) Refresh();
        lock (_sync) return _games.OrderBy(g => g.Name).ToArray();
    }

    public InstalledGame? IdentifyByExecutable(string executablePath)
    {
        if (!_loaded) Refresh();
        var full = Normalize(executablePath);
        lock (_sync)
            return _games.Where(g => IsUnder(full, g.InstallDirectory)).OrderByDescending(g => g.InstallDirectory.Length).FirstOrDefault();
    }

    private void Refresh()
    {
        var result = new List<InstalledGame>();
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.item"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(file));
                        var e = doc.RootElement;
                        var name = Get(e, "DisplayName") ?? Get(e, "AppName");
                        var path = Get(e, "InstallLocation");
                        var id = Get(e, "CatalogItemId") ?? Get(e, "AppName") ?? Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                            result.Add(new InstalledGame(StoreName, id!, name!, path!));
                    }
                    catch { }
                }
            }
            lock (_sync) { _games = result; _loaded = true; }
            _logger.Log($"Epic index refreshed: {result.Count} installed games.");
        }
        catch (Exception ex) { _logger.Log($"Epic index refresh failed: {ex.Message}"); }
    }

    private static string? Get(JsonElement e, string name) => e.TryGetProperty(name, out var p) ? p.GetString() : null;
    private static string Normalize(string p) { try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); } catch { return p; } }
    private static bool IsUnder(string path, string root) { var r = Normalize(root); return path.Equals(r, StringComparison.OrdinalIgnoreCase) || path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
}
