using System.Net;

namespace Product_Manager.Services;

/// <summary>
/// Service responsible for downloading and processing product images
/// </summary>
public class ImageDownloaderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageDownloaderService> _logger;
    private readonly string _baseUrl;

    public ImageDownloaderService(
        IHttpClientFactory httpClientFactory,
        ILogger<ImageDownloaderService> logger,
        CrawlerSettings settings)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _baseUrl = settings.TargetUrl;
    }

    /// <summary>
    /// Download image from URL and return as byte array
    /// </summary>
    public async Task<byte[]?> DownloadImageAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            // Handle relative URLs
            var absoluteUrl = MakeAbsoluteUrl(imageUrl);

            var imageBytes = await _httpClient.GetByteArrayAsync(absoluteUrl);
            _logger.LogDebug("✓ Downloaded image from {ImageUrl} ({Size} bytes)", 
                absoluteUrl, imageBytes.Length);
            return imageBytes;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to download image from {ImageUrl}: {Message}", 
                imageUrl, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error downloading image from {ImageUrl}", imageUrl);
            return null;
        }
    }

    /// <summary>
    /// Download multiple images concurrently
    /// </summary>
    public async Task<List<(string Url, byte[]? Data)>> DownloadImagesAsync(List<string> imageUrls)
    {
        var downloadTasks = imageUrls.Select(async url => 
        {
            var data = await DownloadImageAsync(url);
            return (Url: url, Data: data);
        });

        var results = await Task.WhenAll(downloadTasks);
        var successCount = results.Count(r => r.Data != null);
        
        _logger.LogInformation("📥 Downloaded {Success}/{Total} images", successCount, imageUrls.Count);
        
        return results.ToList();
    }

    /// <summary>
    /// Convert relative URL to absolute URL
    /// </summary>
    private string MakeAbsoluteUrl(string url)
    {
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return url;

        var baseUri = new Uri(_baseUrl);
        return new Uri(baseUri, url).ToString();
    }

    /// <summary>
    /// Transform CDN URLs to public-facing URLs (brand-specific logic)
    /// </summary>
    public string TransformCdnUrl(string imageUrl, string brandName)
    {
        // GANT-specific transformation
        if (brandName.Equals("GANT", StringComparison.OrdinalIgnoreCase) &&
            imageUrl.Contains("production-eu01-gant.demandware.net"))
        {
            var transformed = imageUrl.Replace(
                "https://production-eu01-gant.demandware.net/on/demandware.static/-/Sites-gant-master/",
                "https://www.gant.se/dw/image/v2/BFLN_PRD/on/demandware.static/-/Sites-gant-master/"
            );
            
            _logger.LogDebug("🔄 Transformed CDN URL for {Brand}", brandName);
            return transformed;
        }

        return imageUrl;
    }
}
