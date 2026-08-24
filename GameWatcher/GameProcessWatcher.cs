using AutoHDR.Database;
using AutoHDR.HDR;
using AutoHDR.Models;
using AutoHDR.Rules;

namespace AutoHDR.GameWatcher;

public sealed class GameProcessWatcher : IDisposable
{
    private readonly UnifiedGameDetector _games;
    private readonly HdrDatabase _database;
    private readonly HdrController _hdr;
    private readonly FileLogger _logger;
    private readonly GameRuleStore _rules;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<int, InstalledGame> _trackedProcesses = new();
    private readonly HashSet<int> _seenProcessIds = new();
    private readonly Dictionary<string, string> _activeHdrGames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _hdrStateBeforeByDisplay = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private Task? _loop;
    
    public event Action<string>? StatusChanged;

    public GameProcessWatcher(UnifiedGameDetector games, HdrDatabase database, HdrController hdr, GameRuleStore rules, FileLogger logger)
    {
        _games = games; _database = database; _hdr = hdr; _rules = rules; _logger = logger;
    }

    public void Start(bool startupMode = false)
    {
        _loop ??= Task.Run(() => StartLoopAsync(startupMode, _cts.Token));
        PublishStatus(startupMode ? "Starting quietly…" : "Watching for games…");
    }

    private async Task StartLoopAsync(bool startupMode, CancellationToken ct)
    {
        if (startupMode)
        {
            // Windows launches startup applications while the shell, GPU driver,
            // launchers and per-monitor DPI state are still settling. Keep our
            // startup path nearly idle, then inspect all currently running
            // processes once the desktop is ready.
            _logger.Log("Startup mode enabled; delaying game index/process work for 10 seconds.");
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
        PublishStatus("Watching for games…");
        await LoopAsync(ct);
    }

    private void PublishStatus(string text)
    {
        try { StatusChanged?.Invoke(text); }
        catch (Exception ex) { _logger.Log($"Status callback failed: {ex.GetType().Name}: {ex.Message}"); }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var alive = Win32ProcessEnumerator.GetProcessIds();
                foreach (var pid in alive)
                {
                    if (!_seenProcessIds.Add(pid)) continue;
                    var exe = Win32ProcessEnumerator.TryGetExecutablePath(pid);
                    if (string.IsNullOrWhiteSpace(exe)) continue;
                    var game = _games.IdentifyByExecutable(exe);
                    if (game is null) continue;
                    _trackedProcesses[pid] = game;
                    _logger.Log($"Detected {game.Store} process: {game.Name} ({game.StoreId}) PID={pid}, {exe}");
                    _ = HandleGameStartedAsync(game, ct);
                }

                _seenProcessIds.RemoveWhere(pid => !alive.Contains(pid));
                var exited = _trackedProcesses.Keys.Where(pid => !alive.Contains(pid)).ToArray();
                foreach (var pid in exited)
                {
                    var game = _trackedProcesses[pid];
                    _trackedProcesses.Remove(pid);
                    if (_trackedProcesses.Values.Any(g => g.Key.Equals(game.Key, StringComparison.OrdinalIgnoreCase))) continue;
                    await HandleGameExitedAsync(game);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.Log($"Watcher loop error: {ex}"); }

            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task HandleGameStartedAsync(InstalledGame game, CancellationToken ct)
    {
        try
        {
            var identity = _database.ResolveIdentity(game, includeMediumCandidates: false);
            var entry = identity?.Entry;
            if (identity is null || entry?.NativeHdr != true)
            {
                _logger.Log($"Local HDR DB miss: {game.Name} [{game.Store}:{game.StoreId}]. HDR unchanged.");
                PublishStatus($"{game.Name}: not in HDR database");
                return;
            }

            _logger.Log($"Local HDR DB hit: {entry.Name} for {game.Name} [{game.Store}], source={entry.Source}, identity={identity.MatchType}, confidence={identity.ConfidenceLabel}, score={identity.Score}.");

            var rule = _rules.Get(game);
            if (rule.EnableDelayMs > 0)
            {
                _logger.Log($"{game.Name}: delaying HDR enable by {rule.EnableDelayMs} ms.");
                await Task.Delay(rule.EnableDelayMs, ct);

                // If the process disappeared while waiting, do not flash HDR on
                // for a game that has already closed.
                if (!_trackedProcesses.Values.Any(g => g.Key.Equals(game.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.Log($"{game.Name}: process exited during HDR enable delay; cancelled.");
                    return;
                }
            }

            await _stateGate.WaitAsync(ct);
            try
            {
                if (_activeHdrGames.ContainsKey(game.Key)) return;

                var displayName = string.IsNullOrWhiteSpace(rule.DisplayDeviceName)
                    ? ""
                    : rule.DisplayDeviceName;
                var displayKey = string.IsNullOrWhiteSpace(displayName) ? "<primary>" : displayName;
                var alreadyActiveOnDisplay = _activeHdrGames.Values.Any(v =>
                    v.Equals(displayKey, StringComparison.OrdinalIgnoreCase));

                _activeHdrGames[game.Key] = displayKey;

                if (!alreadyActiveOnDisplay)
                {
                    var state = _hdr.GetStateForDisplay(displayName);
                    _hdrStateBeforeByDisplay[displayKey] = state.AnyHdrEnabled;
                    _logger.Log($"Saving pre-game HDR state for {displayKey}: enabled={state.AnyHdrEnabled}, supportedTargets={state.SupportedTargetCount}.");
                    if (!state.AnyHdrEnabled)
                    {
                        _logger.Log($"Native-HDR game detected; enabling Windows HDR on {displayKey}.");
                        _hdr.SetHdrForDisplay(true, displayName);
                    }
                }
                PublishStatus($"HDR ON — {game.Name}");
            }
            finally { _stateGate.Release(); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.Log($"Game-start handler failed for {game.Name} [{game.Store}:{game.StoreId}]: {ex}");
            PublishStatus("TrueAuto HDR error — see log");
        }
    }

    private async Task HandleGameExitedAsync(InstalledGame game)
    {
        var rule = _rules.Get(game);
        if (rule.ExitGraceMs > 0)
        {
            _logger.Log($"{game.Name}: applying {rule.ExitGraceMs} ms exit grace period.");
            await Task.Delay(rule.ExitGraceMs);

            // Launchers sometimes replace/restart the real game process. If the
            // same game came back during the grace period, leave the HDR session.
            if (_trackedProcesses.Values.Any(g => g.Key.Equals(game.Key, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.Log($"{game.Name}: process returned during exit grace period; HDR session preserved.");
                return;
            }
        }

        await _stateGate.WaitAsync();
        try
        {
            if (!_activeHdrGames.Remove(game.Key, out var displayKey)) return;
            _logger.Log($"Native-HDR game exited: {game.Name} [{game.Store}:{game.StoreId}].");

            var stillActiveOnDisplay = _activeHdrGames.Values.Any(v =>
                v.Equals(displayKey, StringComparison.OrdinalIgnoreCase));

            if (!stillActiveOnDisplay)
            {
                var displayName = displayKey == "<primary>" ? "" : displayKey;
                _hdrStateBeforeByDisplay.TryGetValue(displayKey, out var beforeEnabled);

                if (rule.KeepHdrAfterExit)
                {
                    _logger.Log($"{game.Name}: per-game rule keeps HDR enabled after exit on {displayKey}.");
                }
                else if (!beforeEnabled)
                {
                    _hdr.SetHdrForDisplay(false, displayName);
                    _logger.Log($"Restored HDR to OFF on {displayKey}.");
                }
                else
                {
                    _logger.Log($"HDR was already enabled before the game on {displayKey}; leaving it enabled.");
                }

                _hdrStateBeforeByDisplay.Remove(displayKey);
            }

            if (_activeHdrGames.Count == 0)
                PublishStatus("Watching for games…");
        }
        finally { _stateGate.Release(); }
    }

    public void Dispose()
    {
        _cts.Cancel(); _cts.Dispose(); _stateGate.Dispose();
    }
}
