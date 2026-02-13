using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Product_Manager.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Product_Manager.Services;

/// <summary>
/// Service responsible for parsing product data from HTML and JSON-LD
/// </summary>
public class ProductParserService
{
    private readonly ILogger<ProductParserService> _logger;

    public ProductParserService(ILogger<ProductParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse product data from a product detail page
    /// </summary>
    public Task<ParsedProduct?> ParseProductPageAsync(
        IHtmlDocument document, 
        string productUrl, 
        BrandConfig brandConfig)
    {
        try
        {
            _logger.LogInformation("🎯 Parsing product page: {Url}", productUrl);

            // Skip gift cards - we don't want to extract these
            if (productUrl.Contains("gift-card", StringComparison.OrdinalIgnoreCase) ||
                productUrl.Contains("giftcard", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("⏭️  Skipping gift card product");
                return Task.FromResult<ParsedProduct?>(null);
            }

            var product = new ParsedProduct { ProductUrl = productUrl };

            // Step 1: Try JSON-LD first if enabled (fastest and most reliable)
            if (brandConfig.UseJsonLdExtraction)
            {
                var jsonLdData = ExtractFromJsonLd(document, brandConfig);
                if (jsonLdData != null)
                {
                    _logger.LogInformation("✅ Found product data in JSON-LD");
                    product.Merge(jsonLdData);
                }
            }

            // Step 2: Fill missing data from HTML selectors
            var htmlData = ExtractFromHtml(document, brandConfig);
            product.Merge(htmlData);

            // Step 3: Extract from URL if article number still missing
            if (string.IsNullOrWhiteSpace(product.ArticleNumber))
            {
                product.ArticleNumber = ExtractArticleNumberFromUrl(productUrl, brandConfig);
            }

            if (string.IsNullOrWhiteSpace(product.ArticleNumber))
            {
                _logger.LogWarning("⚠️ Could not extract article number from product page");
                return Task.FromResult<ParsedProduct?>(null);
            }

            _logger.LogInformation("✅ Parsed product: {ArticleNumber}", product.ArticleNumber);
            return Task.FromResult<ParsedProduct?>(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error parsing product page {Url}", productUrl);
            return Task.FromResult<ParsedProduct?>(null);
        }
    }

    /// <summary>
    /// Extract product data from JSON-LD structured data
    /// </summary>
    private ParsedProduct? ExtractFromJsonLd(IHtmlDocument document, BrandConfig brandConfig)
    {
        var jsonLdScripts = document.QuerySelectorAll("script[type='application/ld+json']");

        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation("🔍 Found {Count} JSON-LD script(s) on page", jsonLdScripts.Length);
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        int scriptIndex = 0;
        ParsedProduct? productResult = null;

        foreach (var script in jsonLdScripts)
        {
            scriptIndex++;
            try
            {
                var jsonContent = script.TextContent;
                if (string.IsNullOrWhiteSpace(jsonContent))
                    continue;

                using var jsonDoc = JsonDocument.Parse(jsonContent);
                var root = jsonDoc.RootElement;

                // Log ALL JSON-LD structures found
                _logger.LogInformation("");
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _logger.LogInformation("📋 JSON-LD SCRIPT #{Index}", scriptIndex);

                // Get the @type to show what kind of data this is
                var typeName = "Unknown";
                if (root.TryGetProperty("@type", out var typeProperty))
                {
                    typeName = typeProperty.GetString() ?? "Unknown";
                }
                _logger.LogInformation("📌 Type: {Type}", typeName);
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                // Pretty-print the entire JSON structure
                var prettyJson = JsonSerializer.Serialize(root, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                _logger.LogInformation(prettyJson);

                // List all available properties
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _logger.LogInformation("📝 AVAILABLE PROPERTIES:");
                foreach (var property in root.EnumerateObject())
                {
                    _logger.LogInformation("   • {Name} ({Type})", property.Name, property.Value.ValueKind);
                }
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                // Only process Product types for extraction
                if (typeName == "Product" && productResult == null)
                {
                    _logger.LogInformation("✅ Processing this as PRODUCT data...");

                    var product = new ParsedProduct();

                    // Name
                    if (root.TryGetProperty("name", out var nameProperty))
                        product.ProductName = nameProperty.GetString();

                    // Description
                    if (root.TryGetProperty("description", out var descProperty))
                        product.Description = descProperty.GetString();

                    // Color (can be null for items without color like gift cards)
                    if (root.TryGetProperty("color", out var colorProperty) && 
                        colorProperty.ValueKind != JsonValueKind.Null)
                    {
                        product.ColorId = colorProperty.GetString();
                    }

                    // Images
                    product.ImageUrls = ExtractImagesFromJsonLd(root);

                    // EAN/GTIN - use configured field priority
                    product.EAN = ExtractEanFromJsonLd(root, brandConfig);

                    // Article Number - use configured source
                    product.ArticleNumber = ExtractArticleNumberFromJsonLd(root, brandConfig);

                    // Material (if enabled in config)
                    if (brandConfig.ExtractMaterialFromJsonLd && 
                        root.TryGetProperty("material", out var materialProperty))
                    {
                        product.Material = materialProperty.GetString();
                    }

                    // Category (if enabled in config)
                    if (brandConfig.ExtractCategoryFromJsonLd && 
                        root.TryGetProperty("category", out var categoryProperty))
                    {
                        product.Category = categoryProperty.GetString();
                    }

                    // Price
                    if (root.TryGetProperty("offers", out var offers))
                    {
                        product.Price = ExtractPriceFromJsonLd(offers);
                    }

                    productResult = product;
                }
                else
                {
                    _logger.LogInformation("ℹ️  Skipping extraction (not a Product type or already found)");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("⚠️ Failed to parse JSON-LD script #{Index}: {Message}", scriptIndex, ex.Message);
            }
        }

        _logger.LogInformation("");
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation("✅ Finished processing all JSON-LD scripts");
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        return productResult;
    }

    /// <summary>
    /// Extract images from JSON-LD
    /// </summary>
    private List<string> ExtractImagesFromJsonLd(JsonElement root)
    {
        var images = new List<string>();

        if (!root.TryGetProperty("image", out var imageProperty))
            return images;

        if (imageProperty.ValueKind == JsonValueKind.String)
        {
            var url = imageProperty.GetString();
            if (!string.IsNullOrWhiteSpace(url))
                images.Add(url);
        }
        else if (imageProperty.ValueKind == JsonValueKind.Array)
        {
            var urls = imageProperty.EnumerateArray()
                .Select(imgElement => imgElement.ValueKind == JsonValueKind.String 
                    ? imgElement.GetString() 
                    : (imgElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null))
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .OfType<string>();

            images.AddRange(urls);
        }
        else if (imageProperty.ValueKind == JsonValueKind.Object)
        {
            if (imageProperty.TryGetProperty("@id", out var idProp))
            {
                var url = idProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    images.Add(url);
            }
            else if (imageProperty.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    images.Add(url);
            }
        }

        return images;
    }

    /// <summary>
    /// Extract EAN from JSON-LD using brand-configured field priority
    /// </summary>
    private string? ExtractEanFromJsonLd(JsonElement root, BrandConfig brandConfig)
    {
        _logger.LogInformation("🔍 Extracting EAN from JSON-LD...");

        // Try fields in the order specified by brand config
        foreach (var fieldName in brandConfig.EanJsonLdFields)
        {
            _logger.LogInformation("   Checking field: {Field}", fieldName);

            if (root.TryGetProperty(fieldName, out var property))
            {
                _logger.LogInformation("   Found field '{Field}' with type: {Type}", fieldName, property.ValueKind);

                if (property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _logger.LogInformation("✓ Found EAN in JSON-LD field '{Field}': {Value}", fieldName, value);
                        return value;
                    }
                }
                else if (property.ValueKind == JsonValueKind.Number)
                {
                    var value = property.GetInt64().ToString();
                    _logger.LogInformation("✓ Found EAN (as number) in JSON-LD field '{Field}': {Value}", fieldName, value);
                    return value;
                }
            }
            else
            {
                _logger.LogInformation("   Field '{Field}' not found", fieldName);
            }
        }

        _logger.LogWarning("⚠️ No EAN found in any configured fields");
        return null;
    }

    /// <summary>
    /// Extract article number from JSON-LD using brand-configured source
    /// </summary>
    private string? ExtractArticleNumberFromJsonLd(JsonElement root, BrandConfig brandConfig)
    {
        if (brandConfig.ArticleNumberSource == "jsonld-field")
        {
            // Extract from a specific JSON-LD field
            if (root.TryGetProperty(brandConfig.ArticleNumberJsonLdField, out var fieldProperty))
            {
                var fieldValue = fieldProperty.GetString();
                if (!string.IsNullOrWhiteSpace(fieldValue))
                {
                    // Apply pattern if specified
                    if (!string.IsNullOrWhiteSpace(brandConfig.ArticleNumberUrlPattern))
                    {
                        var match = Regex.Match(fieldValue, brandConfig.ArticleNumberUrlPattern);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            _logger.LogDebug("✓ Extracted article number from JSON-LD field '{Field}': {Number}", 
                                brandConfig.ArticleNumberJsonLdField, match.Groups[1].Value);
                            return match.Groups[1].Value;
                        }
                    }

                    _logger.LogDebug("✓ Using article number from JSON-LD field '{Field}': {Number}", 
                        brandConfig.ArticleNumberJsonLdField, fieldValue);
                    return fieldValue;
                }
            }
        }
        else if (brandConfig.ArticleNumberSource == "url")
        {
            // Extract from @id URL field (default behavior)
            if (root.TryGetProperty("@id", out var idProperty))
            {
                var idUrl = idProperty.GetString();
                if (!string.IsNullOrWhiteSpace(idUrl))
                {
                    var match = Regex.Match(idUrl, brandConfig.ArticleNumberUrlPattern);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        _logger.LogDebug("✓ Extracted article number from @id URL: {Number}", match.Groups[1].Value);
                        return match.Groups[1].Value;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extract price from JSON-LD offers
    /// </summary>
    private decimal? ExtractPriceFromJsonLd(JsonElement offers)
    {
        // Check if offers is null or not an object
        if (offers.ValueKind == JsonValueKind.Null || offers.ValueKind == JsonValueKind.Undefined)
        {
            _logger.LogDebug("⚠️ Offers element is null or undefined");
            return null;
        }

        if (!offers.TryGetProperty("price", out var priceProperty))
            return null;

        if (priceProperty.ValueKind == JsonValueKind.String)
        {
            var priceStr = priceProperty.GetString();
            if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var priceValue))
                return priceValue;
        }
        else if (priceProperty.ValueKind == JsonValueKind.Number)
        {
            return priceProperty.GetDecimal();
        }

        return null;
    }

    /// <summary>
    /// Extract product data from HTML using brand-specific selectors
    /// </summary>
    private ParsedProduct ExtractFromHtml(IHtmlDocument document, BrandConfig brandConfig)
    {
        var product = new ParsedProduct();

        if (!string.IsNullOrWhiteSpace(brandConfig.ProductPageNameSelector))
            product.ProductName = ExtractText(document.DocumentElement, brandConfig.ProductPageNameSelector);

        if (!string.IsNullOrWhiteSpace(brandConfig.ProductPagePriceSelector))
        {
            var priceString = ExtractText(document.DocumentElement, brandConfig.ProductPagePriceSelector);
            product.Price = ParsePrice(priceString);
        }

        if (!string.IsNullOrWhiteSpace(brandConfig.ProductPageDescriptionSelector))
            product.Description = ExtractText(document.DocumentElement, brandConfig.ProductPageDescriptionSelector);

        if (!string.IsNullOrWhiteSpace(brandConfig.ProductPageColorSelector))
            product.ColorId = ExtractText(document.DocumentElement, brandConfig.ProductPageColorSelector);

        if (!string.IsNullOrWhiteSpace(brandConfig.ProductPageImageSelector))
            product.ImageUrls = ExtractImagesFromHtml(document, brandConfig.ProductPageImageSelector);

        return product;
    }

    /// <summary>
    /// Extract text content from element using CSS selector
    /// </summary>
    private string? ExtractText(IElement element, string selector)
    {
        var targetElement = element.QuerySelector(selector);
        return targetElement?.TextContent?.Trim();
    }

    /// <summary>
    /// Extract images from HTML
    /// </summary>
    private List<string> ExtractImagesFromHtml(IHtmlDocument document, string selector)
    {
        var imageElements = document.DocumentElement.QuerySelectorAll(selector);
        
        return imageElements
            .Select(imgElement => ExtractImageUrl(imgElement))
            .Where(imageUrl => !string.IsNullOrWhiteSpace(imageUrl))
            .OfType<string>()
            .ToList();
    }

    /// <summary>
    /// Extract image URL from element (handles multiple attributes and picture elements)
    /// </summary>
    public string? ExtractImageUrl(IElement element)
    {
        // Check if inside a <picture> element with <source> tags
        var pictureParent = element.ParentElement;
        if (pictureParent?.TagName?.Equals("PICTURE", StringComparison.OrdinalIgnoreCase) == true)
        {
            var sourceElements = pictureParent.QuerySelectorAll("source");
            foreach (var source in sourceElements)
            {
                var srcset = source.GetAttribute("data-srcset") ?? source.GetAttribute("srcset");
                if (!string.IsNullOrEmpty(srcset))
                {
                    var firstUrl = srcset.Split(',')[0].Trim().Split(' ')[0];
                    return firstUrl;
                }
            }
        }

        // Try multiple common image attributes
        var imageUrl = element.GetAttribute("src") 
                    ?? element.GetAttribute("data-src") 
                    ?? element.GetAttribute("data-original")
                    ?? element.GetAttribute("data-lazy-src");

        // Try srcset as fallback
        if (string.IsNullOrEmpty(imageUrl))
        {
            var srcset = element.GetAttribute("srcset") ?? element.GetAttribute("data-srcset");
            if (!string.IsNullOrEmpty(srcset))
            {
                var firstUrl = srcset.Split(',')[0].Trim().Split(' ')[0];
                imageUrl = firstUrl;
            }
        }

        return imageUrl;
    }

    /// <summary>
    /// Parse price string to decimal
    /// </summary>
    private decimal? ParsePrice(string? priceString)
    {
        if (string.IsNullOrWhiteSpace(priceString))
            return null;

        // Remove currency symbols and whitespace
        var cleanPrice = Regex.Replace(priceString, @"[^\d.,]", "").Trim();
        
        if (string.IsNullOrEmpty(cleanPrice))
            return null;

        // Normalize decimal/thousands separators so that invariant parsing works for both
        // "1,234.56" and "1.234,56" (common EU style) formats.
        var lastComma = cleanPrice.LastIndexOf(',');
        var lastDot = cleanPrice.LastIndexOf('.');
        string normalizedPrice;

        if (lastComma >= 0 && lastDot >= 0)
        {
            // Use ternary operator to simplify branch logic
            normalizedPrice = lastComma > lastDot
                ? cleanPrice.Replace(".", string.Empty).Replace(',', '.') // Comma is decimal: "1.234,56"
                : cleanPrice.Replace(",", string.Empty); // Dot is decimal: "1,234.56"
        }
        else if (lastComma >= 0)
        {
            // Only comma present. Decide whether it's a decimal or thousands separator.
            var digitsAfterComma = cleanPrice.Length - lastComma - 1;

            // Use ternary: if 3 digits after comma and comma not at start, it's likely thousands separator
            normalizedPrice = (digitsAfterComma == 3 && lastComma > 0)
                ? cleanPrice.Replace(",", string.Empty) // Thousands separator: "1,234" -> "1234"
                : cleanPrice.Replace(',', '.'); // Decimal separator: "199,99" -> "199.99"
        }
        else
        {
            // Only dot present or no separator; keep as-is.
            normalizedPrice = cleanPrice;
        }

        if (decimal.TryParse(normalizedPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var priceValue))
            return priceValue;

        return null;
    }

    /// <summary>
    /// Extract article number from URL using regex patterns
    /// </summary>
    private string? ExtractArticleNumberFromUrl(string productUrl, BrandConfig brandConfig)
    {
        // Try common patterns
        var patterns = new[]
        {
            @"/(\d{7,})\.html",
            @"/(\d{7,})(?:[?#]|$)",
            @"(\d{7,})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(productUrl, pattern);
            if (match.Success)
            {
                _logger.LogDebug("✓ Extracted article number from URL: {Number}", match.Groups[1].Value);
                return match.Groups[1].Value;
            }
        }

        // Try brand-specific selector
        if (!string.IsNullOrWhiteSpace(brandConfig.ProductSkuSelector))
        {
            // Note: This requires the document which we don't have here
            // We'll need to pass it in or handle this differently
            _logger.LogWarning("⚠️ Cannot use ProductSkuSelector without document");
        }

        return null;
    }

    /// <summary>
    /// Extract product URLs from JSON-LD on category pages
    /// </summary>
    public List<string> ExtractProductUrlsFromJsonLd(IHtmlDocument document)
    {
        var productUrls = new List<string>();
        
        try
        {
            var jsonLdScripts = document.QuerySelectorAll("script[type='application/ld+json']");
            
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
                        typeProperty.GetString() == "Product" &&
                        root.TryGetProperty("offers", out var offers) &&
                        offers.TryGetProperty("url", out var urlProperty))
                    {
                        var url = urlProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            productUrls.Add(url);
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
}

/// <summary>
/// Represents parsed product data from a page
/// </summary>
public class ParsedProduct
{
    public string? ArticleNumber { get; set; }
    public string? EAN { get; set; }
    public string? ColorId { get; set; }
    public string? ProductName { get; set; }
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public string? Material { get; set; }
    public string? Category { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? ProductUrl { get; set; }

    /// <summary>
    /// Merge data from another ParsedProduct (only fills missing values)
    /// </summary>
    public void Merge(ParsedProduct? other)
    {
        if (other == null) return;

        ArticleNumber ??= other.ArticleNumber;
        EAN ??= other.EAN;
        ColorId ??= other.ColorId;
        ProductName ??= other.ProductName;
        Price ??= other.Price;
        Description ??= other.Description;
        Material ??= other.Material;
        Category ??= other.Category;
        ProductUrl ??= other.ProductUrl;

        if (!ImageUrls.Any() && other.ImageUrls.Any())
            ImageUrls = other.ImageUrls;
    }

    /// <summary>
    /// Build full description combining description and material
    /// </summary>
    public string GetFullDescription()
    {
        var result = Description ?? string.Empty;

        // Add material with 2 new lines below if available
        if (!string.IsNullOrWhiteSpace(Material))
        {
            result += $"\n\n{Material}";
        }

        return result;
    }
}
