using System.Net;
using System.Text.RegularExpressions;

namespace AutoHDR.Database;

public enum PcgwHdrSupport
{
    Unknown = 0,
    Native,
    Limited,
    AlwaysOn,
    RequiresManualFix,
    NoNativeSupport
}

public readonly record struct PcgwHdrMatch(
    bool Success,
    bool IsHdrSupported,
    PcgwHdrSupport Support,
    string MatchedTitle,
    string Detail)
{
    public string SupportLabel => Support switch
    {
        PcgwHdrSupport.Native => "Native support",
        PcgwHdrSupport.Limited => "Limited native support",
        PcgwHdrSupport.AlwaysOn => "Always on",
        PcgwHdrSupport.RequiresManualFix => "Requires manual fix",
        PcgwHdrSupport.NoNativeSupport => "No native support",
        _ => "Unknown"
    };
}

public sealed class PcgwHdrListClient
{
    public const string ListUrl =
        "https://www.pcgamingwiki.com/wiki/List_of_games_that_support_high_dynamic_range_display_(HDR)";

    private readonly HttpClient _http;
    private readonly FileLogger _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Dictionary<string, PcgwEntry>? _entries;
    private DateTime _loadedUtc;

    public PcgwHdrListClient(FileLogger logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrueAutoHDR/1.1.4 (+https://www.pcgamingwiki.com/)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json");
    }

    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            _entries = null;
            _loadedUtc = default;
        }
        finally
        {
            _loadLock.Release();
        }

        await EnsureLoadedAsync(ct);
        return _entries is { Count: > 0 };
    }

    public async Task<PcgwHdrMatch> CheckAsync(string gameName, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        if (_entries is null || _entries.Count == 0)
            return new(false, false, PcgwHdrSupport.Unknown, "", "PCGamingWiki HDR list unavailable");

        var exact = GameIdentityMatcher.Normalize(gameName);
        if (_entries.TryGetValue(exact, out var entry))
            return ToMatch(entry, "Exact normalized title");

        var canonical = GameIdentityMatcher.Canonicalize(gameName);
        var canonicalEntry = _entries.Values.FirstOrDefault(e =>
            GameIdentityMatcher.Canonicalize(e.Title) == canonical);
        if (canonicalEntry is not null)
            return ToMatch(canonicalEntry, "Canonical title");

        // Do not auto-accept fuzzy PCGW matches. Return them only as a review hint.
        PcgwEntry? best = null;
        var bestScore = 0;
        foreach (var candidate in _entries.Values)
        {
            var score = GameIdentityMatcher.TokenSimilarity(gameName, candidate.Title);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best is not null && bestScore >= 84)
        {
            var m = ToMatch(best, $"Similar title ({bestScore}%)");
            return m with { IsHdrSupported = false };
        }

        return new(true, false, PcgwHdrSupport.Unknown, "", "No PCGamingWiki HDR-list match");
    }

    private static PcgwHdrMatch ToMatch(PcgwEntry entry, string method)
    {
        var positive = entry.Support is PcgwHdrSupport.Native or PcgwHdrSupport.Limited or PcgwHdrSupport.AlwaysOn;
        return new(true, positive, entry.Support, entry.Title, $"PCGamingWiki: {entry.SupportLabel}; {method}");
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_entries is not null && _entries.Count > 0 &&
            DateTime.UtcNow - _loadedUtc < TimeSpan.FromHours(6))
            return;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_entries is not null && _entries.Count > 0 &&
                DateTime.UtcNow - _loadedUtc < TimeSpan.FromHours(6))
                return;

            var urls = new[]
            {
                ListUrl,
                "https://www.pcgamingwiki.com/w/index.php?title=List_of_games_that_support_high_dynamic_range_display_(HDR)&printable=yes",
                "https://www.pcgamingwiki.com/w/api.php?action=parse&page=List_of_games_that_support_high_dynamic_range_display_(HDR)&prop=text&format=json"
            };

            foreach (var url in urls)
            {
                try
                {
                    using var response = await _http.GetAsync(url, ct);
                    var body = await response.Content.ReadAsStringAsync(ct);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        _logger.Log($"PCGW HDR list: HTTP {(int)response.StatusCode} from {url}.");
                        continue;
                    }

                    // API parse responses JSON-escape the HTML. Decode the common
                    // sequences sufficiently for the same row parser.
                    if (body.TrimStart().StartsWith("{"))
                        body = Regex.Unescape(body)
                            .Replace("\\/", "/", StringComparison.Ordinal);

                    var parsed = Parse(body);
                    if (parsed.Count > 0)
                    {
                        _entries = parsed;
                        _loadedUtc = DateTime.UtcNow;
                        _logger.Log($"PCGW HDR list loaded: {_entries.Count} tagged games.");
                        return;
                    }

                    _logger.Log($"PCGW HDR list returned content but no recognizable HDR rows: {url}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"PCGW HDR list request failed: {ex.Message}");
                }
            }

            _entries ??= new Dictionary<string, PcgwEntry>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static Dictionary<string, PcgwEntry> Parse(string html)
    {
        var result = new Dictionary<string, PcgwEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (Match rowMatch in Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var row = rowMatch.Groups[1].Value;

            var titleMatch = Regex.Match(row,
                @"<a\b[^>]*\bhref\s*=\s*[""'][^""']*/wiki/[^""']+[""'][^>]*\btitle\s*=\s*[""']([^""']+)[""'][^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!titleMatch.Success)
                titleMatch = Regex.Match(row,
                    @"<a\b[^>]*\btitle\s*=\s*[""']([^""']+)[""'][^>]*\bhref\s*=\s*[""'][^""']*/wiki/[^""']+[""'][^>]*>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!titleMatch.Success) continue;

            var title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
            if (title.Length == 0 || title.StartsWith("List of games", StringComparison.OrdinalIgnoreCase))
                continue;

            var support = DetectSupport(row);
            if (support == PcgwHdrSupport.Unknown) continue;

            var key = GameIdentityMatcher.Normalize(title);
            if (!result.ContainsKey(key))
                result[key] = new PcgwEntry(title, support);
        }

        return result;
    }

    private static PcgwHdrSupport DetectSupport(string row)
    {
        var decoded = WebUtility.HtmlDecode(row).ToLowerInvariant();

        // Cargo-generated PCGW tables have used both textual status metadata
        // and sortable values over time, so accept either representation.
        if (ContainsAny(decoded, "limited native support", "data-sort-value=\"limited\"", "data-sort-value='limited'"))
            return PcgwHdrSupport.Limited;
        if (ContainsAny(decoded, "always on", "data-sort-value=\"always on\"", "data-sort-value='always on'",
            "data-sort-value=\"alwayson\"", "data-sort-value='alwayson'"))
            return PcgwHdrSupport.AlwaysOn;
        if (ContainsAny(decoded, "requires manual fix", "data-sort-value=\"hackable\"", "data-sort-value='hackable'"))
            return PcgwHdrSupport.RequiresManualFix;
        if (ContainsAny(decoded, "no native support", "data-sort-value=\"false\"", "data-sort-value='false'"))
            return PcgwHdrSupport.NoNativeSupport;
        if (ContainsAny(decoded, "native support", "data-sort-value=\"true\"", "data-sort-value='true'"))
            return PcgwHdrSupport.Native;

        // Some PCGW semantic tables expose status through icon alt/title text.
        if (Regex.IsMatch(decoded, @"(?:alt|title)\s*=\s*[""'][^""']*\blimited\b")) return PcgwHdrSupport.Limited;
        if (Regex.IsMatch(decoded, @"(?:alt|title)\s*=\s*[""'][^""']*\balways\b")) return PcgwHdrSupport.AlwaysOn;
        if (Regex.IsMatch(decoded, @"(?:alt|title)\s*=\s*[""'][^""']*\btrue\b")) return PcgwHdrSupport.Native;
        if (Regex.IsMatch(decoded, @"(?:alt|title)\s*=\s*[""'][^""']*\bfalse\b")) return PcgwHdrSupport.NoNativeSupport;

        return PcgwHdrSupport.Unknown;
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(text.Contains);

    private sealed record PcgwEntry(string Title, PcgwHdrSupport Support)
    {
        public string SupportLabel => Support switch
        {
            PcgwHdrSupport.Native => "Native support",
            PcgwHdrSupport.Limited => "Limited native support",
            PcgwHdrSupport.AlwaysOn => "Always on",
            PcgwHdrSupport.RequiresManualFix => "Requires manual fix",
            PcgwHdrSupport.NoNativeSupport => "No native support",
            _ => "Unknown"
        };
    }
}
