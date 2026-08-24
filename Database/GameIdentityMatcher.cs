using System.Text.RegularExpressions;
using AutoHDR.Models;

namespace AutoHDR.Database;

public enum IdentityConfidence
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Verified = 4
}

public sealed record GameIdentityMatch(
    HdrGame Entry,
    IdentityConfidence Confidence,
    int Score,
    string MatchType,
    string MatchedName,
    bool IsUserEntry)
{
    public string ConfidenceLabel => Confidence switch
    {
        IdentityConfidence.Verified => "Verified",
        IdentityConfidence.High => "High",
        IdentityConfidence.Medium => "Medium",
        IdentityConfidence.Low => "Low",
        _ => "None"
    };

    public bool SafeForAutomaticUse => Confidence >= IdentityConfidence.High;
}

public static class GameIdentityMatcher
{
    private static readonly string[] EditionNoise =
    {
        "directors cut", "director cut", "enhanced edition", "complete edition",
        "ultimate edition", "deluxe edition", "standard edition", "definitive edition",
        "game of the year edition", "goty edition", "digital deluxe", "premium edition",
        "windows edition", "pc edition"
    };

    public static string Normalize(string value)
    {
        value = value.Replace("™", "").Replace("®", "").Replace("©", "");
        value = value.Replace("S.T.A.L.K.E.R.", "STALKER", StringComparison.OrdinalIgnoreCase);
        value = Regex.Replace(value, @"[^\p{L}\p{N}]+", " ");
        return Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
    }

    public static string Canonicalize(string value)
    {
        var normalized = Normalize(value);
        foreach (var noise in EditionNoise)
            normalized = Regex.Replace(normalized, $@"\b{Regex.Escape(noise)}\b", " ", RegexOptions.IgnoreCase);

        normalized = Regex.Replace(normalized, @"\b(?:steam|epic|gog|xbox|game pass|ubisoft connect|rockstar|ea app)\b", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(normalized.Trim(), @"\s+", " ");
    }

    public static int TokenSimilarity(string a, string b)
    {
        var aa = Canonicalize(a).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bb = Canonicalize(b).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (aa.Count == 0 || bb.Count == 0) return 0;
        var intersection = aa.Intersect(bb, StringComparer.OrdinalIgnoreCase).Count();
        var union = aa.Union(bb, StringComparer.OrdinalIgnoreCase).Count();
        return (int)Math.Round(intersection * 100.0 / union);
    }

    public static IEnumerable<string> Names(HdrGame game)
    {
        if (!string.IsNullOrWhiteSpace(game.Name)) yield return game.Name;
        if (game.Aliases is not null)
            foreach (var alias in game.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
                yield return alias;
    }
}
