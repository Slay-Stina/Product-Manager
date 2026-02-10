# Brand Configuration Management

## Overview

The Brand Configuration system allows you to save and manage multiple crawler configurations for different brands and websites. Each configuration includes URL settings, authentication credentials, crawler settings, and CSS selectors for extracting product data.

## Features

✅ **Multiple Brand Configurations** - Save unlimited brand configurations
✅ **Easy Switching** - Load any configuration with one click
✅ **CSS Selector Storage** - Save brand-specific selectors for product data
✅ **UTF-8 Support** - Full Unicode support for international brands
✅ **Persistent Storage** - Configurations saved to JSON file
✅ **Last Used Tracking** - See when each configuration was last used

## How to Use

### 1. Access Brand Configs Page

Navigate to **Brand Configs** from the sidebar menu.

### 2. Create a New Configuration

1. Fill in the **Brand Name** (e.g., "GANT", "H&M", "Zara")
2. Enter the **Target URL** where products are located
3. *(Optional)* Add login credentials if the site requires authentication
4. Add **CSS Selectors** for extracting product data:
   - Product Name Selector
   - Price Selector
   - Image Selector
   - Description Selector
   - SKU Selector
5. Configure crawler settings (max pages, delay, etc.)
6. Click **💾 Save Configuration**

### 3. Finding CSS Selectors

Use browser DevTools to find the right selectors:

**Chrome/Edge:**
1. Right-click on a product name → **Inspect**
2. In DevTools, right-click the highlighted HTML element
3. Select **Copy** → **Copy selector**
4. Paste it into the appropriate field
5. Repeat for price, images, description, and SKU

**Example selectors for GANT:**
```
Product Name: #ot-pc-title
Price: .price-sales
Image: .product-detail__grid-image img
Description: #ot-pc-desc
SKU: [data-pid]
```

### 4. Load a Configuration

1. Find your saved configuration in the table
2. Click **⚡ Load** to apply it to the crawler
3. The system will navigate you to the Products page
4. Start crawling!

### 5. Edit a Configuration

1. Click **✏️ Edit** next to the configuration
2. Modify the fields as needed
3. Click **💾 Save Configuration**

### 6. Delete a Configuration

Click **🗑️ Delete** to remove a configuration you no longer need.

## Configuration File Location

Configurations are stored in: `Data/brand-configs.json`

This file is created automatically and uses UTF-8 encoding for full Unicode support.

## Example Configuration (GANT)

```json
{
  "BrandName": "GANT Sweden",
  "TargetUrl": "https://www.gant.se/",
  "ProductNameSelector": "#ot-pc-title",
  "ProductPriceSelector": ".price-sales",
  "ProductImageSelector": ".product-detail__grid-image img",
  "ProductDescriptionSelector": "#ot-pc-desc",
  "MaxPagesToCrawl": 50,
  "CrawlDelayMilliseconds": 1500
}
```

## Tips

💡 **Test Selectors First** - Use browser console to test selectors: `document.querySelector('your-selector')`

💡 **Be Specific** - Use more specific selectors to avoid getting wrong data

💡 **Respect Rate Limits** - Set appropriate delay times (1000ms minimum recommended)

💡 **Check Terms of Service** - Ensure you have permission to scrape the target website

💡 **Unicode Support** - All fields support Unicode characters (å, ö, ñ, 中文, emoji, etc.)

## Workflow

```
1. Create/Edit Configuration → 
2. Save → 
3. Load Configuration → 
4. Go to Products Page → 
5. Start Crawling
```

## Troubleshooting

**❓ Selectors not working?**
- Verify the selector using browser DevTools
- The website might use dynamic content (JavaScript-rendered)
- Try more specific selectors

**❓ Authentication failing?**
- Check login URL is correct
- Verify username/password field names match the form
- Some sites use CSRF tokens (advanced handling needed)

**❓ No products found?**
- Check the Target URL points to a product page
- Verify selectors are correct
- Ensure crawl delay isn't too aggressive

## Security Note

⚠️ **Credentials are stored in plain text** in the JSON file. For production:
- Use environment variables
- Use Azure Key Vault or similar
- Never commit credentials to source control
- Add `brand-configs.json` to `.gitignore`

## Advanced Usage

### Bulk Import Configurations

You can manually edit `Data/brand-configs.json` to add multiple configurations at once.

### Backup Configurations

Copy `Data/brand-configs.json` to backup your configurations.

### Share Configurations

Share the JSON file with team members (remove credentials first!).

## Next Steps

After loading a configuration:
1. Go to the **Products** page
2. Click **Start Crawling** to begin
3. Monitor the progress and logs
4. View extracted products in the products grid
