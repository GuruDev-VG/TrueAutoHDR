using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoHDR.Database;
using AutoHDR.Models;

namespace AutoHDR.Mods;

public enum HdrModReadiness
{
    Unknown = 0,
    InProgress = 1,
    Experimental = 2,
    Ready = 3
}

public sealed record HdrModMatch(string Provider, HdrModReadiness Readiness, string UpstreamStatus, string Url, string MatchedTitle)
{
    public string ReadinessLabel => Readiness switch
    {
        HdrModReadiness.Ready => "Ready",
        HdrModReadiness.Experimental => "Experimental",
        HdrModReadiness.InProgress => "In Progress",
        _ => "Unknown"
    };
}

/// <summary>
/// Canary-only consumer. Refresh is explicit (wired to Refresh HDR Data in the
/// Canary build); GetMatches never performs network I/O.
/// </summary>
public sealed class HdrModDiscoveryService : IDisposable
{
    private const string RenoDxSource = "https://raw.githubusercontent.com/wiki/clshortfuse/renodx/Mods.md";
    public const string RenoDxProject = "https://github.com/clshortfuse/renodx/wiki/Mods";
    public const string SpecialKProject = "https://www.special-k.info/";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly string _cachePath;
    private readonly FileLogger _logger;
    private List<CacheEntry> _entries = new();

    public HdrModDiscoveryService(string appData, FileLogger logger)
    {
        _logger = logger;
        _cachePath = Path.Combine(appData, "Cache", "Mods", "renodx.json");
        LoadCache();
    }

    public async Task<(bool Success, string Message)> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(RenoDxSource, cancellationToken);
            response.EnsureSuccessStatusCode();
            var markdown = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseRenoDx(markdown);
            if (parsed.Count == 0) return (false, "RenoDX source was reachable but no mod rows could be parsed; retained the previous cache.");

            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var temp = _cachePath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            File.Move(temp, _cachePath, true);
            _entries = parsed;
            _logger.Log($"RenoDX Canary catalog refreshed: {_entries.Count} entries.");
            return (true, $"RenoDX catalog refreshed: {_entries.Count} entries.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Log($"RenoDX catalog refresh failed: {ex.Message}");
            return (false, $"RenoDX refresh failed; retained the previous cache. {ex.Message}");
        }
    }

    public IReadOnlyList<HdrModMatch> GetMatches(InstalledGame game)
    {
        var result = new List<HdrModMatch>();
        var normalized = GameIdentityMatcher.Normalize(game.Name);
        var variant = GameIdentityMatcher.CanonicalizeSafeVariant(game.Name);
        var best = _entries
            .Select(e => new
            {
                Entry = e,
                Exact = GameIdentityMatcher.Normalize(e.Title) == normalized,
                Variant = GameIdentityMatcher.CanonicalizeSafeVariant(e.Title) == variant,
                Score = GameIdentityMatcher.TokenSimilarity(game.Name, e.Title)
            })
            .Where(x => x.Exact || x.Variant || x.Score >= 92)
            .OrderByDescending(x => x.Exact)
            .ThenByDescending(x => x.Variant)
            .ThenByDescending(x => x.Score)
            .FirstOrDefault();

        if (best is not null)
            result.Add(new HdrModMatch("RenoDX", best.Entry.Readiness, best.Entry.Status, RenoDxProject, best.Entry.Title));

        // Special K is a general framework and its official compatibility data
        // is not a single authoritative machine-readable per-game list. Keep the
        // status Unknown rather than inventing compatibility, while still giving
        // Canary users the official project entry point.
        result.Add(new HdrModMatch("Special K", HdrModReadiness.Unknown, "Official per-game status not available in a stable machine-readable catalog", SpecialKProject, game.Name));
        return result;
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;
            _entries = JsonSerializer.Deserialize<List<CacheEntry>>(File.ReadAllText(_cachePath)) ?? new();
        }
        catch (Exception ex) { _logger.Log($"Could not load RenoDX cache: {ex.Message}"); }
    }

    private static List<CacheEntry> ParseRenoDx(string markdown)
    {
        var result = new List<CacheEntry>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|') || !line.EndsWith('|')) continue;
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 3) continue;
            var title = StripMarkdown(cells[0]);
            if (title.Length < 2 || title.Equals("Name", StringComparison.OrdinalIgnoreCase) || title.All(c => c == '-' || c == ':')) continue;
            var status = cells[^1];
            var readiness = status.Contains("✅", StringComparison.Ordinal) ? HdrModReadiness.Ready
                : status.Contains("🚧", StringComparison.Ordinal) ? HdrModReadiness.InProgress
                : HdrModReadiness.Unknown;
            result.Add(new CacheEntry(title, readiness, StripMarkdown(status)));
        }
        return result.GroupBy(e => GameIdentityMatcher.Normalize(e.Title)).Select(g => g.First()).ToList();
    }

    private static string StripMarkdown(string value)
    {
        value = Regex.Replace(value, @"!?(?:\[([^\]]+)\])\([^\)]+\)", "$1");
        value = value.Replace("**", "").Replace("`", "");
        return value.Trim();
    }

    public void Dispose() => _http.Dispose();

    public sealed record CacheEntry(string Title, HdrModReadiness Readiness, string Status);
}
