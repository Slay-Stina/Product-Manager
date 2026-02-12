# JavaScript Rendering Solution for GANT Crawler

## Problem: Products Loaded via JavaScript

The GANT website loads product links **after page load** using JavaScript:
- Initial HTML has no product links
- JavaScript fetches products from API
- JavaScript injects links into DOM
- **Abot2/HttpClient never sees these links**

## Why Waiting Doesn't Help

```csharp
// ❌ THIS DOESN'T WORK
await Task.Delay(3000);  // JavaScript still not executed!
```

**Reason:** `HttpClient` doesn't have a browser engine - it just downloads HTML text.

---

## Solution: Add Playwright Support

### Step 1: Install Playwright

```bash
cd Product-Manager
dotnet add package Microsoft.Playwright
pwsh bin\Debug\net9.0\playwright.ps1 install chromium
```

### Step 2: Create PlaywrightCrawlerService

```csharp
using Microsoft.Playwright;

public class PlaywrightCrawlerService
{
    private readonly ILogger<PlaywrightCrawlerService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<List<string>> GetProductLinksAsync(string categoryUrl, string linkPattern)
    {
        var productLinks = new List<string>();
        
        try
        {
            // Initialize Playwright
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new()
            {
                Headless = true  // Run without UI
            });
            
            var page = await _browser.NewPageAsync();
            
            _logger.LogInformation("🌐 Loading page with JavaScript: {Url}", categoryUrl);
            await page.GotoAsync(categoryUrl);
            
            // Wait for products to load (adjust selector as needed)
            _logger.LogInformation("⏳ Waiting for products to load...");
            await page.WaitForSelectorAsync("a[href*='/p/']", new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000  // 10 seconds max
            });
            
            // Give JavaScript a bit more time to finish
            await Task.Delay(2000);
            
            // Extract all product links
            var links = await page.Locator("a[href*='/p/']").AllAsync();
            foreach (var link in links)
            {
                var href = await link.GetAttributeAsync("href");
                if (!string.IsNullOrWhiteSpace(href))
                {
                    productLinks.Add(href);
                }
            }
            
            _logger.LogInformation("✅ Found {Count} product links", productLinks.Count);
            
            return productLinks;
        }
        finally
        {
            if (_browser != null)
                await _browser.CloseAsync();
            
            _playwright?.Dispose();
        }
    }
}
```

### Step 3: Update ProductCrawlerService

```csharp
public class ProductCrawlerService
{
    private readonly PlaywrightCrawlerService _playwrightService;
    
    public async Task StartCrawlingAsync()
    {
        _logger.LogInformation("🎭 Using Playwright for JavaScript rendering");
        
        // Get product links using Playwright
        var productLinks = await _playwrightService.GetProductLinksAsync(
            _settings.TargetUrl,
            _currentBrandConfig.ProductUrlPattern
        );
        
        // Now crawl each product page
        foreach (var productUrl in productLinks)
        {
            await CrawlProductPage(productUrl);
            await Task.Delay(_settings.CrawlDelayMilliseconds);
        }
    }
}
```

---

## Alternative: Selenium

If you prefer Selenium:

```bash
dotnet add package Selenium.WebDriver
dotnet add package Selenium.WebDriver.ChromeDriver
```

```csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

public class SeleniumCrawlerService
{
    public List<string> GetProductLinks(string categoryUrl)
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        options.AddArgument("--disable-gpu");
        
        using var driver = new ChromeDriver(options);
        driver.Navigate().GoToUrl(categoryUrl);
        
        // Wait for products to load
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        wait.Until(d => d.FindElements(By.CssSelector("a[href*='/p/']")).Count > 0);
        
        // Give extra time for JavaScript
        Thread.Sleep(2000);
        
        // Extract links
        var links = driver.FindElements(By.CssSelector("a[href*='/p/']"));
        return links.Select(l => l.GetAttribute("href")).ToList();
    }
}
```

---

## Configuration Update

Add to `BrandConfig`:

```json
{
  "UseJavaScriptRendering": true,
  "JavaScriptWaitSelector": "a[href*='/p/']",
  "JavaScriptWaitTimeoutMs": 10000,
  "PostRenderDelayMs": 2000
}
```

---

## Performance Considerations

### Abot2 (Current)
- ⚡ **Fast:** 50-100 pages/minute
- 💾 **Low memory:** ~50MB
- ❌ **No JavaScript**

### Playwright/Selenium
- 🐢 **Slower:** 10-20 pages/minute
- 💾 **High memory:** ~500MB per browser
- ✅ **Full JavaScript support**

### Hybrid Approach (Recommended)

```csharp
// Use Playwright for category pages (get product links)
var productLinks = await _playwright.GetProductLinksAsync(categoryUrl);

// Use Abot2/HttpClient for product detail pages (faster)
foreach (var link in productLinks)
{
    await CrawlProductPageWithHttpClient(link);  // Fast!
}
```

**Best of both worlds:**
- Playwright: 1 category page (1-2 seconds)
- HttpClient: 20 product pages (10 seconds)
- **Total: 12 seconds** instead of 40 seconds with full Playwright

---

## Checking If JavaScript is Required

### Test with curl
```bash
curl https://www.gant.se/c/herr/accessoarer/vaskor > page.html
# Open page.html in text editor
# Search for "/p/"
# If not found → JavaScript required ✅
```

### Test in Browser
1. Open DevTools (F12)
2. Go to Network tab
3. Disable JavaScript
4. Reload page
5. No products? → JavaScript required ✅

---

## Summary

| Solution | Speed | Memory | JavaScript | Complexity |
|----------|-------|--------|------------|------------|
| **Current (Abot2)** | ⚡⚡⚡ | ✅ Low | ❌ No | 🟢 Simple |
| **Playwright** | 🐢 Slow | ❌ High | ✅ Yes | 🟡 Medium |
| **Selenium** | 🐢 Slow | ❌ High | ✅ Yes | 🟡 Medium |
| **Hybrid** | ⚡⚡ Fast | ✅ Medium | ✅ Yes | 🟠 Complex |

### Recommendation for GANT:
Use **Hybrid Approach**:
1. Playwright for 1 category page → Get product URLs
2. HttpClient for product pages → Fast extraction

This gives you JavaScript support where needed (category page) while keeping product page crawling fast!

---

## Next Steps

1. ✅ Verify JavaScript is the issue (check with DevTools)
2. ⬜ Choose: Playwright or Selenium
3. ⬜ Install packages
4. ⬜ Implement JavaScript rendering service
5. ⬜ Test with GANT category page
6. ⬜ Compare product link count
