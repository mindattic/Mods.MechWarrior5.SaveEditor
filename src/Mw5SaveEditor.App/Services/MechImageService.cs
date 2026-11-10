using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace Mw5SaveEditor.App.Services;

/// <summary>
/// Fetches a chassis picture from Sarna.net's MediaWiki API once, caches it on disk, and serves
/// frozen bitmaps thereafter. Failures (offline, no picture) resolve to null and are not retried
/// until the next launch.
/// </summary>
public sealed class MechImageService : IDisposable
{
    private const string Api = "https://www.sarna.net/wiki/api.php";
    private const int ThumbnailWidth = 320;

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private readonly SemaphoreSlim _throttle = new(4);
    private readonly ConcurrentDictionary<string, Task<BitmapSource?>> _images = new(StringComparer.OrdinalIgnoreCase);

    public MechImageService(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MW5SaveEditor/2.0 (desktop save editor; mech thumbnails)");
    }

    public bool Enabled { get; set; } = true;

    public static Uri PageUrl(string sarnaTitle) =>
        new($"https://www.sarna.net/wiki/{Uri.EscapeDataString(sarnaTitle.Replace(' ', '_'))}");

    public Task<BitmapSource?> GetAsync(string sarnaTitle) =>
        _images.GetOrAdd(sarnaTitle, LoadAsync);

    public void Dispose()
    {
        _http.Dispose();
        _throttle.Dispose();
    }

    private async Task<BitmapSource?> LoadAsync(string title)
    {
        var file = Path.Combine(_cacheDir, CacheKey(title) + ".img");
        if (File.Exists(file))
        {
            return Decode(await File.ReadAllBytesAsync(file).ConfigureAwait(false));
        }

        if (!Enabled)
        {
            return null;
        }

        await _throttle.WaitAsync().ConfigureAwait(false);
        try
        {
            var url = await FindThumbnailAsync(title).ConfigureAwait(false);
            if (url is null)
            {
                return null;
            }

            var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
            var image = Decode(bytes);
            if (image is not null)
            {
                await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
            }

            return image;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private async Task<Uri?> FindThumbnailAsync(string title)
    {
        var query = $"{Api}?action=query&format=json&prop=pageimages&piprop=thumbnail&pithumbsize={ThumbnailWidth}"
            + $"&redirects=1&titles={Uri.EscapeDataString(title)}";
        using var response = await _http.GetAsync(query).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync().ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages))
        {
            return null;
        }

        foreach (var page in pages.EnumerateObject())
        {
            if (page.Value.TryGetProperty("thumbnail", out var thumb)
                && thumb.TryGetProperty("source", out var source)
                && Uri.TryCreate(source.GetString(), UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                return uri;
            }
        }

        return null;
    }

    private static BitmapImage? Decode(byte[] bytes)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 200;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string CacheKey(string title) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title.ToLowerInvariant())))[..24];
}
