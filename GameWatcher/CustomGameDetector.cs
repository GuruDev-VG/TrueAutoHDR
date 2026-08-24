using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

public sealed class CustomGameDetector : IGameSource
{
    private sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ExePath { get; set; } = "";
    }

    private readonly string _path;
    private readonly FileLogger _logger;
    private readonly object _sync = new();
    private List<Entry> _entries = new();
    private bool _loaded;
    public string StoreName => "Standalone";

    public CustomGameDetector(string path, FileLogger logger) { _path = path; _logger = logger; }

    public IReadOnlyList<InstalledGame> GetInstalledGames(bool forceRefresh = false)
    {
        if (forceRefresh || !_loaded) Load();
        lock (_sync)
            return _entries.Where(e => File.Exists(e.ExePath))
                .Select(ToInstalled).OrderBy(g => g.Name).ToArray();
    }

    public InstalledGame? IdentifyByExecutable(string executablePath)
    {
        if (!_loaded) Load();
        string full;
        try { full = Path.GetFullPath(executablePath); } catch { full = executablePath; }
        lock (_sync)
        {
            var match = _entries.FirstOrDefault(e => string.Equals(Normalize(e.ExePath), Normalize(full), StringComparison.OrdinalIgnoreCase));
            return match is null ? null : ToInstalled(match);
        }
    }

    public InstalledGame AddExecutable(string exePath)
    {
        exePath = Path.GetFullPath(exePath);
        var version = FileVersionInfo.GetVersionInfo(exePath);
        var name = version.ProductName;
        if (string.IsNullOrWhiteSpace(name)) name = version.FileDescription;
        if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(exePath);

        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exePath.ToLowerInvariant())))[..16].ToLowerInvariant();
        lock (_sync)
        {
            if (!_loaded) LoadUnlocked();
            var existing = _entries.FirstOrDefault(e => e.Id == id);
            if (existing is null) _entries.Add(new Entry { Id = id, Name = name!, ExePath = exePath });
            else { existing.Name = name!; existing.ExePath = exePath; }
            SaveUnlocked();
            return ToInstalled(_entries.First(e => e.Id == id));
        }
    }

    private void Load()
    {
        lock (_sync) LoadUnlocked();
    }

    private void LoadUnlocked()
    {
        try
        {
            if (File.Exists(_path))
                _entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(_path)) ?? new();
            else _entries = new();
        }
        catch (Exception ex) { _entries = new(); _logger.Log($"Standalone game list load failed: {ex.Message}"); }
        _loaded = true;
        _logger.Log($"Standalone index refreshed: {_entries.Count} custom executable(s).");
    }

    private void SaveUnlocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static InstalledGame ToInstalled(Entry e)
        => new("Standalone", e.Id, e.Name, Path.GetDirectoryName(e.ExePath) ?? "");

    private static string Normalize(string p) { try { return Path.GetFullPath(p); } catch { return p; } }
}
