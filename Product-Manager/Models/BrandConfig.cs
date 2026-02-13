namespace Product_Manager.Models;

public class BrandConfig
{
    public string BrandName { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string UsernameFieldName { get; set; } = "username";
    public string PasswordFieldName { get; set; } = "password";
    public int MaxPagesToCrawl { get; set; } = 100;
    public int CrawlDelayMilliseconds { get; set; } = 1000;
    public string ImageDownloadPath { get; set; } = "wwwroot/images/products";
    
    // Brand-specific selectors
    public string ProductContainerSelector { get; set; } = string.Empty;
    public string ProductLinkSelector { get; set; } = string.Empty;  // Selector for the link to product detail page
    public string ProductNameSelector { get; set; } = string.Empty;
    public string ProductPriceSelector { get; set; } = string.Empty;
    public string ProductImageSelector { get; set; } = string.Empty;
    public string ProductDescriptionSelector { get; set; } = string.Empty;
    public string ProductSkuSelector { get; set; } = string.Empty;

    // JSON-LD and product page crawling
    public bool UseJsonLdExtraction { get; set; } = false;
    public bool CrawlProductPages { get; set; } = false;
    public string ProductUrlPattern { get; set; } = string.Empty;  // URL pattern to identify product pages (e.g., "/p/" for GANT)
    public string ProductPageNameSelector { get; set; } = string.Empty;
    public string ProductPagePriceSelector { get; set; } = string.Empty;
    public string ProductPageDescriptionSelector { get; set; } = string.Empty;
    public string ProductPageImageSelector { get; set; } = string.Empty;
    public string ProductPageColorSelector { get; set; } = string.Empty;

    // Brand-specific JSON-LD parsing configuration
    public string ArticleNumberSource { get; set; } = "url";  // Where to extract article number from: "url", "jsonld-field", "html-selector"
    public string ArticleNumberUrlPattern { get; set; } = @"/([^/]+)$";  // Regex pattern to extract article number from URL (e.g., last segment)
    public string ArticleNumberJsonLdField { get; set; } = "@id";  // JSON-LD field to extract article number from (when source is "jsonld-field")
    public List<string> EanJsonLdFields { get; set; } = new() { "productID", "mpn", "gtin13", "gtin", "sku" };  // Priority order of fields to check for EAN in JSON-LD
    public bool ExtractMaterialFromJsonLd { get; set; } = true;  // Extract material field from JSON-LD
    public bool ExtractCategoryFromJsonLd { get; set; } = true;  // Extract category field from JSON-LD

    // Playwright JavaScript rendering
    public bool UseJavaScriptRendering { get; set; } = false;  // Enable Playwright for JavaScript-rendered pages
    public int JavaScriptWaitTimeoutMs { get; set; } = 15000;  // How long to wait for elements (ms)
    public int PostRenderDelayMs { get; set; } = 2000;  // Extra time after elements appear (ms)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsed { get; set; }
}
