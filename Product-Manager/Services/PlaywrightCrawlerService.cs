using Microsoft.Playwright;
using Product_Manager.Models;

namespace Product_Manager.Services;

/// <summary>
/// Service for crawling JavaScript-rendered pages using Playwright
/// </summary>
public class PlaywrightCrawlerService : IAsyncDisposable
{
    private readonly ILogger<PlaywrightCrawlerService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _isInitialized = false;

    public PlaywrightCrawlerService(ILogger<PlaywrightCrawlerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize Playwright and launch browser
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("🎭 Initializing Playwright...");
            
            _playwright = await Playwright.CreateAsync();
            
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,  // Run without UI
                Args = new[]
                {
                    "--disable-gpu",
                    "--disable-dev-shm-usage",
                    "--disable-setuid-sandbox",
                    "--no-sandbox"
                }
            });

            _isInitialized = true;
            _logger.LogInformation("✅ Playwright initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Playwright");
            throw;
        }
    }

    /// <summary>
    /// Extract product links from a category page that uses JavaScript
    /// </summary>
    public async Task<List<string>> GetProductLinksAsync(string categoryUrl, BrandConfig brandConfig)
    {
        var productLinks = new List<string>();
        
        try
        {
            await InitializeAsync();
            
            if (_browser == null)
            {
                _logger.LogError("❌ Browser not initialized");
                return productLinks;
            }

            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("🎭 PLAYWRIGHT: Loading JavaScript-rendered page");
            _logger.LogInformation("🔗 URL: {Url}", categoryUrl);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var page = await _browser.NewPageAsync();
            
            try
            {
                // Navigate to the page (DON'T wait for networkidle - it never happens with analytics!)
                _logger.LogInformation("⏳ Step 1: Loading page...");
                var response = await page.GotoAsync(categoryUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,  // Just wait for DOM load, not network idle
                    Timeout = 30000  // 30 seconds timeout
                });

                if (response?.Status != 200)
                {
                    _logger.LogWarning("⚠️ Page returned status: {Status}", response?.Status);
                }

                // Wait for product links to appear - try multiple selectors
                _logger.LogInformation("⏳ Step 2: Waiting for products to load...");

                // Try different possible selectors for GANT products
                var possibleSelectors = new[]
                {
                    $"a[href*='{brandConfig.ProductUrlPattern}']",
                    ".product-tile__link",
                    ".product-grid__item a",
                    "[data-pid]",
                    ".product-tile"
                };

                var selector = "";
                var found = false;

                foreach (var testSelector in possibleSelectors)
                {
                    try
                    {
                        _logger.LogInformation("🔍 Trying selector: {Selector}", testSelector);
                        await page.WaitForSelectorAsync(testSelector, new PageWaitForSelectorOptions
                        {
                            State = WaitForSelectorState.Attached,
                            Timeout = 10000  // 10 seconds per selector
                        });
                        selector = testSelector;
                        found = true;
                        _logger.LogInformation("✅ Product elements found with selector: {Selector}", selector);
                        break;
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogInformation("⏭️ Selector not found: {Selector}", testSelector);
                        continue;
                    }
                }

                if (!found)
                {
                    _logger.LogWarning("⚠️ No product selectors found, trying to extract links anyway...");
                    selector = $"a[href*='{brandConfig.ProductUrlPattern}']";
                }

                // Give JavaScript extra time to finish rendering
                _logger.LogInformation("⏳ Step 3: Waiting for JavaScript to finish (3 seconds)...");
                await Task.Delay(3000);

                // Extract all product links
                _logger.LogInformation("🔍 Step 4: Extracting product links...");
                var links = await page.Locator(selector).AllAsync();
                
                foreach (var link in links)
                {
                    var href = await link.GetAttributeAsync("href");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        // Make URL absolute if it's relative
                        if (!href.StartsWith("http"))
                        {
                            var baseUri = new Uri(categoryUrl);
                            href = new Uri(baseUri, href).ToString();
                        }
                        
                        if (!productLinks.Contains(href))
                        {
                            productLinks.Add(href);
                        }
                    }
                }

                _logger.LogInformation("✅ Found {Count} unique product links", productLinks.Count);
                
                // Log first few links for verification
                if (productLinks.Any())
                {
                    _logger.LogInformation("📝 Sample product links:");
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
                    _logger.LogWarning("⚠️ No product links found!");
                    
                    // Debug: Try to get page content
                    var html = await page.ContentAsync();
                    var hasPattern = html.Contains(brandConfig.ProductUrlPattern);
                    _logger.LogInformation("💡 Pattern '{Pattern}' in page HTML: {HasPattern}", 
                        brandConfig.ProductUrlPattern, hasPattern ? "YES" : "NO");
                }

                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            }
            finally
            {
                await page.CloseAsync();
            }
            
            return productLinks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error extracting product links with Playwright");
            return productLinks;
        }
    }

    /// <summary>
    /// Dispose of Playwright resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
        }
        
        _playwright?.Dispose();
        
        _isInitialized = false;
        _logger.LogInformation("🎭 Playwright disposed");
    }
}
