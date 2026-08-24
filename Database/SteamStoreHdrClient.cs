using System.Net;
using System.Text.Json;

namespace AutoHDR.Database;

public sealed class SteamStoreHdrClient
{
    private readonly HttpClient _http;
    private readonly FileLogger _logger;

    public SteamStoreHdrClient(FileLogger logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrueAutoHDR/1.0 (+local Windows HDR utility)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<SteamHdrCheck> CheckAsync(string appId, CancellationToken ct = default)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(appId)}&l=english";
        try
        {
            using var response = await _http.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.Log($"Steam HDR check {appId}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                return new(false, false, $"HTTP {(int)response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty(appId, out var root) ||
                !root.TryGetProperty("success", out var success) || !success.GetBoolean() ||
                !root.TryGetProperty("data", out var data))
                return new(false, false, "No Steam store data");

            var hdr = false;
            if (data.TryGetProperty("categories", out var categories) && categories.ValueKind == JsonValueKind.Array)
            {
                foreach (var category in categories.EnumerateArray())
                {
                    var id = category.TryGetProperty("id", out var idNode) && idNode.TryGetInt32(out var n) ? n : -1;
                    var description = category.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    if (id == 61 || description.Contains("HDR", StringComparison.OrdinalIgnoreCase))
                    {
                        hdr = true;
                        break;
                    }
                }
            }

            _logger.Log($"Steam HDR check {appId}: HDR available={hdr}.");
            return new(true, hdr, hdr ? "Steam Store: HDR available" : "Steam Store: no HDR category");
        }
        catch (Exception ex)
        {
            _logger.Log($"Steam HDR check {appId} failed: {ex.Message}");
            return new(false, false, ex.Message);
        }
    }
}

public readonly record struct SteamHdrCheck(bool Success, bool HdrAvailable, string Detail);
