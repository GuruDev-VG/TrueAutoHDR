using System.Diagnostics;
using System.IO.Compression;
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
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public AppUpdateService(string appData, FileLogger logger)
    {
        _appData = appData;
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrueAutoHDR/1.2.4");
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
            await using (var input = await _http.GetStreamAsync(packageUri, ct))
            await using (var output = File.Create(zipPath))
                await input.CopyToAsync(output, ct);

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
