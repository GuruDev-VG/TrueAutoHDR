using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using AutoHDR.Models;

namespace AutoHDR.Database;

public sealed record DatabaseUpdateResult(bool Success, bool Updated, string Message, string? Version = null);

public sealed class DatabaseUpdater
{
    private sealed class Manifest
    {
        public string Version { get; set; } = "";
        public string DatabaseUrl { get; set; } = "";
        public string? Sha256 { get; set; }
    }

    private readonly HdrDatabase _database;
    private readonly string _databasePath;
    private readonly string _versionPath;
    private readonly FileLogger _logger;
    private readonly HttpClient _http;

    public DatabaseUpdater(HdrDatabase database, string databasePath, string versionPath, FileLogger logger)
    {
        _database = database;
        _databasePath = databasePath;
        _versionPath = versionPath;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TrueAutoHDR", "1.3.1"));
    }

    public string CurrentVersion
    {
        get
        {
            try { return File.Exists(_versionPath) ? File.ReadAllText(_versionPath).Trim() : "bundled"; }
            catch { return "unknown"; }
        }
    }

    public async Task<DatabaseUpdateResult> CheckAndUpdateAsync(string manifestUrl, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri) ||
            (manifestUri.Scheme != Uri.UriSchemeHttps && manifestUri.Scheme != Uri.UriSchemeHttp))
            return new(false, false, "Set a valid database manifest URL in Settings.");

        try
        {
            _logger.Log($"Checking HDR database update: {manifestUri}");
            var manifestJson = await _http.GetStringAsync(manifestUri, ct);
            var manifest = JsonSerializer.Deserialize<Manifest>(manifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
                return new(false, false, "The database manifest is invalid.");

            var current = CurrentVersion;

            // A live manifest with no databaseUrl means the maintained database
            // channel exists, but no standalone database package has been
            // published yet. PCGamingWiki verification can still run normally.
            if (string.IsNullOrWhiteSpace(manifest.DatabaseUrl))
                return new(true, false,
                    $"No separate TrueAuto HDR database package is published yet (manifest {manifest.Version}).",
                    manifest.Version.Trim());

            if (!Uri.TryCreate(manifest.DatabaseUrl, UriKind.Absolute, out var dbUri))
                return new(false, false, "The database manifest contains an invalid database URL.");
            if (string.Equals(current, manifest.Version.Trim(), StringComparison.OrdinalIgnoreCase))
                return new(true, false, $"HDR database is already up to date ({current}).", current);

            var bytes = await _http.GetByteArrayAsync(dbUri, ct);
            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var actual = Convert.ToHexString(SHA256.HashData(bytes));
                var expected = manifest.Sha256.Replace(" ", "").Trim();
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    return new(false, false, "Database SHA-256 validation failed.");
            }

            // Validate the downloaded payload before replacing the active database.
            var parsed = JsonSerializer.Deserialize<Dictionary<string, HdrGame>>(bytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed is null || parsed.Count == 0)
                return new(false, false, "Downloaded HDR database is empty or invalid.");

            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            var temp = _databasePath + ".download";
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, _databasePath, true);
            await File.WriteAllTextAsync(_versionPath, manifest.Version.Trim(), ct);

            _database.ReloadBundledDatabase();
            _logger.Log($"HDR database updated: {current} -> {manifest.Version}, {parsed.Count} entries.");
            return new(true, true, $"HDR database updated to {manifest.Version} ({parsed.Count} games).", manifest.Version);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new(false, false, "Database update cancelled.");
        }
        catch (Exception ex)
        {
            _logger.Log($"HDR database update failed: {ex}");
            return new(false, false, $"Database update failed: {ex.Message}");
        }
    }
}
