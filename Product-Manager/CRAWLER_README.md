# Product Crawler Setup Guide

This application includes a web crawler using the Abot library to scrape product information from authenticated websites.

## Features

- Authenticate to protected image bank/product websites
- Extract product information including:
  - Article Number (Product ID)
  - Color ID
  - Product Description
  - Product Images
- Save products to database
- Display products in a Blazor FluentUI DataGrid

## Setup Instructions

### 1. Configure the Crawler Settings

Edit `appsettings.json` and update the `CrawlerSettings` section:

```json
"CrawlerSettings": {
  "TargetUrl": "https://your-image-bank.com/products",
  "LoginUrl": "https://your-image-bank.com/login",
  "Username": "your-username",
  "Password": "your-password",
  "UsernameFieldName": "username",
  "PasswordFieldName": "password",
  "MaxPagesToCrawl": 100,
  "CrawlDelayMilliseconds": 1000,
  "ImageDownloadPath": "wwwroot/images/products"
}
```

**Important:** Replace these values with your actual website URLs and credentials.

### 2. Customize the HTML Selectors

The crawler needs to know how to extract product information from the target website. Open `Services\ProductCrawlerService.cs` and modify the `ParseAndSaveProducts` method:

```csharp
private void ParseAndSaveProducts(IHtmlDocument document, string pageUrl)
{
    // Update these CSS selectors to match your target website's HTML structure
    var productElements = document.QuerySelectorAll(".product-item, .product, [data-product]");
    
    foreach (var productElement in productElements)
    {
        // Customize these selectors:
        var articleNumber = ExtractText(productElement, ".product-id, .article-number, [data-article-id]");
        var colorId = ExtractText(productElement, ".color-id, .product-color, [data-color]");
        var description = ExtractText(productElement, ".product-description, .description, p");
        var imageUrl = ExtractImageUrl(productElement, "img");
        
        // ...
    }
}
```

**To find the right selectors:**
1. Open the target website in a browser
2. Right-click on a product element and select "Inspect"
3. Find the CSS classes or attributes that identify products
4. Update the selectors in the code above

### 3. Update the Database

Run the database migration to create the Products table:

```bash
dotnet ef database update --project Product-Manager\Product-Manager.csproj
```

### 4. Run the Application

```bash
dotnet run --project Product-Manager\Product-Manager.csproj
```

### 5. Use the Crawler

1. Navigate to the `/products` page
2. Click the "Start Crawler" button
3. Wait for the crawler to complete
4. Products will be displayed in the grid

## Customization Tips

### Authentication

If your target website uses a different authentication method (e.g., OAuth, bearer tokens), modify the `AuthenticateAsync` method in `ProductCrawlerService.cs`.

### Complex HTML Structures

If products are loaded via JavaScript or AJAX:
- Consider using a headless browser like Selenium or Playwright
- Or use the website's API if available

### Rate Limiting

Adjust `CrawlDelayMilliseconds` to avoid overwhelming the target server:
```json
"CrawlDelayMilliseconds": 2000  // Wait 2 seconds between requests
```

### Image Storage

Currently, images are stored as byte arrays in the database. For better performance with many images:
- Store images in blob storage (Azure Blob Storage, AWS S3)
- Store only the file path/URL in the database

## Troubleshooting

### Authentication Fails
- Check that the login URL and credentials are correct
- Inspect the login form to verify field names
- Check if the website uses CSRF tokens (you'll need to extract and include them)

### No Products Found
- Verify the CSS selectors match the actual HTML structure
- Check the browser console and application logs for errors
- Use browser DevTools to inspect the page structure

### Database Errors
- Ensure migrations are up to date: `dotnet ef database update`
- Check connection string in `appsettings.json`

## Security Notes

?? **Important Security Considerations:**

1. **Never commit credentials to source control**
   - Use User Secrets for development: `dotnet user-secrets set "CrawlerSettings:Username" "your-username"`
   - Use environment variables or Azure Key Vault for production

2. **Respect robots.txt and Terms of Service**
   - Check if web scraping is allowed by the website
   - Respect rate limits and crawl delays

3. **Legal Compliance**
   - Ensure you have permission to scrape the target website
   - Be aware of GDPR and other data protection regulations

## Advanced Configuration

### Using User Secrets (Recommended for Development)

```bash
cd Product-Manager
dotnet user-secrets init
dotnet user-secrets set "CrawlerSettings:Username" "your-username"
dotnet user-secrets set "CrawlerSettings:Password" "your-password"
dotnet user-secrets set "CrawlerSettings:TargetUrl" "https://your-site.com"
dotnet user-secrets set "CrawlerSettings:LoginUrl" "https://your-site.com/login"
```

## API Endpoints

You can also trigger the crawler programmatically by creating a controller or minimal API endpoint.

Example minimal API in `Program.cs`:

```csharp
app.MapPost("/api/crawler/start", async (ProductCrawlerService crawler) =>
{
    await crawler.StartCrawlingAsync();
    return Results.Ok("Crawler started");
});
```

## Support

For issues or questions, please refer to the Abot documentation: https://github.com/sjdirect/abot
