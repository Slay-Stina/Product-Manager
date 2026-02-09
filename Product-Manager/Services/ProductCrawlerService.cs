using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using Product_Manager.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Product_Manager.Services;

public class ProductCrawlerService
{
    private readonly ApplicationDbContext _context;
    private readonly CrawlerSettings _settings;
    private readonly ILogger<ProductCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private CookieContainer _cookieContainer;

    public ProductCrawlerService(
        ApplicationDbContext context,
        CrawlerSettings settings,
        ILogger<ProductCrawlerService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _settings = settings;
        _logger = logger;
        
        // Configure HttpClient with UTF-8 encoding support
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.AcceptCharset.Clear();
        _httpClient.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));
        
        _cookieContainer = new CookieContainer();
        
        // Ensure UTF-8 encoding is registered
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            _logger.LogInformation("?? Attempting to authenticate at {LoginUrl}", _settings.LoginUrl);
            
            // Check if authentication is needed
            if (string.IsNullOrWhiteSpace(_settings.LoginUrl) || 
                _settings.LoginUrl.Contains("example.com"))
            {
                _logger.LogWarning("?? Login URL is not configured or uses example.com. Skipping authentication.");
                _logger.LogInformation("?? If the site doesn't require login, this is OK. Otherwise, update CrawlerSettings in appsettings.json");
                return true; // Allow crawling without authentication for public sites
            }

            if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogWarning("?? Username or password is empty. Skipping authentication.");
                _logger.LogInformation("?? If the site doesn't require login, this is OK. Otherwise, configure credentials.");
                return true; // Allow crawling without authentication
            }

            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>(_settings.UsernameFieldName, _settings.Username),
                new KeyValuePair<string, string>(_settings.PasswordFieldName, _settings.Password)
            });

            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            using var client = new HttpClient(handler);
            var response = await client.PostAsync(_settings.LoginUrl, loginData);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("? Authentication successful");
                return true;
            }

            _logger.LogWarning("? Authentication failed with status code: {StatusCode}", response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Response body: {ResponseBody}", responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? Error during authentication");
            return false;
        }
    }

    public async Task StartCrawlingAsync()
    {
        _logger.LogInformation("?? Starting crawler for {TargetUrl}", _settings.TargetUrl);
        
        // Check if URL is configured
        if (string.IsNullOrWhiteSpace(_settings.TargetUrl) || _settings.TargetUrl.Contains("example.com"))
        {
            _logger.LogError("? Target URL is not configured or uses example.com. Please update CrawlerSettings in appsettings.json");
            _logger.LogError("?? Set 'CrawlerSettings:TargetUrl' to your actual website URL");
            return;
        }

        // Authenticate first
        if (!await AuthenticateAsync())
        {
            _logger.LogError("? Failed to authenticate. Aborting crawl.");
            return;
        }

        var config = new CrawlConfiguration
        {
            MaxPagesToCrawl = _settings.MaxPagesToCrawl,
            MinCrawlDelayPerDomainMilliSeconds = _settings.CrawlDelayMilliseconds,
            MaxConcurrentThreads = 1,
            IsUriRecrawlingEnabled = false,
            IsExternalPageCrawlingEnabled = false,
            IsExternalPageLinksCrawlingEnabled = false,
            HttpServicePointConnectionLimit = 200,
            HttpRequestTimeoutInSeconds = 15,
            HttpRequestMaxAutoRedirects = 7,
            IsHttpRequestAutoRedirectsEnabled = true,
            UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
            IsRespectRobotsDotTextEnabled = false
        };

        var crawler = new PoliteWebCrawler(config);

        // Subscribe to events
        crawler.PageCrawlCompleted += ProcessPage;

        var crawlResult = await crawler.CrawlAsync(new Uri(_settings.TargetUrl));

        if (crawlResult.ErrorOccurred)
        {
            _logger.LogError("? Crawl error: {ErrorMessage}", crawlResult.ErrorException?.Message);
            if (crawlResult.ErrorException != null)
            {
                _logger.LogError("Stack trace: {StackTrace}", crawlResult.ErrorException.StackTrace);
            }
        }
        else
        {
            _logger.LogInformation("? Crawl completed successfully!");
        }

        _logger.LogInformation("?? Crawled {PageCount} pages", crawlResult.CrawlContext.CrawledCount);
    }

    private void ProcessPage(object? sender, PageCrawlCompletedArgs e)
    {
        var httpStatus = e.CrawledPage.HttpResponseMessage.StatusCode;
        var rawPageText = e.CrawledPage.Content.Text;

        _logger.LogInformation("Page crawled: {Url} [{StatusCode}]", e.CrawledPage.Uri, httpStatus);

        if (httpStatus != HttpStatusCode.OK || string.IsNullOrEmpty(rawPageText))
        {
            return;
        }

        try
        {
            var htmlDocument = e.CrawledPage.AngleSharpHtmlDocument;
            if (htmlDocument == null)
            {
                return;
            }

            // Parse product information from the page
            ParseAndSaveProducts(htmlDocument, e.CrawledPage.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing page {Url}", e.CrawledPage.Uri);
        }
    }

    private void ParseAndSaveProducts(IHtmlDocument document, string pageUrl)
    {
        // TODO: Customize these selectors based on the actual HTML structure of your target website
        // This is a template that you'll need to adjust

        try
        {
            // Example: Find all product containers (adjust selector as needed)
            var productElements = document.QuerySelectorAll(".product-item, .product, [data-product]");

            _logger.LogInformation("Found {Count} potential product elements on page {Url}", productElements.Length, pageUrl);

            if (productElements.Length == 0)
            {
                // Log the page structure to help with debugging
                _logger.LogWarning("?? No products found with current selectors on page: {Url}", pageUrl);
                _logger.LogInformation("?? Page title: '{Title}'", document.Title);
                _logger.LogInformation("?? Page body classes: {Classes}", document.Body?.ClassName ?? "(none)");
                
                // Log a sample of the page HTML to help identify the correct selectors
                var bodyHtml = document.Body?.InnerHtml;
                if (!string.IsNullOrEmpty(bodyHtml))
                {
                    var sample = bodyHtml.Length > 1000 ? bodyHtml.Substring(0, 1000) : bodyHtml;
                    _logger.LogDebug("?? Page HTML sample (first 1000 chars):\n{HtmlSample}", sample);
                }
                
                // Try to find any common product-related elements
                var allDivs = document.QuerySelectorAll("div[class*='product'], div[id*='product'], article, .item, [data-product-id]");
                _logger.LogInformation("?? Found {Count} elements with potential product-related attributes", allDivs.Length);
                
                if (allDivs.Length > 0)
                {
                    _logger.LogInformation("?? Consider using one of these selectors:");
                    foreach (var div in allDivs.Take(5))
                    {
                        var className = div.ClassName;
                        var id = div.Id;
                        _logger.LogInformation("  - Element: {TagName}, Class: '{ClassName}', ID: '{Id}'", 
                            div.TagName, className ?? "(none)", id ?? "(none)");
                    }
                }
            }

            foreach (var productElement in productElements)
            {
                try
                {
                    // Extract product information (adjust selectors based on actual HTML)
                    var articleNumber = ExtractText(productElement, ".product-id, .article-number, [data-article-id]");
                    var colorId = ExtractText(productElement, ".color-id, .product-color, [data-color]");
                    var description = ExtractText(productElement, ".product-description, .description, p");
                    var imageUrl = ExtractImageUrl(productElement, "img");

                    if (string.IsNullOrWhiteSpace(articleNumber))
                    {
                        _logger.LogDebug("Skipping element - no article number found");
                        continue; // Skip if no article number found
                    }

                    _logger.LogInformation("Found product: Article={ArticleNumber}, Color={ColorId}, HasImage={HasImage}",
                        articleNumber, colorId ?? "N/A", !string.IsNullOrEmpty(imageUrl));

                    // Save to database
                    SaveProduct(articleNumber, colorId, description, imageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing individual product element");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ParseAndSaveProducts for page {Url}", pageUrl);
        }
    }

    private string? ExtractText(AngleSharp.Dom.IElement element, string selector)
    {
        var targetElement = element.QuerySelector(selector);
        return targetElement?.TextContent?.Trim();
    }

    private string? ExtractImageUrl(AngleSharp.Dom.IElement element, string selector)
    {
        var imgElement = element.QuerySelector(selector) as IHtmlImageElement;
        return imgElement?.Source;
    }

    private void SaveProduct(string articleNumber, string? colorId, string? description, string? imageUrl)
    {
        try
        {
            var existingProduct = _context.Products
                .FirstOrDefault(p => p.ArticleNumber == articleNumber && p.ColorId == colorId);

            if (existingProduct != null)
            {
                // Update existing product
                existingProduct.Description = description;
                existingProduct.ImageUrl = imageUrl;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    existingProduct.ImageData = DownloadImage(imageUrl).Result;
                }

                _logger.LogInformation("Updated product: {ArticleNumber} - {ColorId}", articleNumber, colorId);
            }
            else
            {
                // Create new product
                var product = new Product
                {
                    ArticleNumber = articleNumber,
                    ColorId = colorId,
                    Description = description,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    product.ImageData = DownloadImage(imageUrl).Result;
                }

                _context.Products.Add(product);
                _logger.LogInformation("Added new product: {ArticleNumber} - {ColorId}", articleNumber, colorId);
            }

            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving product {ArticleNumber}", articleNumber);
        }
    }

    private async Task<byte[]?> DownloadImage(string imageUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            // Handle relative URLs
            if (!imageUrl.StartsWith("http"))
            {
                var baseUri = new Uri(_settings.TargetUrl);
                imageUrl = new Uri(baseUri, imageUrl).ToString();
            }

            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            _logger.LogInformation("Downloaded image from {ImageUrl}", imageUrl);
            return imageBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image from {ImageUrl}", imageUrl);
            return null;
        }
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _context.Products.ToListAsync();
    }

    public async Task<Product?> GetProductByArticleNumberAsync(string articleNumber)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber);
    }
}
