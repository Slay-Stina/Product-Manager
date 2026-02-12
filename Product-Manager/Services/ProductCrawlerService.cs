using System.Net;
using System.Text;
using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Product_Manager.Models;

namespace Product_Manager.Services;

public partial class ProductCrawlerService
{
    private readonly CrawlerSettings _settings;
    private readonly ILogger<ProductCrawlerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly PlaywrightCrawlerService _playwrightService;
    private readonly AuthenticationService _authService;
    private readonly ProductParserService _parserService;
    private readonly ProductSaverService _saverService;
    private readonly ImageDownloaderService _imageDownloader;

    // Current brand configuration with selectors
    private BrandConfig? _currentBrandConfig;

    // Crawl statistics
    private int _categoryPagesProcessed = 0;
    private int _productPagesProcessed = 0;
    private readonly HashSet<string> _productLinks = new();

    public ProductCrawlerService(
        CrawlerSettings settings,
        ILogger<ProductCrawlerService> logger,
        IHttpClientFactory httpClientFactory,
        PlaywrightCrawlerService playwrightService,
        AuthenticationService authService,
        ProductParserService parserService,
        ProductSaverService saverService,
        ImageDownloaderService imageDownloader)
    {
        _settings = settings;
        _logger = logger;
        _playwrightService = playwrightService;
        _authService = authService;
        _parserService = parserService;
        _saverService = saverService;
        _imageDownloader = imageDownloader;

        // Configure HttpClient with UTF-8 encoding support
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.AcceptCharset.Clear();
        _httpClient.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));

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
        return await _authService.AuthenticateAsync();
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
            await _saverService.FlushBatchAsync();

            // Final statistics
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("✅ Crawl completed successfully!");
            _logger.LogInformation("📊 Statistics:");
            _logger.LogInformation("   🔗 Product links found: {Links}", productLinks.Count);
            _logger.LogInformation("   🎯 Product pages processed: {Pages}", _productPagesProcessed);
            _logger.LogInformation("   💾 Products saved: {Saved}", _saverService.ProductsSaved);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Playwright crawling");
            // Try to flush batch even on error
            try
            {
                await _saverService.FlushBatchAsync();
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
        await _saverService.FlushBatchAsync();

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
            _logger.LogInformation("   💾 Products saved: {ProductsSaved}", _saverService.ProductsSaved);
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
                    if (!string.IsNullOrWhiteSpace(description))
                    {
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

        return _parserService.ExtractImageUrl(imgElement);
    }

    private async Task SaveProduct(string articleNumber, string? colorId, string? description, string? imageUrl, string? productUrl = null)
    {
        await _saverService.SaveProductAsync(articleNumber, colorId, description, imageUrl, productUrl);
    }

    private async Task SaveProductWithDetails(string articleNumber, string? ean, string? colorId, decimal? price, string? description, List<string> imageUrls, string? productUrl = null)
    {
        var parsedProduct = new ParsedProduct
        {
            ArticleNumber = articleNumber,
            EAN = ean,
            ColorId = colorId,
            Price = price,
            Description = description,
            ImageUrls = imageUrls,
            ProductUrl = productUrl
        };

        await _saverService.AddProductToBatchAsync(parsedProduct);
    }

    /// <summary>
    /// Extract product URLs from JSON-LD structured data
    /// </summary>
    private List<string> ExtractProductUrlsFromJsonLd(IHtmlDocument document)
    {
        return _parserService.ExtractProductUrlsFromJsonLd(document);
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

            // Use parser service to extract product data
            var parsedProduct = await _parserService.ParseProductPageAsync(document, productUrl, _currentBrandConfig);

            if (parsedProduct == null || string.IsNullOrWhiteSpace(parsedProduct.ArticleNumber))
            {
                _logger.LogError("❌ FAILED: Could not extract product data from page");
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                return;
            }

            // NEW APPROACH: Instead of using parsed image URLs that might be CDN-restricted,
            // find and validate all image URLs on the page containing the article number
            _logger.LogInformation("🔍 Discovering valid image URLs from page...");
            var validImageUrls = await _imageDownloader.FindAndValidateImageUrlsAsync(
                document, 
                parsedProduct.ArticleNumber, 
                _currentBrandConfig.BrandName);

            // Replace parsed image URLs with validated ones
            if (validImageUrls.Any())
            {
                parsedProduct.ImageUrls = validImageUrls;
                _logger.LogInformation("✅ Using {Count} validated image URLs", validImageUrls.Count);
            }
            else
            {
                _logger.LogWarning("⚠️ No valid image URLs found, keeping original parsed URLs ({Count})", 
                    parsedProduct.ImageUrls.Count);
            }

            // Log extracted data
            _logger.LogInformation("💾 Adding product to batch:");
            _logger.LogInformation("   📦 Article Number: {ArticleNumber}", parsedProduct.ArticleNumber);
            _logger.LogInformation("   🏷️  EAN: {EAN}", parsedProduct.EAN ?? "N/A");
            _logger.LogInformation("   📝 Name: {Name}", parsedProduct.ProductName ?? "N/A");
            _logger.LogInformation("   💰 Price: {Price}", parsedProduct.Price?.ToString("C") ?? "N/A");
            _logger.LogInformation("   🎨 Color: {Color}", parsedProduct.ColorId ?? "N/A");
            _logger.LogInformation("   🖼️  Images: {Count}", parsedProduct.ImageUrls.Count);
            _logger.LogInformation("   🔗 URL: {Url}", parsedProduct.ProductUrl);

            // Save using saver service
            if (parsedProduct.ImageUrls.Any())
            {
                await _saverService.AddProductToBatchAsync(parsedProduct);
            }
            else
            {
                // For products without images, use immediate save
                await SaveProduct(
                    parsedProduct.ArticleNumber,
                    parsedProduct.ColorId,
                    parsedProduct.GetFullDescription(),
                    null,
                    parsedProduct.ProductUrl);
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
}

