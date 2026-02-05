# Product Crawler - Quick Start Summary

## ? What Has Been Set Up

1. **Abot Web Crawler Library** - Installed and configured
2. **Product Database Model** - Created with fields for:
   - Article Number (Product ID)
   - Color ID
   - Description
   - Image URL
   - Image Data (binary)
   - Timestamps
3. **ProductCrawlerService** - Complete crawler service with:
   - Authentication support
   - HTML parsing
   - Image downloading
   - Database storage
4. **UI Pages**:
   - `/products` - View and manage products, start crawler
   - `/crawler-config` - Configure crawler settings
5. **Database Migration** - Ready to apply

## ?? Next Steps

### 1. Update the Database
Run this command to create the Products table:
```bash
dotnet ef database update --project Product-Manager\Product-Manager.csproj
```

### 2. Configure Your Target Website
Edit `appsettings.json` or use the `/crawler-config` page:
- Set the target product page URL
- Set the login page URL (if authentication required)
- Add your credentials
- Verify the form field names (username/password)

### 3. Customize HTML Selectors
**CRITICAL:** Open `Services\ProductCrawlerService.cs` and update the `ParseAndSaveProducts` method:

```csharp
// Line ~131 - Update these CSS selectors to match YOUR website
var productElements = document.QuerySelectorAll(".product-item, .product, [data-product]");

// Line ~136-139 - Update these selectors for YOUR website's structure
var articleNumber = ExtractText(productElement, ".product-id, .article-number");
var colorId = ExtractText(productElement, ".color-id, .product-color");
var description = ExtractText(productElement, ".product-description");
var imageUrl = ExtractImageUrl(productElement, "img");
```

**How to find the right selectors:**
1. Open your target website in Chrome/Edge
2. Right-click on a product ? "Inspect"
3. Look at the HTML structure
4. Find unique CSS classes or attributes
5. Update the selectors in the code

### 4. Test the Crawler
1. Run the application: `dotnet run`
2. Navigate to `/crawler-config`
3. Enter your website details
4. Go to `/products`
5. Click "Start Crawler"
6. Monitor the console logs

## ?? Files Created/Modified

### New Files:
- `Data\Product.cs` - Product entity model
- `Services\CrawlerSettings.cs` - Configuration model
- `Services\ProductCrawlerService.cs` - Main crawler service
- `Components\Pages\CrawlerConfig.razor` - Configuration UI
- `CRAWLER_README.md` - Detailed documentation

### Modified Files:
- `Data\ApplicationDbContext.cs` - Added Products DbSet
- `Program.cs` - Registered crawler services
- `Components\Pages\Products.razor` - Complete product management UI
- `Components\Layout\NavMenu.razor` - Added navigation links
- `appsettings.json` - Added crawler configuration section
- `Product-Manager.csproj` - Added Abot package

### Generated Files:
- Migration file for Products table

## ?? How It Works

1. **Authentication Flow:**
   - Service sends POST request to login URL with credentials
   - Stores authentication cookies
   - Uses cookies for subsequent requests

2. **Crawling Process:**
   - Starts at TargetUrl
   - Follows links on the page (configurable)
   - Respects crawl delay to avoid overwhelming server
   - Parses HTML using AngleSharp

3. **Data Extraction:**
   - Uses CSS selectors to find product information
   - Downloads images (stores as byte arrays)
   - Saves to database
   - Avoids duplicates (checks article number + color ID)

4. **UI Integration:**
   - Real-time updates during crawling
   - Display products in FluentUI DataGrid
   - Shows images inline

## ?? Common Customizations

### Different Authentication Method
If your site uses OAuth or API tokens, modify `AuthenticateAsync()` in `ProductCrawlerService.cs`.

### Additional Product Fields
1. Add properties to `Data\Product.cs`
2. Create new migration: `dotnet ef migrations add AddNewFields`
3. Update extraction logic in `ParseAndSaveProducts()`
4. Update UI in `Products.razor`

### JavaScript-Rendered Content
If products are loaded via JavaScript:
- Consider using Selenium or Playwright instead of Abot
- Or find the API endpoint that loads the data

### Multiple Product Pages
The crawler will automatically follow links. Configure:
```json
"MaxPagesToCrawl": 500,  // Increase for more pages
"CrawlDelayMilliseconds": 2000  // Slow down for safety
```

## ?? Security Best Practices

### Development
```bash
dotnet user-secrets set "CrawlerSettings:Username" "your-username"
dotnet user-secrets set "CrawlerSettings:Password" "your-password"
```

### Production
- Use Azure Key Vault or environment variables
- Never commit credentials
- Add `appsettings.json` sensitive sections to `.gitignore`

## ?? Monitoring

Check application logs for:
- Authentication success/failure
- Number of pages crawled
- Products found and saved
- Errors during extraction

Logs are in Visual Studio Output window or console.

## ?? Troubleshooting

| Issue | Solution |
|-------|----------|
| No products found | Update CSS selectors to match actual HTML |
| Authentication fails | Check credentials, field names, CSRF tokens |
| Database errors | Run migrations: `dotnet ef database update` |
| Images not loading | Check image URL format, update `DownloadImage()` |
| Crawler too slow | Reduce `CrawlDelayMilliseconds` (be respectful) |
| Crawler blocked | Website may have bot detection, add delays |

## ?? Resources

- Abot Documentation: https://github.com/sjdirect/abot
- AngleSharp Documentation: https://anglesharp.github.io/
- CSS Selectors Reference: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_Selectors

## ?? Legal Notice

Ensure you have permission to scrape the target website. Check:
- Website's Terms of Service
- robots.txt file
- Rate limiting requirements
- Data protection regulations (GDPR, etc.)

---

**Ready to start!** Update the database, configure your target website, customize the selectors, and start crawling!
