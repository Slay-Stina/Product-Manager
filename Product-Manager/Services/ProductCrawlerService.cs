using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using Product_Manager.Data;
using Product_Manager.Models;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Product_Manager.Services;

public partial class ProductCrawlerService
{
    private readonly ApplicationDbContext _context;
    private readonly CrawlerSettings _settings;
    private readonly ILogger<ProductCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private CookieContainer _cookieContainer;
    
    // Current brand configuration with selectors
    private BrandConfig? _currentBrandConfig;
    
    // Compiled regex for extracting attribute names from selectors
    [GeneratedRegex(@"^\[([^\]]+)\]$", RegexOptions.Compiled)]
    private static partial Regex AttributeSelectorRegex();

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
    
    /// <summary>
    /// Set the brand configuration to use for parsing products
    /// </summary>
    public void SetBrandConfig(BrandConfig brandConfig)
    {
        _currentBrandConfig = brandConfig;
        _logger.LogInformation("?? Using brand configuration: {BrandName}", brandConfig.BrandName);
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
        try
        {
            // Use brand-specific selector or fallback to generic ones
            string productContainerSelector;
            
            if (_currentBrandConfig != null && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductContainerSelector))
            {
                // Use the brand-configured product selector
                productContainerSelector = _currentBrandConfig.ProductContainerSelector;
                _logger.LogInformation("🎯 Using brand-specific selector from config: {Selector}", productContainerSelector);
            }
            else
            {
                // Fallback to generic selectors
                productContainerSelector = ".product-item, .product, [data-product], .product-grid__item";
                _logger.LogWarning("⚠️ No brand-specific product selector set, using generic selectors");
            }
            
            var productElements = document.QuerySelectorAll(productContainerSelector);

            _logger.LogInformation("Found {Count} potential product elements on page {Url}", productElements.Length, pageUrl);

            if (productElements.Length == 0)
            {
                // Log the page structure to help with debugging
                _logger.LogWarning("?? No products found with selector '{Selector}' on page: {Url}", productContainerSelector, pageUrl);
                _logger.LogInformation("?? Page title: '{Title}'", document.Title);
                _logger.LogInformation("?? Page body classes: {Classes}", document.Body?.ClassName ?? "(none)");
                
                // Try to find any common product-related elements
                var allDivs = document.QuerySelectorAll("div[class*='product'], div[id*='product'], article, .item, [data-product-id], [data-pid]");
                _logger.LogInformation("?? Found {Count} elements with potential product-related attributes", allDivs.Length);
                
                if (allDivs.Length > 0)
                {
                    _logger.LogInformation("?? Consider using one of these selectors:");
                    foreach (var div in allDivs.Take(5))
                    {
                        var className = div.ClassName;
                        var id = div.Id;
                        var dataPid = div.GetAttribute("data-pid");
                        _logger.LogInformation("  - Element: {TagName}, Class: '{ClassName}', ID: '{Id}', data-pid: '{DataPid}'", 
                            div.TagName, className ?? "(none)", id ?? "(none)", dataPid ?? "(none)");
                    }
                }
                
                return;
            }

            foreach (var productElement in productElements)
            {
                try
                {
                    // Extract product information using brand-specific selectors
                    string? articleNumber = null;
                    string? productName = null;
                    string? price = null;
                    string? description = null;
                    string? imageUrl = null;
                    
                    if (_currentBrandConfig != null)
                    {
                        // Extract SKU using ProductSkuSelector
                        if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductSkuSelector))
                        {
                            // Check if selector is for an attribute (e.g., "[data-pid]")
                            var attrMatch = AttributeSelectorRegex().Match(_currentBrandConfig.ProductSkuSelector);
                            if (attrMatch.Success)
                            {
                                // Extract attribute value from element
                                articleNumber = productElement.GetAttribute(attrMatch.Groups[1].Value);
                            }
                            else
                            {
                                // Use selector to find element containing SKU
                                articleNumber = ExtractText(productElement, _currentBrandConfig.ProductSkuSelector);
                            }
                        }
                        
                        // Use brand-specific selectors for other fields with null/whitespace guards
                        if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductNameSelector))
                        {
                            productName = ExtractText(productElement, _currentBrandConfig.ProductNameSelector);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductPriceSelector))
                        {
                            price = ExtractText(productElement, _currentBrandConfig.ProductPriceSelector);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductDescriptionSelector))
                        {
                            description = ExtractText(productElement, _currentBrandConfig.ProductDescriptionSelector);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductImageSelector))
                        {
                            imageUrl = ExtractImageUrl(productElement, _currentBrandConfig.ProductImageSelector);
                        }
                        
                        _logger.LogDebug("📊 Extracted - SKU: {SKU}, Name: {Name}, Price: {Price}", 
                            articleNumber ?? "(none)", productName ?? "(none)", price ?? "(none)");
                    }
                    else
                    {
                        // Fallback to generic extraction
                        articleNumber = ExtractText(productElement, ".product-id, .article-number, [data-article-id]");
                        productName = ExtractText(productElement, ".product-name, .product-title, h3, h2");
                        price = ExtractText(productElement, ".price, .product-price");
                        description = ExtractText(productElement, ".product-description, .description, p");
                        imageUrl = ExtractImageUrl(productElement, "img");
                    }

                    if (string.IsNullOrWhiteSpace(articleNumber))
                    {
                        _logger.LogDebug("?? Skipping element - no article number/SKU found");
                        continue;
                    }

                    // Combine product name and price into description if available
                    var fullDescription = description;
                    if (!string.IsNullOrWhiteSpace(productName))
                    {
                        fullDescription = productName;
                        if (!string.IsNullOrWhiteSpace(price))
                        {
                            fullDescription += $" - {price}";
                        }
                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            fullDescription += $" | {description}";
                        }
                    }

                    _logger.LogInformation("? Found product: SKU={ArticleNumber}, Name={Name}, Price={Price}, HasImage={HasImage}",
                        articleNumber, productName ?? "N/A", price ?? "N/A", !string.IsNullOrEmpty(imageUrl));

                    // Save to database
                    SaveProduct(articleNumber, null, fullDescription, imageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "? Error parsing individual product element");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? Error in ParseAndSaveProducts for page {Url}", pageUrl);
        }
    }

    private string? ExtractText(AngleSharp.Dom.IElement element, string selector)
    {
        var targetElement = element.QuerySelector(selector);
        return targetElement?.TextContent?.Trim();
    }

    private string? ExtractImageUrl(AngleSharp.Dom.IElement element, string selector)
    {
        var imgElement = element.QuerySelector(selector);
        if (imgElement == null)
            return null;

        // Try multiple common image attributes
        var imageUrl = imgElement.GetAttribute("src") 
                    ?? imgElement.GetAttribute("data-src") 
                    ?? imgElement.GetAttribute("data-original")
                    ?? imgElement.GetAttribute("data-lazy-src");

        // If we found a srcset, extract the first URL
        if (string.IsNullOrEmpty(imageUrl))
        {
            var srcset = imgElement.GetAttribute("srcset");
            if (!string.IsNullOrEmpty(srcset))
            {
                // srcset format: "url 1x, url 2x" or "url 480w, url 800w"
                var firstUrl = srcset.Split(',')[0].Trim().Split(' ')[0];
                imageUrl = firstUrl;
            }
        }

        return imageUrl;
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
