using System.Net.Http;
using AutoHDR.Models;

namespace AutoHDR.UI;

/// <summary>
/// Loads Steam library artwork only when a selected game needs it.  There is no
/// background prefetch/polling: local Steam artwork is preferred and the CDN is
/// used once as a cache miss fallback.
/// </summary>
public sealed class SteamArtworkService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string _cacheDirectory;
    private readonly FileLogger _logger;

    public SteamArtworkService(FileLogger logger)
    {
        _logger = logger;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrueAutoHDR", "Cache", "Artwork");
    }

    public async Task<Image?> GetAsync(InstalledGame game, CancellationToken cancellationToken = default)
    {
        if (!game.IsSteam || string.IsNullOrWhiteSpace(game.StoreId)) return null;

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var cachePath = Path.Combine(_cacheDirectory, $"steam_{Sanitize(game.StoreId)}.jpg");
            if (File.Exists(cachePath)) return LoadUnlocked(cachePath);

            var local = FindLocalSteamArtwork(game);
            if (local is not null)
            {
                File.Copy(local, cachePath, true);
                return LoadUnlocked(cachePath);
            }

            // Standard public Steam CDN artwork. Try portrait library art first,
            // then header art so old/indie AppIDs still get a useful image.
            var urls = new[]
            {
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.StoreId}/library_600x900_2x.jpg",
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.StoreId}/library_600x900.jpg",
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.StoreId}/header.jpg"
            };

            foreach (var url in urls)
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (bytes.Length < 1024) continue;
                await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
                return LoadUnlocked(cachePath);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.Log($"Steam artwork load failed for {game.Name} ({game.StoreId}): {ex.Message}");
        }
        return null;
    }

    private static string? FindLocalSteamArtwork(InstalledGame game)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(game.InstallDirectory)) return null;
            var directory = new DirectoryInfo(game.InstallDirectory);
            // Typical path: <SteamRoot>\\steamapps\\common\\Game
            var steamApps = directory.Parent?.Parent;
            var steamRoot = steamApps?.Parent;
            if (steamRoot is null) return null;
            var userData = Path.Combine(steamRoot.FullName, "userdata");
            if (!Directory.Exists(userData)) return null;

            var names = new[]
            {
                $"{game.StoreId}p.jpg", $"{game.StoreId}p.png",
                $"{game.StoreId}.jpg", $"{game.StoreId}.png",
                $"{game.StoreId}_hero.jpg", $"{game.StoreId}_hero.png"
            };

            foreach (var user in Directory.EnumerateDirectories(userData))
            {
                var grid = Path.Combine(user, "config", "grid");
                if (!Directory.Exists(grid)) continue;
                foreach (var name in names)
                {
                    var candidate = Path.Combine(grid, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }
        catch { }
        return null;
    }

    private static Image? LoadUnlocked(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch { return null; }
    }

    private static string Sanitize(string value)
        => string.Concat(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));

    public void Dispose() => _http.Dispose();
}
