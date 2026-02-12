using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Product_Manager.Data;
using Product_Manager.Models;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Product_Manager.Services;

public partial class ProductCrawlerService
{
    private readonly ApplicationDbContext _context;
    private readonly CrawlerSettings _settings;
    private readonly ILogger<ProductCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly PlaywrightCrawlerService _playwrightService;
    private CookieContainer _cookieContainer;

    // Current brand configuration with selectors
    private BrandConfig? _currentBrandConfig;

    // Crawl statistics
    private int _categoryPagesProcessed = 0;
    private int _productPagesProcessed = 0;
    private int _productsSaved = 0;
    private readonly HashSet<string> _productLinks = new();

    // Batch saving for performance
    private const int BATCH_SIZE = 50;
    private readonly List<Product> _productBatch = new();
    private readonly List<ProductImage> _imageBatch = new();

    public ProductCrawlerService(
        ApplicationDbContext context,
        CrawlerSettings settings,
        ILogger<ProductCrawlerService> logger,
        IHttpClientFactory httpClientFactory,
        PlaywrightCrawlerService playwrightService)
    {
        _context = context;
        _settings = settings;
        _logger = logger;
        _playwrightService = playwrightService;

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
        _logger.LogInformation("📋 Using brand configuration: {BrandName}", brandConfig.BrandName);
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
        _logger.LogInformation("🚀 Starting crawler for {TargetUrl}", _settings.TargetUrl);

        // Check if URL is configured
        if (string.IsNullOrWhiteSpace(_settings.TargetUrl) || _settings.TargetUrl.Contains("example.com"))
        {
            _logger.LogError("❌ Target URL is not configured or uses example.com. Please update CrawlerSettings in appsettings.json");
            _logger.LogError("💡 Set 'CrawlerSettings:TargetUrl' to your actual website URL");
            return;
        }

        // Authenticate first
        if (!await AuthenticateAsync())
        {
            _logger.LogError("❌ Failed to authenticate. Aborting crawl.");
            return;
        }

        // HYBRID APPROACH: Use Playwright for JavaScript-rendered pages
        if (_currentBrandConfig?.UseJavaScriptRendering == true)
        {
            await StartPlaywrightCrawlingAsync();
        }
        else
        {
            // Use traditional Abot2 crawler
            await StartAbot2CrawlingAsync();
        }
    }

    /// <summary>
    /// Hybrid approach: Use Playwright for category pages, HttpClient for product pages
    /// </summary>
    private async Task StartPlaywrightCrawlingAsync()
    {
        try
        {
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("🎭 HYBRID MODE: Playwright + HttpClient");
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (_currentBrandConfig == null)
            {
                _logger.LogError("❌ No brand config set");
                return;
            }

            // Step 1: Use Playwright to get product links from category page
            _logger.LogInformation("📂 Step 1: Using Playwright for category page (JavaScript rendering)");
            var productLinks = await _playwrightService.GetProductLinksAsync(
                _settings.TargetUrl,
                _currentBrandConfig
            );

            if (!productLinks.Any())
            {
                _logger.LogWarning("⚠️ No product links found. Possible reasons:");
                _logger.LogWarning("   1. Wrong ProductUrlPattern: '{Pattern}'", _currentBrandConfig.ProductUrlPattern);
                _logger.LogWarning("   2. Products take longer to load");
                _logger.LogWarning("   3. Products loaded via additional API calls");
                return;
            }

            // Step 2: Use fast HttpClient for product detail pages
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("🚀 Step 2: Using HttpClient for product pages (fast!)");
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var maxProducts = Math.Min(productLinks.Count, _settings.MaxPagesToCrawl - 1);
            _logger.LogInformation("📦 Processing {Count} product pages...", maxProducts);

            for (int i = 0; i < maxProducts; i++)
            {
                var productUrl = productLinks[i];
                _logger.LogInformation("📦 [{Current}/{Total}] Processing: {Url}", 
                    i + 1, maxProducts, productUrl);

                await CrawlProductPage(productUrl);
                _productPagesProcessed++;

                // Rate limiting
                if (i < maxProducts - 1)
                {
                    await Task.Delay(_settings.CrawlDelayMilliseconds);
                }
            }

            // Flush any remaining products in batch
            await FlushBatchAsync();

            // Final statistics
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("✅ Crawl completed successfully!");
            _logger.LogInformation("📊 Statistics:");
            _logger.LogInformation("   🔗 Product links found: {Links}", productLinks.Count);
            _logger.LogInformation("   🎯 Product pages processed: {Pages}", _productPagesProcessed);
            _logger.LogInformation("   💾 Products saved: {Saved}", _productsSaved);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Playwright crawling");
            // Try to flush batch even on error
            try
            {
                await FlushBatchAsync();
            }
            catch { /* Ignore errors during cleanup */ }
        }
    }

    /// <summary>
    /// Traditional Abot2 crawler (for non-JavaScript sites)
    /// </summary>
    private async Task StartAbot2CrawlingAsync()
    {
        _logger.LogInformation("🤖 Using traditional Abot2 crawler (no JavaScript)");

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

        // Flush any remaining products in batch
        await FlushBatchAsync();

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

            // Log statistics
            _logger.LogInformation("📊 Crawl Statistics:");
            _logger.LogInformation("   📄 Total pages crawled: {PageCount}", crawlResult.CrawlContext.CrawledCount);
            _logger.LogInformation("   📂 Category pages: {CategoryPages}", _categoryPagesProcessed);
            _logger.LogInformation("   🎯 Product pages: {ProductPages}", _productPagesProcessed);
            _logger.LogInformation("   💾 Products saved: {ProductsSaved}", _productsSaved);
            _logger.LogInformation("   🔗 Unique product links found: {ProductLinks}", _productLinks.Count);
        }

        _logger.LogInformation("?? Crawled {PageCount} pages", crawlResult.CrawlContext.CrawledCount);
    }

    private void ProcessPage(object? sender, PageCrawlCompletedArgs e)
    {
        // Event handlers can't be async, so we use GetAwaiter().GetResult()
        ProcessPageAsync(e).GetAwaiter().GetResult();
    }

    private async Task ProcessPageAsync(PageCrawlCompletedArgs e)
    {
        var httpStatus = e.CrawledPage.HttpResponseMessage.StatusCode;
        var rawPageText = e.CrawledPage.Content.Text;
        var pageUrl = e.CrawledPage.Uri.ToString();

        _logger.LogInformation("📄 Page crawled: {Url} [{StatusCode}]", pageUrl, httpStatus);

        if (httpStatus != HttpStatusCode.OK || string.IsNullOrEmpty(rawPageText))
        {
            _logger.LogWarning("⚠️ Skipping page - Invalid status or empty content");
            return;
        }

        try
        {
            var htmlDocument = e.CrawledPage.AngleSharpHtmlDocument;
            if (htmlDocument == null)
            {
                _logger.LogWarning("⚠️ Skipping page - Could not parse HTML");
                return;
            }

            // FLOW STEP 2 & 3: Identify page type by URL pattern
            if (_currentBrandConfig?.CrawlProductPages == true && 
                !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductUrlPattern) &&
                pageUrl.Contains(_currentBrandConfig.ProductUrlPattern))
            {
                // FLOW STEP 4: This is a PRODUCT PAGE - Parse product details
                _logger.LogInformation("🎯 PRODUCT PAGE detected (contains '{Pattern}')", _currentBrandConfig.ProductUrlPattern);
                _productPagesProcessed++;
                await ParseProductPageData(htmlDocument, pageUrl);
            }
            else
            {
                // FLOW STEP 1 & 2: This is a CATEGORY/LISTING PAGE - Extract product links
                _logger.LogInformation("📂 CATEGORY PAGE detected (no '{Pattern}' in URL)", _currentBrandConfig?.ProductUrlPattern ?? "N/A");
                _categoryPagesProcessed++;

                // Log all links found on this page to help debug
                LogPageLinks(htmlDocument, pageUrl);

                await ParseAndSaveProducts(htmlDocument, pageUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing page {Url}", pageUrl);
        }
    }

    /// <summary>
    /// Log all links found on a page for debugging
    /// </summary>
    private void LogPageLinks(IHtmlDocument document, string pageUrl)
    {
        try
        {
            var allLinks = document.QuerySelectorAll("a[href]");
            var productLinks = allLinks
                .Select(a => a.GetAttribute("href"))
                .Where(href => !string.IsNullOrWhiteSpace(href) && 
                              !string.IsNullOrWhiteSpace(_currentBrandConfig?.ProductUrlPattern) &&
                              href.Contains(_currentBrandConfig.ProductUrlPattern))
                .Distinct()
                .ToList();

            if (productLinks.Any())
            {
                _logger.LogInformation("🔗 Found {Count} product links on this page:", productLinks.Count);
                
                // Add all discovered product links to the global set for accurate metrics
                foreach (var link in productLinks)
                {
                    _productLinks.Add(link);
                }

                // Only log a subset (up to 5) to avoid excessive output
                foreach (var link in productLinks.Take(5))
                {
                    _logger.LogInformation("   → {Link}", link);
                }
                if (productLinks.Count > 5)
                {
                    _logger.LogInformation("   ... and {More} more", productLinks.Count - 5);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No product links found on this page!");
                _logger.LogInformation("💡 Total links on page: {TotalLinks}", allLinks.Length);

                // Show some sample links to help debug
                var sampleLinks = allLinks
                    .Select(a => a.GetAttribute("href"))
                    .Where(href => !string.IsNullOrWhiteSpace(href))
                    .Distinct()
                    .Take(10)
                    .ToList();

                if (sampleLinks.Any())
                {
                    _logger.LogInformation("📝 Sample links found:");
                    foreach (var link in sampleLinks)
                    {
                        _logger.LogInformation("   → {Link}", link);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging page links");
        }
    }

    /// <summary>
    /// FLOW STEP 2: Process category/listing pages
    /// Purpose: Let Abot2 discover and follow product links automatically
    /// </summary>
    private async Task ParseAndSaveProducts(IHtmlDocument document, string pageUrl)
    {
        try
        {
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("📂 PROCESSING CATEGORY PAGE");
            _logger.LogInformation("🔗 URL: {Url}", pageUrl);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // If URL pattern matching is enabled, Abot2 will automatically:
            // 1. Discover all <a> links on this page
            // 2. Follow those links
            // 3. Call ProcessPage for each link
            // 4. ProcessPage will detect product pages by URL pattern
            if (_currentBrandConfig?.CrawlProductPages == true && 
                !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductUrlPattern))
            {
                _logger.LogInformation("✅ URL pattern matching enabled: '{Pattern}'", _currentBrandConfig.ProductUrlPattern);
                _logger.LogInformation("🤖 Abot2 will automatically:");
                _logger.LogInformation("   1️⃣  Discover all links on this page");
                _logger.LogInformation("   2️⃣  Crawl all discovered links (up to MaxPagesToCrawl limit)");
                _logger.LogInformation("   3️⃣  Parse product data from pages matching '{Pattern}'", _currentBrandConfig.ProductUrlPattern);
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                return; // Let Abot2 do its magic
            }

            _logger.LogWarning("⚠️ URL pattern matching NOT enabled - using fallback methods");

            // Legacy: Try to extract product URLs from JSON-LD if enabled
            if (_currentBrandConfig?.UseJsonLdExtraction == true)
            {
                _logger.LogInformation("🔍 Attempting JSON-LD extraction...");
                var productUrls = ExtractProductUrlsFromJsonLd(document);

                if (productUrls.Any())
                {
                    _logger.LogInformation("✅ Found {Count} product URLs in JSON-LD", productUrls.Count);

                    if (_currentBrandConfig.CrawlProductPages)
                    {
                        _logger.LogInformation("🌐 Manually crawling product pages...");
                        foreach (var productUrl in productUrls)
                        {
                            await CrawlProductPage(productUrl);
                        }
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ No product URLs in JSON-LD");
                }
            }

            // Fallback: Try to parse products directly from category page
            _logger.LogInformation("🔄 Falling back to direct category page parsing...");
            await ParseProductsFromCategoryPage(document, pageUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in ParseAndSaveProducts for page {Url}", pageUrl);
        }
    }

    private async Task ParseProductsFromCategoryPage(IHtmlDocument document, string pageUrl)
    {
        // Use brand-specific selector or fallback to generic ones
        string productContainerSelector;

        if (_currentBrandConfig != null && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductContainerSelector))
        {
            productContainerSelector = _currentBrandConfig.ProductContainerSelector;
            _logger.LogInformation("🎯 Using brand-specific selector: {Selector}", productContainerSelector);
        }
        else
        {
            // Fallback to generic selectors
            productContainerSelector = ".product-item, .product, [data-product], .product-grid__item";
            _logger.LogWarning("⚠️ No brand config set, using generic selectors");
        }

        var productElements = document.QuerySelectorAll(productContainerSelector);

        _logger.LogInformation("Found {Count} potential product elements on page {Url}", productElements.Length, pageUrl);

        if (productElements.Length == 0)
        {
            // Log the page structure to help with debugging
            _logger.LogWarning("⚠️ No products found with selector '{Selector}' on page: {Url}", productContainerSelector, pageUrl);
            _logger.LogInformation("📄 Page title: '{Title}'", document.Title);
            _logger.LogInformation("🔍 Page body classes: {Classes}", document.Body?.ClassName ?? "(none)");

            // Try to find any common product-related elements
            var allDivs = document.QuerySelectorAll("div[class*='product'], div[id*='product'], article, .item, [data-product-id], [data-pid]");
            _logger.LogInformation("🔎 Found {Count} elements with potential product-related attributes", allDivs.Length);

            if (allDivs.Length > 0)
            {
                _logger.LogInformation("💡 Consider using one of these selectors:");
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
                    // Extract SKU from data-pid attribute
                    articleNumber = productElement.GetAttribute("data-pid");

                    // Use brand-specific selectors for other fields
                    productName = ExtractText(productElement, _currentBrandConfig.ProductNameSelector);
                    price = ExtractText(productElement, _currentBrandConfig.ProductPriceSelector);
                    description = ExtractText(productElement, _currentBrandConfig.ProductDescriptionSelector);
                    imageUrl = ExtractImageUrl(productElement, _currentBrandConfig.ProductImageSelector);

                    _logger.LogDebug("🔍 Extracted - SKU: {SKU}, Name: {Name}, Price: {Price}", 
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
                    _logger.LogDebug("⚠️ Skipping element - no article number/SKU found");
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
                    if (!string.IsNullOrWhiteSpace(description))                        {
                        fullDescription += $" | {description}";
                    }
                }

                _logger.LogInformation("✅ Found product: SKU={ArticleNumber}, Name={Name}, Price={Price}, HasImage={HasImage}",
                    articleNumber, productName ?? "N/A", price ?? "N/A", !string.IsNullOrEmpty(imageUrl));

                // Save to database
                await SaveProduct(articleNumber, null, fullDescription, imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error parsing individual product element");
            }
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

        // For GANT: Check if this is inside a <picture> element with <source> tags
        // <picture><source data-srcset="https://www.gant.se/dw/image/...">
        var pictureParent = imgElement.ParentElement;
        if (pictureParent?.TagName?.Equals("PICTURE", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Look for <source> tags with data-srcset or srcset
            var sourceElements = pictureParent.QuerySelectorAll("source");
            foreach (var source in sourceElements)
            {
                var srcset = source.GetAttribute("data-srcset") ?? source.GetAttribute("srcset");
                if (!string.IsNullOrEmpty(srcset))
                {
                    // Extract first URL from srcset (format: "url 1x, url 2x")
                    var firstUrl = srcset.Split(',')[0].Trim().Split(' ')[0];

                    // Prefer URLs that start with the site domain (avoid CDN 403 errors)
                    if (firstUrl.StartsWith("https://www.gant.se/") || 
                        firstUrl.StartsWith("http://www.gant.se/") ||
                        !firstUrl.Contains("production-"))
                    {
                        return firstUrl;
                    }
                }
            }
        }

        // Try multiple common image attributes on the img element itself
        var imageUrl = imgElement.GetAttribute("src") 
                    ?? imgElement.GetAttribute("data-src") 
                    ?? imgElement.GetAttribute("data-original")
                    ?? imgElement.GetAttribute("data-lazy-src");

        // If we found a srcset, extract the first URL
        if (string.IsNullOrEmpty(imageUrl))
        {
            var srcset = imgElement.GetAttribute("srcset") ?? imgElement.GetAttribute("data-srcset");
            if (!string.IsNullOrEmpty(srcset))
            {
                // srcset format: "url 1x, url 2x" or "url 480w, url 800w"
                var firstUrl = srcset.Split(',')[0].Trim().Split(' ')[0];
                imageUrl = firstUrl;
            }
        }

        return imageUrl;
    }

    private async Task SaveProduct(string articleNumber, string? colorId, string? description, string? imageUrl, string? productUrl = null)
    {
        try
        {
            var existingProduct = _context.Products
                .Include(p => p.Images)
                .FirstOrDefault(p => p.ArticleNumber == articleNumber && p.ColorId == colorId);

            if (existingProduct != null)
            {
                // Update existing product
                existingProduct.Description = description;
                existingProduct.ImageUrl = imageUrl;
                existingProduct.ProductUrl = productUrl;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                // Maintain backward compatibility - update old ImageData
                if (!string.IsNullOrEmpty(imageUrl))
                {
#pragma warning disable CS0618
                    existingProduct.ImageData = await DownloadImage(imageUrl);
#pragma warning restore CS0618
                }

                _logger.LogInformation("   ♻️  Updated existing product");
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
                    ProductUrl = productUrl,
                    CreatedAt = DateTime.UtcNow
                };

                // Maintain backward compatibility - set old ImageData
                if (!string.IsNullOrEmpty(imageUrl))
                {
#pragma warning disable CS0618
                    product.ImageData = await DownloadImage(imageUrl);
#pragma warning restore CS0618
                }

                _context.Products.Add(product);
                _logger.LogInformation("   ➕ Created new product");
            }

            await _context.SaveChangesAsync();
            _productsSaved++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving product {ArticleNumber}", articleNumber);
        }
    }

    private async Task SaveProductWithDetails(string articleNumber, string? ean, string? colorId, decimal? price, string? description, List<string> imageUrls, string? productUrl = null)
    {
        try
        {
            var existingProduct = _context.Products
                .Include(p => p.Images)
                .FirstOrDefault(p => p.ArticleNumber == articleNumber && p.ColorId == colorId);

            if (existingProduct != null)
            {
                // Update existing product
                existingProduct.EAN = ean;
                existingProduct.Price = price;
                existingProduct.Description = description;
                existingProduct.ProductUrl = productUrl;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                // Update images - remove old ones and add new ones
                _context.ProductImages.RemoveRange(existingProduct.Images);

                for (int i = 0; i < imageUrls.Count; i++)
                {
                    var imageUrl = imageUrls[i];
                    var imageData = await DownloadImage(imageUrl);

                    existingProduct.Images.Add(new ProductImage
                    {
                        ImageUrl = imageUrl,
                        ImageData = imageData,
                        Order = i,
                        IsPrimary = i == 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Maintain backward compatibility
                if (imageUrls.Any())
                {
#pragma warning disable CS0618
                    existingProduct.ImageUrl = imageUrls[0];
                    existingProduct.ImageData = existingProduct.Images.FirstOrDefault()?.ImageData;
#pragma warning restore CS0618
                }

                _logger.LogInformation("   ♻️  Updated existing product with {ImageCount} images", imageUrls.Count);
            }
            else
            {
                // Create new product
                var product = new Product
                {
                    ArticleNumber = articleNumber,
                    EAN = ean,
                    ColorId = colorId,
                    Price = price,
                    Description = description,
                    ProductUrl = productUrl,
                    CreatedAt = DateTime.UtcNow
                };

                // Add images
                for (int i = 0; i < imageUrls.Count; i++)
                {
                    var imageUrl = imageUrls[i];
                    var imageData = await DownloadImage(imageUrl);

                    product.Images.Add(new ProductImage
                    {
                        ImageUrl = imageUrl,
                        ImageData = imageData,
                        Order = i,
                        IsPrimary = i == 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Maintain backward compatibility
                if (imageUrls.Any())
                {
#pragma warning disable CS0618
                    product.ImageUrl = imageUrls[0];
                    product.ImageData = product.Images.FirstOrDefault()?.ImageData;
#pragma warning restore CS0618
                }

                _context.Products.Add(product);
                _logger.LogInformation("   ➕ Created new product with {ImageCount} images", imageUrls.Count);
            }

            await _context.SaveChangesAsync();
            _productsSaved++;
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
        return await _context.Products
            .Include(p => p.Images)
            .ToListAsync();
    }

    public async Task<Product?> GetProductByArticleNumberAsync(string articleNumber)
    {
        return await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.ArticleNumber == articleNumber);
    }
    
    /// <summary>
    /// Extract product URLs from JSON-LD structured data
    /// </summary>
    private List<string> ExtractProductUrlsFromJsonLd(IHtmlDocument document)
    {
        var productUrls = new List<string>();
        
        try
        {
            // Find all JSON-LD script tags
            var jsonLdScripts = document.QuerySelectorAll("script[type='application/ld+json']");
            
            _logger.LogDebug("Found {Count} JSON-LD script tags", jsonLdScripts.Length);
            
            foreach (var script in jsonLdScripts)
            {
                try
                {
                    var jsonContent = script.TextContent;
                    if (string.IsNullOrWhiteSpace(jsonContent))
                        continue;
                    
                    using var jsonDoc = JsonDocument.Parse(jsonContent);
                    var root = jsonDoc.RootElement;
                    
                    // Check if this is a Product schema
                    if (root.TryGetProperty("@type", out var typeProperty) &&
                        typeProperty.GetString() == "Product")
                    {
                        // Extract the offer URL
                        if (root.TryGetProperty("offers", out var offers) &&
                            offers.TryGetProperty("url", out var urlProperty))
                        {
                            var url = urlProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                productUrls.Add(url);
                                _logger.LogDebug("📦 Found product URL: {Url}", url);
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("⚠️ Failed to parse JSON-LD: {Message}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error extracting product URLs from JSON-LD");
        }
        
        return productUrls;
    }
    
    /// <summary>
    /// Crawl an individual product page for detailed information
    /// </summary>
    private async Task CrawlProductPage(string productUrl)
    {
        try
        {
            _logger.LogInformation("🔗 Crawling product page: {Url}", productUrl);
            
            // Make sure URL is absolute
            if (!productUrl.StartsWith("http"))
            {
                var baseUri = new Uri(_settings.TargetUrl);
                productUrl = new Uri(baseUri, productUrl).ToString();
            }
            
            var response = await _httpClient.GetAsync(productUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("⚠️ Failed to load product page: {StatusCode}", response.StatusCode);
                return;
            }
            
            var html = await response.Content.ReadAsStringAsync();
            
            // Parse HTML using AngleSharp
            var parser = new HtmlParser();
            var document = parser.ParseDocument(html);
            
            // Extract product data from the product page
            await ParseProductPageData(document, productUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error crawling product page: {Url}", productUrl);
        }
    }
    
    /// <summary>
    /// FLOW STEP 4: Parse product data from an individual product detail page
    /// </summary>
    private async Task ParseProductPageData(IHtmlDocument document, string productUrl)
    {
        try
        {
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("🎯 PROCESSING PRODUCT PAGE");
            _logger.LogInformation("🔗 URL: {Url}", productUrl);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (_currentBrandConfig == null)
            {
                _logger.LogWarning("⚠️ No brand config set for product page parsing");
                return;
            }

            // Extract information from URL
            var urlInfo = ExtractInfoFromUrl(productUrl);
            if (urlInfo.Any())
            {
                _logger.LogInformation("📍 URL Analysis:");
                foreach (var kv in urlInfo)
                {
                    _logger.LogInformation("   • {Key}: {Value}", kv.Key, kv.Value);
                }
            }

            string? articleNumber = null;
            string? ean = null;
            string? colorId = null;
            string? productName = null;
            string? priceString = null;
            decimal? price = null;
            string? description = null;
            var imageUrls = new List<string>();

            // STEP 4A: Try JSON-LD first (fastest and most reliable)
            _logger.LogInformation("🔍 Step 1: Trying JSON-LD extraction...");
            var jsonLdScripts = document.QuerySelectorAll("script[type='application/ld+json']");
            _logger.LogInformation("   Found {Count} JSON-LD script tags", jsonLdScripts.Length);

            foreach (var script in jsonLdScripts)
            {
                try
                {
                    var jsonContent = script.TextContent;
                    if (string.IsNullOrWhiteSpace(jsonContent))
                        continue;

                    using var jsonDoc = JsonDocument.Parse(jsonContent);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("@type", out var typeProperty) && 
                        typeProperty.GetString() == "Product")
                    {
                        _logger.LogInformation("✅ Found Product schema in JSON-LD");

                        // Extract from JSON-LD
                        if (root.TryGetProperty("name", out var nameProperty))
                        {
                            productName = nameProperty.GetString();
                            _logger.LogInformation("   ✓ Name: {Name}", productName);
                        }

                        if (root.TryGetProperty("description", out var descProperty))
                        {
                            description = descProperty.GetString();
                            _logger.LogInformation("   ✓ Description: {Length} chars", description?.Length ?? 0);
                        }

                        if (root.TryGetProperty("color", out var colorProperty))
                        {
                            // Handle null color (e.g., gift cards)
                            if (colorProperty.ValueKind != JsonValueKind.Null)
                            {
                                colorId = colorProperty.GetString();
                                _logger.LogInformation("   ✓ Color: {Color}", colorId);
                            }
                            else
                            {
                                _logger.LogInformation("   ⚠️ Color is null (product has no color)");
                            }
                        }

                        // Handle image field - can be string, array, or object
                        if (root.TryGetProperty("image", out var imageProperty))
                        {
                            if (imageProperty.ValueKind == JsonValueKind.String)
                            {
                                var imageUrl = imageProperty.GetString();
                                if (!string.IsNullOrWhiteSpace(imageUrl))
                                    imageUrls.Add(imageUrl);
                            }
                            else if (imageProperty.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var imgElement in imageProperty.EnumerateArray())
                                {
                                    var imageUrl = imgElement.ValueKind == JsonValueKind.String 
                                        ? imgElement.GetString() 
                                        : (imgElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null);

                                    if (!string.IsNullOrWhiteSpace(imageUrl))
                                        imageUrls.Add(imageUrl);
                                }
                            }
                            else if (imageProperty.ValueKind == JsonValueKind.Object)
                            {
                                if (imageProperty.TryGetProperty("@id", out var idProp))
                                {
                                    var imageUrl = idProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(imageUrl))
                                        imageUrls.Add(imageUrl);
                                }
                                else if (imageProperty.TryGetProperty("url", out var urlProp))
                                {
                                    var imageUrl = urlProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(imageUrl))
                                        imageUrls.Add(imageUrl);
                                }
                            }
                            _logger.LogInformation("   ✓ Images: {Count}", imageUrls.Count);
                        }

                        // Extract EAN (European Article Number)
                        // Check for standard GTIN properties (gtin13 is EAN-13, gtin is generic)
                        if (root.TryGetProperty("gtin13", out var gtin13Property))
                        {
                            ean = gtin13Property.GetString();
                            _logger.LogInformation("   ✓ EAN (gtin13): {EAN}", ean);
                        }
                        else if (root.TryGetProperty("gtin", out var gtinProperty))
                        {
                            ean = gtinProperty.GetString();
                            _logger.LogInformation("   ✓ EAN (gtin): {EAN}", ean);
                        }
                        else if (root.TryGetProperty("sku", out var skuProperty))
                        {
                            // Some sites use 'sku' for EAN
                            ean = skuProperty.GetString();
                            _logger.LogInformation("   ✓ EAN (from sku field): {EAN}", ean);
                        }
                        else if (root.TryGetProperty("pid", out var pidProperty))
                        {
                            // Some sites use 'pid' for product identifier/EAN
                            ean = pidProperty.GetString();
                            _logger.LogInformation("   ✓ EAN (from pid field): {EAN}", ean);
                        }

                        if (root.TryGetProperty("productID", out var productIdProperty))
                        {
                            articleNumber = productIdProperty.GetString();
                            _logger.LogInformation("   ✓ Product ID: {ArticleNumber}", articleNumber);
                        }

                        if (root.TryGetProperty("offers", out var offers))
                        {
                            if (offers.TryGetProperty("price", out var priceProperty))
                            {
                                // Handle both string and number price formats
                                if (priceProperty.ValueKind == JsonValueKind.String)
                                {
                                    var priceStr = priceProperty.GetString();
                                    if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var priceValue))
                                    {
                                        price = priceValue;
                                        priceString = priceValue.ToString("F2");
                                    }
                                    else
                                    {
                                        priceString = priceStr;
                                    }
                                }
                                else if (priceProperty.ValueKind == JsonValueKind.Number)
                                {
                                    var priceValue = priceProperty.GetDecimal();
                                    price = priceValue;
                                    priceString = priceValue.ToString("F2");
                                }
                            }

                            if (offers.TryGetProperty("priceCurrency", out var currencyProperty))
                                priceString = $"{priceString} {currencyProperty.GetString()}";

                            _logger.LogInformation("   ✓ Price: {Price}", priceString);
                        }

                        break; // Found product data, stop looking
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("⚠️ Failed to parse JSON-LD: {Message}", ex.Message);
                }
            }

            // STEP 4B: Fill missing data from HTML selectors
            _logger.LogInformation("🔍 Step 2: Filling missing data from HTML selectors...");

            if (string.IsNullOrWhiteSpace(productName) && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductPageNameSelector))
            {
                productName = ExtractText(document.DocumentElement, _currentBrandConfig.ProductPageNameSelector);
                if (!string.IsNullOrWhiteSpace(productName))
                    _logger.LogInformation("   ✓ Name from HTML: {Name}", productName);
            }

            if (!price.HasValue && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductPagePriceSelector))
            {
                priceString = ExtractText(document.DocumentElement, _currentBrandConfig.ProductPagePriceSelector);
                if (!string.IsNullOrWhiteSpace(priceString))
                {
                    // Try to parse price from HTML (remove currency symbols, etc.)
                    var cleanPrice = Regex.Replace(priceString, @"[^\d.,]", "").Trim();
                    if (decimal.TryParse(cleanPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var priceValue))
                    {
                        price = priceValue;
                    }
                    _logger.LogInformation("   ✓ Price from HTML: {Price}", priceString);
                }
            }

            if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductPageDescriptionSelector))
            {
                description = ExtractText(document.DocumentElement, _currentBrandConfig.ProductPageDescriptionSelector);
                if (!string.IsNullOrWhiteSpace(description))
                    _logger.LogInformation("   ✓ Description from HTML: {Length} chars", description.Length);
            }

            // Extract multiple images from HTML if not found in JSON-LD
            if (!imageUrls.Any() && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductPageImageSelector))
            {
                var imageElements = document.DocumentElement.QuerySelectorAll(_currentBrandConfig.ProductPageImageSelector);
                foreach (var imgElement in imageElements)
                {
                    var imageUrl = ExtractImageUrl(imgElement, "img") ?? ExtractImageUrl(imgElement, "source");
                    if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.Contains("production-eu01-gant.demandware.net"))
                    {
                        imageUrls.Add(imageUrl);
                    }
                }
                if (imageUrls.Any())
                    _logger.LogInformation("   ✓ Using {Count} public image URLs from HTML (avoiding CDN 403 errors)", imageUrls.Count);
            }

            // FALLBACK: Transform CDN URLs to public-facing URLs
            for (int i = 0; i < imageUrls.Count; i++)
            {
                if (imageUrls[i].Contains("production-eu01-gant.demandware.net"))
                {
                    imageUrls[i] = imageUrls[i].Replace(
                        "https://production-eu01-gant.demandware.net/on/demandware.static/-/Sites-gant-master/",
                        "https://www.gant.se/dw/image/v2/BFLN_PRD/on/demandware.static/-/Sites-gant-master/"
                    );
                    _logger.LogInformation("   🔄 Transformed CDN URL to public URL for image {Index}", i + 1);
                }
            }

            if (string.IsNullOrWhiteSpace(colorId) && !string.IsNullOrWhiteSpace(_currentBrandConfig.ProductPageColorSelector))
            {
                colorId = ExtractText(document.DocumentElement, _currentBrandConfig.ProductPageColorSelector);
                if (!string.IsNullOrWhiteSpace(colorId))
                    _logger.LogInformation("   ✓ Color from HTML: {Color}", colorId);
            }

            // STEP 4C: Extract article number from URL if still missing
            if (string.IsNullOrWhiteSpace(articleNumber))
            {
                _logger.LogInformation("🔍 Step 3: Extracting article number from URL...");

                Match? urlMatch = null;
                urlMatch = Regex.Match(productUrl, @"/(\d{7,})\.html");
                if (!urlMatch.Success)
                    urlMatch = Regex.Match(productUrl, @"/(\d{7,})(?:[?#]|$)");
                if (!urlMatch.Success)
                    urlMatch = Regex.Match(productUrl, @"(\d{7,})");

                if (urlMatch.Success)
                {
                    articleNumber = urlMatch.Groups[1].Value;
                    _logger.LogInformation("   ✓ Article number from URL: {ArticleNumber}", articleNumber);
                }
                else if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductSkuSelector))
                {
                    articleNumber = ExtractText(document.DocumentElement, _currentBrandConfig.ProductSkuSelector);
                    if (!string.IsNullOrWhiteSpace(articleNumber))
                        _logger.LogInformation("   ✓ Article number from selector: {ArticleNumber}", articleNumber);
                }
            }

            if (string.IsNullOrWhiteSpace(articleNumber))
            {
                _logger.LogError("❌ FAILED: Could not extract article number from product page");
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                return;
            }

            // STEP 4D: Combine description
            var fullDescription = description;
            if (!string.IsNullOrWhiteSpace(productName))
            {
                fullDescription = productName;
                if (!string.IsNullOrWhiteSpace(priceString))
                    fullDescription += $" - {priceString}";
                if (!string.IsNullOrWhiteSpace(description))
                    fullDescription += $" | {description}";
            }

            // STEP 5: Save to database (using batch for performance)
            _logger.LogInformation("💾 Step 4: Adding product to batch...");
            _logger.LogInformation("   📦 Article Number: {ArticleNumber}", articleNumber);
            _logger.LogInformation("   🏷️  EAN: {EAN}", ean ?? "N/A");
            _logger.LogInformation("   📝 Name: {Name}", productName ?? "N/A");
            _logger.LogInformation("   💰 Price: {Price}", price?.ToString("C") ?? "N/A");
            _logger.LogInformation("   🎨 Color: {Color}", colorId ?? "N/A");
            _logger.LogInformation("   🖼️  Images: {Count}", imageUrls.Count);
            _logger.LogInformation("   🔗 URL: {Url}", productUrl);

            // Use batch method for better performance
            if (imageUrls.Any())
            {
                await AddProductToBatchAsync(articleNumber, ean, colorId, price, fullDescription, imageUrls, productUrl);
            }
            else
            {
                // For products without images, still use immediate save to keep it simple
                await SaveProduct(articleNumber, colorId, fullDescription, null, productUrl);
            }

            _logger.LogInformation("✅ SUCCESS: Product added to batch");
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error parsing product page data from {Url}", productUrl);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
    }

    /// <summary>
    /// Extract useful information from product URL
    /// Many URLs contain hints about product name, category, color, etc.
    /// </summary>
    private Dictionary<string, string?> ExtractInfoFromUrl(string productUrl)
    {
        var info = new Dictionary<string, string?>();

        try
        {
            var uri = new Uri(productUrl);
            var path = uri.AbsolutePath;

            // Extract product slug (usually the last meaningful part before .html or id)
            // Example: /products/cool-t-shirt-blue.html -> "cool-t-shirt-blue"
            var slugMatch = Regex.Match(path, @"/([a-z0-9-]+)(?:\.html)?(?:\?|$)", RegexOptions.IgnoreCase);
            if (slugMatch.Success)
            {
                var slug = slugMatch.Groups[1].Value;
                info["slug"] = slug;

                // Try to extract color from slug (common patterns: -blue, -red, -black)
                var colorMatch = Regex.Match(slug, @"-(red|blue|green|black|white|yellow|pink|purple|orange|grey|gray|brown|navy|beige)$", RegexOptions.IgnoreCase);
                if (colorMatch.Success)
                {
                    info["color_hint"] = colorMatch.Groups[1].Value;
                }

                // Try to extract size hints
                var sizeMatch = Regex.Match(slug, @"-(xs|s|m|l|xl|xxl|\d{2,3})$", RegexOptions.IgnoreCase);
                if (sizeMatch.Success)
                {
                    info["size_hint"] = sizeMatch.Groups[1].Value;
                }
            }

            // Extract category hints from path
            // Example: /clothing/shirts/product-123 -> category: "shirts"
            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length > 1)
            {
                // Skip common URL parts like 'products', 'p', 'item'
                var categories = pathParts.Where(p => 
                    !p.Equals("products", StringComparison.OrdinalIgnoreCase) &&
                    !p.Equals("p", StringComparison.OrdinalIgnoreCase) &&
                    !p.Equals("item", StringComparison.OrdinalIgnoreCase) &&
                    !p.Contains(".html", StringComparison.OrdinalIgnoreCase) &&
                    !Regex.IsMatch(p, @"^\d+$") // Skip numeric IDs
                ).ToList();

                if (categories.Any())
                {
                    info["category_hint"] = string.Join(" > ", categories);
                }
            }

            // Extract query parameters that might be useful
            var query = uri.Query;
            if (!string.IsNullOrEmpty(query))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(query);

                // Common parameters
                if (!string.IsNullOrEmpty(queryParams["color"]))
                    info["color_param"] = queryParams["color"];
                if (!string.IsNullOrEmpty(queryParams["size"]))
                    info["size_param"] = queryParams["size"];
                if (!string.IsNullOrEmpty(queryParams["variant"]))
                    info["variant_param"] = queryParams["variant"];
            }

            _logger.LogDebug("📍 Extracted URL info: {Info}", string.Join(", ", info.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Could not extract info from URL: {Url}", productUrl);
        }

        return info;
    }

    /// <summary>
    /// Flush accumulated products to database in a single batch operation
    /// </summary>
    private async Task FlushBatchAsync()
    {
        if (!_productBatch.Any())
            return;

        try
        {
            _logger.LogInformation("💾 Flushing batch of {Count} products to database...", _productBatch.Count);

            // Get existing products to determine which need updates vs inserts
            var articleNumbers = _productBatch.Select(p => p.ArticleNumber).Distinct().ToList();
            var existingProducts = await _context.Products
                .Include(p => p.Images)
                .Where(p => articleNumbers.Contains(p.ArticleNumber))
                .ToListAsync();

            var existingDict = existingProducts
                .ToDictionary(p => $"{p.ArticleNumber}_{p.ColorId ?? ""}");

            int updatedCount = 0;
            int insertedCount = 0;

            foreach (var product in _productBatch)
            {
                var key = $"{product.ArticleNumber}_{product.ColorId ?? ""}";

                if (existingDict.TryGetValue(key, out var existing))
                {
                    // Update existing product
                    existing.EAN = product.EAN;
                    existing.Price = product.Price;
                    existing.Description = product.Description;
                    existing.ProductUrl = product.ProductUrl;
                    existing.UpdatedAt = DateTime.UtcNow;

                    // Update legacy fields
#pragma warning disable CS0618
                    existing.ImageUrl = product.ImageUrl;
                    existing.ImageData = product.ImageData;
#pragma warning restore CS0618

                    // Replace images
                    _context.ProductImages.RemoveRange(existing.Images);
                    foreach (var image in product.Images)
                    {
                        image.ProductId = existing.Id;
                        existing.Images.Add(image);
                    }

                    updatedCount++;
                }
                else
                {
                    // Insert new product
                    _context.Products.Add(product);
                    insertedCount++;
                }
            }

            await _context.SaveChangesAsync();
            _productsSaved += _productBatch.Count;

            _logger.LogInformation("✅ Batch saved: {Inserted} new, {Updated} updated", insertedCount, updatedCount);

            _productBatch.Clear();
            _imageBatch.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error flushing product batch");
            // Don't clear batch on error - could retry
            throw;
        }
    }

    /// <summary>
    /// Add product to batch (optimized version without immediate save)
    /// </summary>
    private async Task AddProductToBatchAsync(
        string articleNumber, 
        string? ean, 
        string? colorId, 
        decimal? price, 
        string? description, 
        List<string> imageUrls, 
        string? productUrl)
    {
        try
        {
            var product = new Product
            {
                ArticleNumber = articleNumber,
                EAN = ean,
                ColorId = colorId,
                Price = price,
                Description = description,
                ProductUrl = productUrl,
                CreatedAt = DateTime.UtcNow
            };

            // Download and add images
            for (int i = 0; i < imageUrls.Count; i++)
            {
                var imageUrl = imageUrls[i];
                var imageData = await DownloadImage(imageUrl);

                var productImage = new ProductImage
                {
                    ImageUrl = imageUrl,
                    ImageData = imageData,
                    Order = i,
                    IsPrimary = i == 0,
                    CreatedAt = DateTime.UtcNow
                };

                product.Images.Add(productImage);
            }

            // Set legacy fields for backward compatibility
            if (imageUrls.Any())
            {
#pragma warning disable CS0618
                product.ImageUrl = imageUrls[0];
                product.ImageData = product.Images.FirstOrDefault()?.ImageData;
#pragma warning restore CS0618
            }

            _productBatch.Add(product);

            // Auto-flush when batch size reached
            if (_productBatch.Count >= BATCH_SIZE)
            {
                await FlushBatchAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error adding product to batch: {ArticleNumber}", articleNumber);
        }
    }
}

