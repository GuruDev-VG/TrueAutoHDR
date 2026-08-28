using System.Text.Json;
using System.Text.RegularExpressions;
using AutoHDR.Models;

namespace AutoHDR.Database;

public sealed class HdrDatabase
{
    private readonly string _userPath;
    private readonly string? _bundledPath;
    private readonly FileLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, HdrGame> _bundled = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, HdrGame> _user = new(StringComparer.OrdinalIgnoreCase);

    public HdrDatabase(string userPath, FileLogger logger, string? bundledSeedPath = null)
    {
        _userPath = userPath; _bundledPath = bundledSeedPath; _logger = logger; Load();
    }

    private void Load()
    {
        _bundled = LoadFile(_bundledPath, "bundled");
        _user = LoadFile(_userPath, "user");
        _logger.Log($"HDR database loaded: {_bundled.Count} bundled + {_user.Count} user entries.");
    }

    private Dictionary<string, HdrGame> LoadFile(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.Log($"HDR {label} database not found: {path ?? "(none)"}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, HdrGame>>(File.ReadAllText(path)) ?? new();
            return new Dictionary<string, HdrGame>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { _logger.Log($"HDR {label} database load failed: {ex}"); return new(StringComparer.OrdinalIgnoreCase); }
    }

    // Legacy Steam lookup.
    public bool TryGet(string appId, out HdrGame? game)
    {
        if (_user.TryGetValue(appId, out game)) return true;
        return _bundled.TryGetValue(appId, out game);
    }

    public bool TryGet(InstalledGame installed, out HdrGame? game)
    {
        var match = ResolveIdentity(installed, includeMediumCandidates: false);
        game = match?.Entry;
        return match is not null;
    }

    public GameIdentityMatch? ResolveIdentity(InstalledGame installed, bool includeMediumCandidates = true)
    {
        // 1) Exact user/store identity: strongest possible signal.
        var composite = CompositeKey(installed.Store, installed.StoreId);
        if (_user.TryGetValue(composite, out var userExact))
            return new GameIdentityMatch(userExact, IdentityConfidence.Verified, 100, "Exact store ID", userExact.Name, true);

        if (_bundled.TryGetValue(composite, out var bundledExact))
            return new GameIdentityMatch(bundledExact, IdentityConfidence.Verified, 100, "Exact store ID", bundledExact.Name, false);

        // Legacy Steam-keyed entries.
        if (installed.IsSteam)
        {
            if (_user.TryGetValue(installed.StoreId, out var legacyUser))
                return new GameIdentityMatch(legacyUser, IdentityConfidence.Verified, 100, "Steam AppID", legacyUser.Name, true);
            if (_bundled.TryGetValue(installed.StoreId, out var legacyBundled))
                return new GameIdentityMatch(legacyBundled, IdentityConfidence.Verified, 100, "Steam AppID", legacyBundled.Name, false);
        }

        // Explicit cross-store IDs carried by newer database entries.
        foreach (var pair in EnumerateEntries())
        {
            var ids = pair.Game.StoreIds;
            if (ids is null) continue;
            var storeIdPair = ids.FirstOrDefault(kv => kv.Key.Equals(installed.Store, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(storeIdPair.Key) &&
                string.Equals(storeIdPair.Value, installed.StoreId, StringComparison.OrdinalIgnoreCase))
                return new GameIdentityMatch(pair.Game, IdentityConfidence.Verified, 100, "Cross-store ID", pair.Game.Name, pair.IsUser);
        }

        var installedNormalized = GameIdentityMatcher.Normalize(installed.Name);

        // 2) Exact normalized title or alias. This is safe enough for automatic
        // cross-store reuse because only punctuation/trademark differences disappear.
        foreach (var pair in EnumerateEntries(userFirst: true))
        {
            foreach (var name in GameIdentityMatcher.Names(pair.Game))
            {
                if (GameIdentityMatcher.Normalize(name) == installedNormalized)
                    return new GameIdentityMatch(pair.Game, IdentityConfidence.High, 95,
                        name == pair.Game.Name ? "Exact normalized title" : "Exact alias",
                        name, pair.IsUser);
            }
        }

        // Conservative non-identity suffix inheritance. This covers storefront
        // variants such as "Crimson Desert Enhanced" and public beta labels
        // without turning broad fuzzy title matching into an automatic decision.
        var safeVariant = GameIdentityMatcher.CanonicalizeSafeVariant(installed.Name);
        if (safeVariant.Length >= 4)
        {
            foreach (var pair in EnumerateEntries(userFirst: true))
            {
                foreach (var name in GameIdentityMatcher.Names(pair.Game))
                {
                    if (GameIdentityMatcher.CanonicalizeSafeVariant(name) == safeVariant &&
                        GameIdentityMatcher.Normalize(name) != installedNormalized)
                        return new GameIdentityMatch(pair.Game, IdentityConfidence.High, 92,
                            "Known title variant", name, pair.IsUser);
                }
            }
        }

        if (!includeMediumCandidates) return null;

        // 3) Broader edition/storefront-noise-stripped title. Keep this review-only.
        var canonical = GameIdentityMatcher.Canonicalize(installed.Name);
        foreach (var pair in EnumerateEntries(userFirst: true))
        {
            foreach (var name in GameIdentityMatcher.Names(pair.Game))
            {
                if (GameIdentityMatcher.Canonicalize(name) == canonical && canonical.Length >= 4)
                    return new GameIdentityMatch(pair.Game, IdentityConfidence.Medium, 85,
                        "Canonical title", name, pair.IsUser);
            }
        }

        // 4) Token similarity is only a suggestion and never drives HDR automatically.
        GameIdentityMatch? best = null;
        foreach (var pair in EnumerateEntries(userFirst: true))
        {
            foreach (var name in GameIdentityMatcher.Names(pair.Game))
            {
                var score = GameIdentityMatcher.TokenSimilarity(installed.Name, name);
                if (score < 72) continue;
                if (best is null || score > best.Score)
                    best = new GameIdentityMatch(pair.Game,
                        score >= 84 ? IdentityConfidence.Medium : IdentityConfidence.Low,
                        score, "Similar title", name, pair.IsUser);
            }
        }
        return best;
    }

    private IEnumerable<(HdrGame Game, bool IsUser)> EnumerateEntries(bool userFirst = false)
    {
        if (userFirst)
        {
            foreach (var g in _user.Values) if (!g.CapabilityOnly) yield return (g, true);
            foreach (var g in _bundled.Values) if (!g.CapabilityOnly) yield return (g, false);
        }
        else
        {
            foreach (var g in _bundled.Values) if (!g.CapabilityOnly) yield return (g, false);
            foreach (var g in _user.Values) if (!g.CapabilityOnly) yield return (g, true);
        }
    }

    public bool TryGetHdr10PlusGaming(InstalledGame installed, out HdrGame? metadata)
    {
        metadata = null;
        IEnumerable<HdrGame> entries = _bundled.Values.Concat(_user.Values).Where(g => g.Hdr10PlusGaming);

        var composite = CompositeKey(installed.Store, installed.StoreId);
        if (_bundled.TryGetValue(composite, out var exact) && exact.Hdr10PlusGaming) { metadata = exact; return true; }
        if (installed.IsSteam && _bundled.TryGetValue(installed.StoreId, out var steam) && steam.Hdr10PlusGaming) { metadata = steam; return true; }

        foreach (var game in entries)
        {
            if (game.StoreIds is not null && game.StoreIds.TryGetValue(installed.Store, out var storeId) &&
                string.Equals(storeId, installed.StoreId, StringComparison.OrdinalIgnoreCase))
            {
                metadata = game; return true;
            }
        }

        var normalized = GameIdentityMatcher.Normalize(installed.Name);
        foreach (var game in entries)
        {
            foreach (var name in GameIdentityMatcher.Names(game))
            {
                if (GameIdentityMatcher.Normalize(name) == normalized)
                {
                    metadata = game; return true;
                }
            }
        }

        var variant = GameIdentityMatcher.CanonicalizeSafeVariant(installed.Name);
        if (variant.Length >= 4)
        {
            foreach (var game in entries)
            {
                foreach (var name in GameIdentityMatcher.Names(game))
                {
                    if (GameIdentityMatcher.CanonicalizeSafeVariant(name) == variant)
                    {
                        metadata = game; return true;
                    }
                }
            }
        }
        return false;
    }

    public bool IsUserEntry(InstalledGame game)
    {
        var composite = CompositeKey(game.Store, game.StoreId);
        if (_user.ContainsKey(composite)) return true;
        if (game.IsSteam && _user.ContainsKey(game.StoreId)) return true;
        var normalized = NormalizeName(game.Name);
        return _user.Values.Any(g => NormalizeName(g.Name) == normalized);
    }

    public bool IsUserEntry(string appId) => _user.ContainsKey(appId);
    public bool IsBundledEntry(string appId) => _bundled.ContainsKey(appId);

    public IReadOnlyCollection<HdrGame> GetAll()
    {
        var merged = new Dictionary<string, HdrGame>(_bundled, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _user) merged[pair.Key] = pair.Value;
        return merged.Values.OrderBy(g => g.Name).ToArray();
    }

    public async Task PutAsync(HdrGame game, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var key = !string.IsNullOrWhiteSpace(game.StoreId) ? CompositeKey(game.Store, game.StoreId) : game.SteamAppId;
            _user[key] = game;
            await SaveUserAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task PutForInstalledGameAsync(InstalledGame installed, bool nativeHdr, string source, string rawValue, CancellationToken ct = default)
    {
        await PutAsync(new HdrGame
        {
            SteamAppId = installed.IsSteam ? installed.StoreId : "",
            Store = installed.Store,
            StoreId = installed.StoreId,
            Name = installed.Name,
            NativeHdr = nativeHdr,
            RawPcgwHdrValue = rawValue,
            CheckedUtc = DateTime.UtcNow,
            Source = source
        }, ct);
    }

    public async Task<bool> RemoveUserOverrideAsync(InstalledGame game, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var removed = _user.Remove(CompositeKey(game.Store, game.StoreId));
            if (game.IsSteam) removed |= _user.Remove(game.StoreId);
            if (!removed)
            {
                var normalized = NormalizeName(game.Name);
                var key = _user.FirstOrDefault(p => NormalizeName(p.Value.Name) == normalized).Key;
                if (!string.IsNullOrWhiteSpace(key)) removed = _user.Remove(key);
            }
            if (!removed) return false;
            await SaveUserAsync(ct); return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RemoveUserOverrideAsync(string appId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { if (!_user.Remove(appId)) return false; await SaveUserAsync(ct); return true; }
        finally { _gate.Release(); }
    }

    public async Task<int> ImportUserEntriesAsync(string sourcePath, CancellationToken ct = default)
    {
        var imported = LoadFile(sourcePath, "import"); if (imported.Count == 0) return 0;
        await _gate.WaitAsync(ct);
        try { foreach (var pair in imported) _user[pair.Key] = pair.Value; await SaveUserAsync(ct); return imported.Count; }
        finally { _gate.Release(); }
    }

    public async Task ExportMergedAsync(string destinationPath, CancellationToken ct = default)
    {
        var merged = new Dictionary<string, HdrGame>(_bundled, StringComparer.OrdinalIgnoreCase);
        foreach (var p in _user) merged[p.Key] = p.Value;
        await File.WriteAllTextAsync(destinationPath, JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true }), ct);
    }

    private async Task SaveUserAsync(CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(_user, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_userPath)!);
        var tempPath = _userPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _userPath, true);
    }

    public static string CompositeKey(string store, string storeId) => $"{store.ToLowerInvariant()}:{storeId}";
    public static string NormalizeName(string value) => GameIdentityMatcher.Normalize(value);

    public void ReloadBundledDatabase()
    {
        _bundled = LoadFile(_bundledPath, "native HDR");
        _logger.Log($"Native HDR database reloaded: {_bundled.Count} entries.");
    }

    public int Count => GetAll().Count;
    public int BundledCount => _bundled.Count;
    public int UserCount => _user.Count;
    public string UserDatabasePath => _userPath;
    public string? BundledDatabasePath => _bundledPath;
}
