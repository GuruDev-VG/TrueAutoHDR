using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

// Best-effort detector for modern Xbox/Game Pass installs that use the public
// <drive>:\\XboxGames\\<Game>\\Content layout. It deliberately avoids scanning
// protected WindowsApps so TrueAuto HDR stays lightweight and non-elevated.
public sealed class XboxGameDetector : IGameSource
{
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<InstalledGame> _games = new();
    private bool _loaded;
    public string StoreName => "Xbox";

    public XboxGameDetector(FileLogger logger) => _logger = logger;

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
        var result = new List<InstalledGame>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var root = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        var content = Path.Combine(dir, "Content");
                        var install = Directory.Exists(content) ? content : dir;
                        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var id = NormalizeId(name);
                        result.Add(new InstalledGame(StoreName, id, name, install));
                    }
                    catch { }
                }
            }
            lock (_sync) { _games = result; _loaded = true; }
            _logger.Log($"Xbox index refreshed: {result.Count} installed games (public XboxGames folders).");
        }
        catch (Exception ex) { _logger.Log($"Xbox index refresh failed: {ex.Message}"); }
    }

    private static string NormalizeId(string name) => new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string Normalize(string p) { try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); } catch { return p; } }
    private static bool IsUnder(string path, string root) { var r = Normalize(root); return path.Equals(r, StringComparison.OrdinalIgnoreCase) || path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
}
