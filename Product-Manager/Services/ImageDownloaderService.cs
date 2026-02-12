using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;

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
    /// Find all valid image URLs on a page that contain the article number and return successful downloads
    /// </summary>
    public async Task<List<string>> FindAndValidateImageUrlsAsync(IHtmlDocument document, string articleNumber, string brandName)
    {
        _logger.LogInformation("🔍 Searching for valid image URLs containing article number: {ArticleNumber}", articleNumber);

        var validImageUrls = new List<string>();
        var potentialUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Generate search patterns to handle zero-padded color IDs
        var searchPatterns = GenerateArticleNumberPatterns(articleNumber);
        _logger.LogInformation("🔍 Searching for patterns: {Patterns}", string.Join(", ", searchPatterns));

        // 1. Find all image URLs in the page containing the article number (any pattern)
        var allImages = document.QuerySelectorAll("img, source, link[rel='preload'][as='image']");

        foreach (var element in allImages)
        {
            // Check various attributes where image URLs might be stored
            var attributes = new[] 
            { 
                "src", "data-src", "srcset", "data-srcset", 
                "data-original", "data-lazy-src", "href" 
            };

            foreach (var attr in attributes)
            {
                var value = element.GetAttribute(attr);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // Handle srcset format (URL width, URL width, ...)
                if (attr.Contains("srcset"))
                {
                    var urls = value.Split(',')
                        .Select(part => part.Trim().Split(' ')[0])
                        .Where(url => !string.IsNullOrWhiteSpace(url));

                    foreach (var url in urls)
                    {
                        if (MatchesAnyPattern(url, searchPatterns) && 
                            url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        {
                            potentialUrls.Add(url);
                        }
                    }
                }
                else if (MatchesAnyPattern(value, searchPatterns) && 
                         value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    potentialUrls.Add(value);
                }
            }
        }

        // 2. Also check inline styles and all elements with data attributes
        // Note: We can't use [data-*] selector as it's not valid CSS, so we query all elements
        var allElements = document.All;
        foreach (var element in allElements)
        {
            // Check style attribute for background-image
            var style = element.GetAttribute("style");
            if (!string.IsNullOrWhiteSpace(style) && style.Contains(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                var urlMatch = System.Text.RegularExpressions.Regex.Match(
                    style, 
                    @"url\(['""]?([^'""()]+\.jpg)['""]?\)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (urlMatch.Success)
                {
                    var url = urlMatch.Groups[1].Value;
                    if (MatchesAnyPattern(url, searchPatterns))
                    {
                        potentialUrls.Add(url);
                    }
                }
            }

            // Check all data-* attributes
            foreach (var attr in element.Attributes)
            {
                if (attr.Name.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
                {
                    var value = attr.Value;
                    if (!string.IsNullOrWhiteSpace(value) &&
                        MatchesAnyPattern(value, searchPatterns) &&
                        value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        potentialUrls.Add(value);
                    }
                }
            }
        }

        _logger.LogInformation("📋 Found {Count} potential image URLs containing article number", potentialUrls.Count);

        if (potentialUrls.Count == 0)
        {
            _logger.LogWarning("⚠️ No image URLs found containing article number: {ArticleNumber}", articleNumber);
            return validImageUrls;
        }

        // Log sample URLs
        var samples = potentialUrls.Take(3).ToList();
        _logger.LogInformation("📝 Sample URLs found:");
        foreach (var sample in samples)
        {
            _logger.LogInformation("   → {Url}", sample.Length > 100 ? sample.Substring(0, 100) + "..." : sample);
        }
        if (potentialUrls.Count > 3)
        {
            _logger.LogInformation("   ... and {More} more", potentialUrls.Count - 3);
        }

        // 3. Try downloading each URL and keep only successful ones
        _logger.LogInformation("🔄 Validating URLs by attempting downloads...");

        var validationTasks = potentialUrls.Select(async url =>
        {
            try
            {
                var absoluteUrl = MakeAbsoluteUrl(url);

                // Apply brand-specific transformations
                absoluteUrl = TransformCdnUrl(absoluteUrl, brandName);

                // Try HEAD request first (faster)
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, absoluteUrl);
                var headResponse = await _httpClient.SendAsync(headRequest);

                if (headResponse.IsSuccessStatusCode)
                {
                    _logger.LogDebug("✅ Valid image URL: {Url}", absoluteUrl);
                    return absoluteUrl;
                }
                else
                {
                    _logger.LogDebug("❌ Invalid image URL ({StatusCode}): {Url}", 
                        headResponse.StatusCode, absoluteUrl);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("❌ Error validating URL {Url}: {Message}", url, ex.Message);
                return null;
            }
        }).ToList();

        var results = await Task.WhenAll(validationTasks);
        validImageUrls = results.Where(url => url != null).Cast<string>().ToList();

        _logger.LogInformation("✅ Found {ValidCount}/{TotalCount} valid image URLs", 
            validImageUrls.Count, potentialUrls.Count);

        return validImageUrls;
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
            var absoluteUrl = MakeAbsoluteUrl(url);
            var data = await DownloadImageAsync(absoluteUrl);
            return (Url: absoluteUrl, Data: data);
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

    /// <summary>
    /// Generate article number search patterns to handle zero-padded color IDs
    /// Example: "9970239-5" generates ["9970239-5", "9970239-005"]
    /// </summary>
    private List<string> GenerateArticleNumberPatterns(string articleNumber)
    {
        var patterns = new List<string> { articleNumber };

        // Check if article number has format: XXXXXXX-Y (where Y is 1-2 digits)
        var match = System.Text.RegularExpressions.Regex.Match(
            articleNumber, 
            @"^(\d+)-(\d{1,2})$");

        if (match.Success)
        {
            var baseNumber = match.Groups[1].Value;
            var colorId = match.Groups[2].Value;

            // If color ID is 1-2 digits, also try zero-padded version
            if (colorId.Length < 3)
            {
                var paddedColorId = colorId.PadLeft(3, '0');
                var paddedArticleNumber = $"{baseNumber}-{paddedColorId}";
                patterns.Add(paddedArticleNumber);
                _logger.LogDebug("🔢 Generated zero-padded pattern: {Original} → {Padded}", 
                    articleNumber, paddedArticleNumber);
            }
        }

        return patterns;
    }

    /// <summary>
    /// Check if a string matches any of the given patterns
    /// </summary>
    private bool MatchesAnyPattern(string value, List<string> patterns)
    {
        return patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
