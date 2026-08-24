using AutoHDR.GameWatcher;

namespace AutoHDR.Database;

public sealed record HdrSourcesUpdateResult(
    int InstalledGames,
    int PcgwChecked,
    int PcgwAdded,
    int UserOverridesSkipped,
    int PcgwPossibleMatches,
    DatabaseUpdateResult Database)
{
    public bool Success => Database.Success;

    public string Summary
    {
        get
        {
            var db = Database.Updated
                ? $"TrueAuto HDR DB: updated to {Database.Version}."
                : $"TrueAuto HDR DB: {Database.Message}";

            return
                $"PCGamingWiki: checked {PcgwChecked} game{(PcgwChecked == 1 ? "" : "s")}, " +
                $"added {PcgwAdded} HDR game{(PcgwAdded == 1 ? "" : "s")}" +
                (PcgwPossibleMatches > 0 ? $", {PcgwPossibleMatches} fuzzy match{(PcgwPossibleMatches == 1 ? "" : "es")} ignored" : "") +
                (UserOverridesSkipped > 0 ? $", {UserOverridesSkipped} user override{(UserOverridesSkipped == 1 ? "" : "s")} preserved" : "") +
                $".\n\n{db}";
        }
    }
}

public sealed class HdrSourcesUpdater
{
    private readonly UnifiedGameDetector _games;
    private readonly HdrDatabase _database;
    private readonly PcgwHdrListClient _pcgw;
    private readonly DatabaseUpdater _databaseUpdater;
    private readonly FileLogger _logger;

    public HdrSourcesUpdater(
        UnifiedGameDetector games,
        HdrDatabase database,
        PcgwHdrListClient pcgw,
        DatabaseUpdater databaseUpdater,
        FileLogger logger)
    {
        _games = games;
        _database = database;
        _pcgw = pcgw;
        _databaseUpdater = databaseUpdater;
        _logger = logger;
    }

    public async Task<HdrSourcesUpdateResult> UpdateAsync(
        string manifestUrl,
        Action<int, int, string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Invoke(0, 1, "Refreshing PCGamingWiki HDR list…");
        var pcgwAvailable = await _pcgw.RefreshAsync(ct);
        if (!pcgwAvailable)
            _logger.Log("HDR sources update: PCGamingWiki refresh failed; continuing to maintained DB update.");

        var installed = await Task.Run(() => _games.GetInstalledGames(true).ToList(), ct);

        var checkedCount = 0;
        var added = 0;
        var userSkipped = 0;
        var fuzzy = 0;

        for (var i = 0; i < installed.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var game = installed[i];
            progress?.Invoke(i, installed.Count, $"PCGamingWiki {i + 1}/{installed.Count}: {game.Name} [{game.Store}]");

            // Explicit user choices are authoritative. This includes manual SDR,
            // manual HDR and a previously user-approved candidate.
            if (_database.IsUserEntry(game))
            {
                userSkipped++;
                continue;
            }

            if (!pcgwAvailable)
                continue;

            var pcgw = await _pcgw.CheckAsync(game.Name, ct);
            checkedCount++;

            if (pcgw.Success && pcgw.IsHdrSupported)
            {
                await _database.PutForInstalledGameAsync(
                    game,
                    true,
                    $"PCGamingWiki HDR list: {pcgw.SupportLabel}",
                    "pcgw-hdr-list",
                    ct);
                added++;
                _logger.Log(
                    $"HDR sources update: PCGW added {game.Name} [{game.Store}:{game.StoreId}] " +
                    $"as {pcgw.SupportLabel}, matched='{pcgw.MatchedTitle}'.");
            }
            else if (pcgw.Success && !string.IsNullOrWhiteSpace(pcgw.MatchedTitle))
            {
                // The PCGW client only returns a MatchedTitle here for a fuzzy
                // match. Never turn HDR on from this result.
                fuzzy++;
                _logger.Log(
                    $"HDR sources update: ignored fuzzy PCGW match for {game.Name}: " +
                    $"'{pcgw.MatchedTitle}' ({pcgw.Detail}).");
            }

            progress?.Invoke(i + 1, installed.Count, $"PCGamingWiki {i + 1}/{installed.Count}: {game.Name} [{game.Store}]");
        }

        progress?.Invoke(installed.Count, installed.Count, "PCGamingWiki check complete. Checking TrueAuto HDR database…");
        var db = await _databaseUpdater.CheckAndUpdateAsync(manifestUrl, ct);

        _logger.Log(
            $"HDR sources update complete: installed={installed.Count}, pcgw-checked={checkedCount}, " +
            $"pcgw-added={added}, user-overrides-preserved={userSkipped}, fuzzy-ignored={fuzzy}, " +
            $"database-success={db.Success}, database-updated={db.Updated}, database-version={db.Version ?? "n/a"}.");

        return new HdrSourcesUpdateResult(
            installed.Count,
            checkedCount,
            added,
            userSkipped,
            fuzzy,
            db);
    }
}
