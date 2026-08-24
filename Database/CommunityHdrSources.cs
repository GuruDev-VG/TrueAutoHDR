using System.Text.Json;

namespace AutoHDR.Database;

public sealed class CommunityHdrSources
{
    private readonly Dictionary<string, string> _rhi = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _hdrGamingDb = new(StringComparer.OrdinalIgnoreCase);

    public CommunityHdrSources(string path, FileLogger logger)
    {
        try
        {
            if (!File.Exists(path))
            {
                logger.Log($"Community HDR source list not found: {path}");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("rhiNativeHdr", out var rhi))
                foreach (var item in rhi.EnumerateArray()) Add(_rhi, item.GetString());
            if (doc.RootElement.TryGetProperty("hdrGamingDatabase", out var hdr))
                foreach (var item in hdr.EnumerateArray()) Add(_hdrGamingDb, item.GetString());

            logger.Log($"Community HDR sources loaded: {_rhi.Count} RHI names + {_hdrGamingDb.Count} HDR Gaming Database names.");
        }
        catch (Exception ex) { logger.Log($"Community HDR source list load failed: {ex.Message}"); }
    }

    public SourceMatch Match(string gameName)
    {
        var exactKey = GameIdentityMatcher.Normalize(gameName);
        _rhi.TryGetValue(exactKey, out var rhiExact);
        _hdrGamingDb.TryGetValue(exactKey, out var hdrExact);
        if (rhiExact is not null || hdrExact is not null)
            return new SourceMatch(rhiExact, hdrExact, IdentityConfidence.High, 95, "Exact normalized title");

        var canonical = GameIdentityMatcher.Canonicalize(gameName);
        var rhiCanonical = _rhi.Values.FirstOrDefault(v => GameIdentityMatcher.Canonicalize(v) == canonical);
        var hdrCanonical = _hdrGamingDb.Values.FirstOrDefault(v => GameIdentityMatcher.Canonicalize(v) == canonical);
        if (rhiCanonical is not null || hdrCanonical is not null)
            return new SourceMatch(rhiCanonical, hdrCanonical, IdentityConfidence.Medium, 85, "Canonical title");

        string? bestRhi = null;
        string? bestHdr = null;
        var bestScore = 0;

        foreach (var value in _rhi.Values)
        {
            var score = GameIdentityMatcher.TokenSimilarity(gameName, value);
            if (score > bestScore) { bestScore = score; bestRhi = value; bestHdr = null; }
        }

        foreach (var value in _hdrGamingDb.Values)
        {
            var score = GameIdentityMatcher.TokenSimilarity(gameName, value);
            if (score > bestScore) { bestScore = score; bestHdr = value; bestRhi = null; }
            else if (score == bestScore && score >= 72 &&
                     bestRhi is not null &&
                     GameIdentityMatcher.Normalize(bestRhi) == GameIdentityMatcher.Normalize(value))
                bestHdr = value;
        }

        if (bestScore >= 72)
            return new SourceMatch(bestRhi, bestHdr,
                bestScore >= 84 ? IdentityConfidence.Medium : IdentityConfidence.Low,
                bestScore, "Similar title");

        return SourceMatch.None;
    }

    private static void Add(Dictionary<string, string> set, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var trimmed = value.Trim();
        set.TryAdd(GameIdentityMatcher.Normalize(trimmed), trimmed);
    }
}

public readonly record struct SourceMatch(
    string? RhiTitle,
    string? HdrGamingDatabaseTitle,
    IdentityConfidence Confidence,
    int Score,
    string MatchType)
{
    public static SourceMatch None => new(null, null, IdentityConfidence.None, 0, "");
    public bool RhiNativeHdr => !string.IsNullOrWhiteSpace(RhiTitle);
    public bool HdrGamingDatabase => !string.IsNullOrWhiteSpace(HdrGamingDatabaseTitle);
    public bool Any => RhiNativeHdr || HdrGamingDatabase;

    public string Label => (RhiNativeHdr, HdrGamingDatabase) switch
    {
        (true, true) => "RHI + HDR Gaming Database",
        (true, false) => "RHI nativeHdrGames",
        (false, true) => "HDR Gaming Database",
        _ => ""
    };

    public string MatchedTitle
    {
        get
        {
            if (RhiNativeHdr && HdrGamingDatabase)
            {
                if (string.Equals(RhiTitle, HdrGamingDatabaseTitle, StringComparison.OrdinalIgnoreCase))
                    return RhiTitle!;
                return $"{RhiTitle} / {HdrGamingDatabaseTitle}";
            }
            return RhiTitle ?? HdrGamingDatabaseTitle ?? "";
        }
    }

    public string ConfidenceLabel => Confidence switch
    {
        IdentityConfidence.High => "High",
        IdentityConfidence.Medium => "Medium",
        IdentityConfidence.Low => "Low",
        _ => "None"
    };
}
