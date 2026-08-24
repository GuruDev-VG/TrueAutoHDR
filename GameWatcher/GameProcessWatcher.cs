using AutoHDR.Database;
using AutoHDR.HDR;
using AutoHDR.Models;

namespace AutoHDR.GameWatcher;

public sealed class GameProcessWatcher : IDisposable
{
    private readonly UnifiedGameDetector _games;
    private readonly HdrDatabase _database;
    private readonly HdrController _hdr;
    private readonly FileLogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<int, InstalledGame> _trackedProcesses = new();
    private readonly HashSet<int> _seenProcessIds = new();
    private readonly HashSet<string> _activeHdrGames = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private Task? _loop;
    private bool? _hdrStateBeforeFirstGame;

    public event Action<string>? StatusChanged;

    public GameProcessWatcher(UnifiedGameDetector games, HdrDatabase database, HdrController hdr, FileLogger logger)
    {
        _games = games; _database = database; _hdr = hdr; _logger = logger;
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
            await _stateGate.WaitAsync(ct);
            try
            {
                if (!_activeHdrGames.Add(game.Key)) return;
                if (_activeHdrGames.Count == 1)
                {
                    var state = _hdr.GetAggregateState();
                    _hdrStateBeforeFirstGame = state.AnyHdrEnabled;
                    _logger.Log($"Saving pre-game HDR state: enabled={_hdrStateBeforeFirstGame}, supportedTargets={state.SupportedTargetCount}.");
                    if (!state.AnyHdrEnabled)
                    {
                        _logger.Log("Native-HDR game detected; enabling Windows HDR.");
                        _hdr.SetHdrOnAllSupportedTargets(true);
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
        await _stateGate.WaitAsync();
        try
        {
            if (!_activeHdrGames.Remove(game.Key)) return;
            _logger.Log($"Native-HDR game exited: {game.Name} [{game.Store}:{game.StoreId}].");
            if (_activeHdrGames.Count == 0)
            {
                if (_hdrStateBeforeFirstGame == false)
                {
                    _hdr.SetHdrOnAllSupportedTargets(false);
                    _logger.Log("Restored HDR to OFF.");
                }
                else _logger.Log("HDR was already enabled before the game; leaving it enabled.");
                _hdrStateBeforeFirstGame = null;
                PublishStatus("Watching for games…");
            }
        }
        finally { _stateGate.Release(); }
    }

    public void Dispose()
    {
        _cts.Cancel(); _cts.Dispose(); _stateGate.Dispose();
    }
}
