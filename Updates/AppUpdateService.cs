using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json;
using AutoHDR.UI;

namespace AutoHDR.Updates;

public enum AppUpdateChannel
{
    Stable,
    Canary
}

public sealed record AppUpdateInfo(
    bool Success,
    bool UpdateAvailable,
    string Message,
    string Version = "",
    string ReleaseType = "",
    string Notes = "",
    string PackageUrl = "",
    string Sha256 = "",
    string? MinimumVersion = null);

public sealed class AppUpdateService
{
    private readonly FileLogger _logger;
    private readonly string _appData;
    private readonly HttpClient _http;

    public AppUpdateService(string appData, FileLogger logger)
    {
        _appData = appData;
        _logger = logger;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrueAutoHDR/1.2.6");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.8));
    }

    public async Task<AppUpdateInfo> CheckAsync(AppUpdateChannel channel, string manifestUrl, CancellationToken ct = default)
    {
        manifestUrl = (manifestUrl ?? "").Trim();
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return new(false, false, "No valid app-update manifest URL is configured.");

        try
        {
            var json = await _http.GetStringAsync(uri, ct);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
                return new(false, false, "Update manifest is missing its version.");

            if (!Version.TryParse(manifest.Version, out var remote))
                return new(false, false, $"Invalid update version '{manifest.Version}'.");

            var current = typeof(AppUpdateService).Assembly.GetName().Version ?? new Version(0, 0);
            var available = remote > current;
            var type = string.IsNullOrWhiteSpace(manifest.ReleaseType)
                ? (channel == AppUpdateChannel.Canary ? "Canary" : "Stable")
                : manifest.ReleaseType.Trim();

            // The repository may intentionally publish a "nothing newer yet"
            // manifest without a package URL/hash. Only require package fields
            // when the manifest actually advertises a newer build.
            if (!available)
                return new(true, false, $"You already have the latest {channel} build.",
                    remote.ToString(), type, manifest.Notes ?? "", "", "", manifest.MinimumVersion);

            if (string.IsNullOrWhiteSpace(manifest.PackageUrl) ||
                string.IsNullOrWhiteSpace(manifest.Sha256))
                return new(false, false,
                    $"{type} {remote} is newer, but its update package is not published correctly yet.");

            return new(true, true,
                $"{type} {remote} is available.",
                remote.ToString(), type, manifest.Notes ?? "", manifest.PackageUrl.Trim(),
                NormalizeHash(manifest.Sha256), manifest.MinimumVersion);
        }
        catch (Exception ex)
        {
            _logger.Log($"App update check failed ({channel}): {ex}");
            return new(false, false, $"App update check failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DownloadAndStageAsync(AppUpdateInfo update, CancellationToken ct = default)
    {
        if (!update.UpdateAvailable) return (false, "No app update is available.");
        if (!Uri.TryCreate(update.PackageUrl, UriKind.Absolute, out var packageUri))
            return (false, "The update package URL is invalid.");

        try
        {
            var root = Path.Combine(_appData, "Updates", update.Version);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);

            var zipPath = Path.Combine(root, "update.zip");
            var download = await DownloadPackageAsync(packageUri, zipPath, ct);
            if (!download.Success)
                return (false, download.Message);

            var actual = Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(zipPath), ct)).ToLowerInvariant();
            if (!actual.Equals(NormalizeHash(update.Sha256), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(zipPath);
                _logger.Log($"Rejected update {update.Version}: SHA-256 mismatch. expected={update.Sha256}, actual={actual}");
                return (false, "The downloaded update failed SHA-256 verification and was deleted.");
            }

            var payload = Path.Combine(root, "payload");
            Directory.CreateDirectory(payload);
            ZipFile.ExtractToDirectory(zipPath, payload, overwriteFiles: true);

            // Prevent package path tricks from escaping the staging directory.
            var payloadRoot = Path.GetFullPath(payload) + Path.DirectorySeparatorChar;
            foreach (var file in Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories))
                if (!Path.GetFullPath(file).StartsWith(payloadRoot, StringComparison.OrdinalIgnoreCase))
                    return (false, "Unsafe update package path detected.");

            var installedUpdater = Path.Combine(AppContext.BaseDirectory, "TrueAutoHDR.Updater.exe");
            if (!File.Exists(installedUpdater))
                return (false, "TrueAutoHDR.Updater.exe is missing from this build.");

            // Run the updater from staging, not from the install directory.
            // That lets an update package safely replace the updater itself.
            var runnerDir = Path.Combine(root, "runner");
            Directory.CreateDirectory(runnerDir);
            var updater = Path.Combine(runnerDir, "TrueAutoHDR.Updater.exe");
            File.Copy(installedUpdater, updater, true);

            var target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var exe = Environment.ProcessPath ?? Path.Combine(target, "TrueAutoHDR.exe");
            var args = $"--wait {Environment.ProcessId} --source \"{payload}\" --target \"{target}\" --restart \"{exe}\"";
            Process.Start(new ProcessStartInfo(updater, args)
            {
                UseShellExecute = true,
                WorkingDirectory = runnerDir
            });
            return (true, "Update verified. TrueAuto HDR will close, patch changed files, and restart.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not stage app update {update.Version}: {ex}");
            return (false, $"Could not stage update: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> DownloadPackageAsync(
        Uri packageUri,
        string destinationPath,
        CancellationToken ct)
    {
        var direct = await TryDownloadAsync(packageUri, destinationPath, ct);
        if (direct.Success)
            return direct;

        // GitHub release assets can occasionally behave differently for an
        // application client than for a browser. If the normal browser-download
        // URL fails, resolve the release asset through GitHub's public API and
        // request the asset endpoint directly.
        if (TryParseGitHubReleaseAssetUrl(packageUri, out var owner, out var repo, out var tag, out var assetName))
        {
            _logger.Log(
                $"Direct GitHub asset download failed; attempting API fallback. " +
                $"repo={owner}/{repo}, tag={tag}, asset={assetName}");

            var apiAsset = await ResolveGitHubAssetApiUrlAsync(owner, repo, tag, assetName, ct);
            if (apiAsset is not null)
            {
                var fallback = await TryDownloadAsync(
                    apiAsset,
                    destinationPath,
                    ct,
                    githubApiAsset: true);

                if (fallback.Success)
                    return fallback;

                return (false,
                    $"GitHub release download failed both directly and through the GitHub API. {fallback.Message}");
            }

            return (false,
                $"GitHub returned an error for the release package, and the asset could not be resolved through the GitHub API. {direct.Message}");
        }

        return direct;
    }

    private async Task<(bool Success, string Message)> TryDownloadAsync(
        Uri uri,
        string destinationPath,
        CancellationToken ct,
        bool githubApiAsset = false)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("TrueAutoHDR/1.2.6");
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
                githubApiAsset ? "application/octet-stream" : "*/*"));

            if (uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            }

            _logger.Log($"Downloading app update: {uri}");

            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            var finalUri = response.RequestMessage?.RequestUri ?? uri;
            _logger.Log(
                $"App update HTTP {(int)response.StatusCode} {response.ReasonPhrase}; " +
                $"requested={uri}; final={finalUri}; contentType={response.Content.Headers.ContentType}; " +
                $"contentLength={response.Content.Headers.ContentLength?.ToString() ?? "unknown"}.");

            if (!response.IsSuccessStatusCode)
            {
                var body = "";
                try
                {
                    body = await response.Content.ReadAsStringAsync(ct);
                    if (body.Length > 600) body = body[..600] + "…";
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(body))
                    _logger.Log($"App update error response: {body}");

                return (false,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} while downloading the update.");
            }

            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                useAsync: true);
            await input.CopyToAsync(output, 1024 * 128, ct);
            await output.FlushAsync(ct);

            if (output.Length <= 0)
                return (false, "The update server returned an empty file.");

            _logger.Log($"App update download completed: {output.Length} bytes -> {destinationPath}");
            return (true, "Downloaded.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (false, "Update download was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.Log($"App update HTTP request failed for {uri}: {ex}");
            return (false, $"Update download failed: {ex.Message}");
        }
    }

    private async Task<Uri?> ResolveGitHubAssetApiUrlAsync(
        string owner,
        string repo,
        string tag,
        string assetName,
        CancellationToken ct)
    {
        try
        {
            var api = new Uri(
                $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/" +
                $"{Uri.EscapeDataString(repo)}/releases/tags/{Uri.EscapeDataString(tag)}");

            using var request = new HttpRequestMessage(HttpMethod.Get, api);
            request.Headers.UserAgent.ParseAdd("TrueAutoHDR/1.2.6");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            _logger.Log(
                $"GitHub release API HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {api}");

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) ||
                    !string.Equals(nameElement.GetString(), assetName, StringComparison.Ordinal))
                    continue;

                if (!asset.TryGetProperty("id", out var idElement) ||
                    !idElement.TryGetInt64(out var id))
                    continue;

                _logger.Log($"GitHub API resolved release asset '{assetName}' to asset id {id}.");
                return new Uri(
                    $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/" +
                    $"{Uri.EscapeDataString(repo)}/releases/assets/{id}");
            }

            _logger.Log($"GitHub API release exists but asset '{assetName}' was not found.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Log($"GitHub release API fallback failed: {ex}");
            return null;
        }
    }

    private static bool TryParseGitHubReleaseAssetUrl(
        Uri uri,
        out string owner,
        out string repo,
        out string tag,
        out string assetName)
    {
        owner = repo = tag = assetName = "";

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var match = Regex.Match(
            uri.AbsolutePath,
            @"^/([^/]+)/([^/]+)/releases/download/([^/]+)/([^/]+)$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;

        owner = Uri.UnescapeDataString(match.Groups[1].Value);
        repo = Uri.UnescapeDataString(match.Groups[2].Value);
        tag = Uri.UnescapeDataString(match.Groups[3].Value);
        assetName = Uri.UnescapeDataString(match.Groups[4].Value);
        return true;
    }

    private static string NormalizeHash(string value) =>
        new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();

    private sealed class UpdateManifest
    {
        public string Version { get; set; } = "";
        public string? MinimumVersion { get; set; }
        public string? ReleaseType { get; set; }
        public string PackageUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string? Notes { get; set; }
    }
}
