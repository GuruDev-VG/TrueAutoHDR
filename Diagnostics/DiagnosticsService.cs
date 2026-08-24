using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AutoHDR.Database;
using AutoHDR.GameWatcher;
using AutoHDR.HDR;
using AutoHDR.Rules;
using AutoHDR.UI;

namespace AutoHDR.Diagnostics;

public sealed class DiagnosticsService
{
    private readonly UnifiedGameDetector _games;
    private readonly HdrDatabase _database;
    private readonly HdrController _hdr;
    private readonly GameRuleStore _rules;
    private readonly AppSettings _settings;

    public DiagnosticsService(
        UnifiedGameDetector games,
        HdrDatabase database,
        HdrController hdr,
        GameRuleStore rules,
        AppSettings settings)
    {
        _games = games;
        _database = database;
        _hdr = hdr;
        _rules = rules;
        _settings = settings;
    }

    // Generated only when the user asks for it. No diagnostics polling or
    // retained telemetry exists while TrueAuto HDR is idle.
    public string BuildReport()
    {
        var sb = new StringBuilder();
        var assembly = typeof(DiagnosticsService).Assembly.GetName();

        sb.AppendLine("TrueAuto HDR diagnostic report");
        sb.AppendLine($"Version: {assembly.Version}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Process working set: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
        sb.AppendLine($"Update channel: {_settings.UpdateChannel}");
        sb.AppendLine($"Run at startup: {_settings.RunAtStartup}");
        sb.AppendLine($"HDR database: {_database.Count} total ({_database.UserCount} user)");
        sb.AppendLine($"Per-game rules: {_rules.Count}");

        try
        {
            var hdr = _hdr.GetAggregateState();
            sb.AppendLine($"Primary display HDR supported: {hdr.SupportedTargetCount > 0}");
            sb.AppendLine($"Primary display HDR enabled: {hdr.AnyHdrEnabled}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Primary display HDR query: ERROR {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            var screens = Screen.AllScreens;
            sb.AppendLine($"Displays: {screens.Length}");
            foreach (var screen in screens)
            {
                sb.AppendLine(
                    $"  {screen.DeviceName}: {screen.Bounds.Width}x{screen.Bounds.Height}, " +
                    $"primary={screen.Primary}");
            }
        }
        catch { }

        try
        {
            var installed = _games.GetInstalledGames(false).ToList();
            sb.AppendLine($"Detected installed games: {installed.Count}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Installed-game enumeration: ERROR {ex.GetType().Name}: {ex.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("Generated on demand; no personal account credentials are included.");
        return sb.ToString();
    }
}
