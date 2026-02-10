# Troubleshooting Guide

This document contains solutions to common issues you might encounter.

## No Products Appearing in Database

### Symptom
The crawler reports finding products (e.g., "Found 6 potential product elements") but no products appear in the database.

### Common Causes & Solutions

#### 1. Brand Config Not Applied
**Check:** Are you using brand-specific CSS selectors?

**Solution:**
1. Go to Brand Configs page
2. Click "⚡ Load" on the appropriate brand
3. Verify the crawler is using the correct selectors

**Verify in logs:**
```
🎯 Using brand configuration: GANT Sweden
🎯 Using brand-specific selector: .product-grid__item
```

#### 2. Incorrect CSS Selectors
**Check:** Do the selectors match the actual HTML structure?

**Solution:**
1. Open the target page in browser
2. Right-click product ? Inspect
3. Verify the CSS classes match your brand config
4. Update brand config if needed

#### 3. JavaScript-Rendered Content
**Check:** Are products loaded via JavaScript?

**Solution:**
The crawler can only see HTML that exists when the page loads. If products load via JavaScript after page load, they won't be detected.

**Workaround:**
- Use a page that renders products server-side
- Or consider using a headless browser approach (not currently implemented)

#### 4. Authentication Required
**Check:** Does the site require login?

**Solution:**
Configure authentication in `appsettings.json`:
```json
{
  "CrawlerSettings": {
    "LoginUrl": "https://example.com/login",
    "Username": "your-username",
    "Password": "your-password"
  }
}
```

## Products Loading But Missing Images

### Symptom
Products appear in database but "?? No image" shows in the grid.

### Solution
Modern sites use lazy loading. The crawler checks multiple attributes:
- `src` (standard)
- `data-src` (lazy loading)
- `data-original` (alternative)
- `srcset` (responsive images)

**Verify:**
1. Check browser DevTools for the actual image attribute
2. Update `ProductImageSelector` in brand config if needed

## Database Errors

### "String or binary data would be truncated"

**Cause:** Data exceeds field size limits.

**Solution:**
- Product descriptions are limited to 2000 characters
- Article numbers limited to 100 characters
- If you need more, update the model and create a migration

### "Cannot insert duplicate key"

**Cause:** Product with same ArticleNumber already exists.

**Solution:**
The crawler updates existing products. If you see this error, check:
- Is `ArticleNumber` unique for each product?
- Are you extracting the correct field as ArticleNumber?

## Blazor Component Errors

### "The renderer does not have a component with ID X"

**Cause:** Component disposed while async operation still running.

**Solution:**
This has been fixed in the Products page. If you see it elsewhere:
1. Implement `IDisposable`
2. Use `await InvokeAsync(StateHasChanged)`
3. Check disposal before state updates

## Getting Help

### Enable Detailed Logging

In `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Product_Manager.Services.ProductCrawlerService": "Debug"
    }
  }
}
```

### Check the Logs

Look for:
- `??` - What selectors are being used
- `?` - Successfully found/saved products
- `?` - Errors during crawling
- `??` - Warnings about missing configs

### Common Log Messages

| Message | Meaning |
|---------|---------|
| `No brand config set, using generic selectors` | Brand config not applied - likely won't find products |
| `Using brand-specific selector: X` | Brand config is active ? |
| `No article number/SKU found` | Check ProductSkuSelector |
| `Skipping element - no article number` | Elements found but no valid product ID |

---

*For historical changes and fixes, see [CHANGELOG.md](../CHANGELOG.md)*

### Issue #2: No Logging to Diagnose the Problem
Added comprehensive logging to show:
- How many brand configs are loaded
- Which config is being checked
- Whether a match was found
- Which brand config is being applied

## How to Verify the Fix

### 1. Check the Logs
When you click "Start Crawler", you should now see:
```
?? Looking for brand config matching URL: https://www.gant.se/herr/accessoarer/vaskor
?? Found 2 total brand configs
?? Target host: www.gant.se
?? Checking config 'Example Generic Site' with host: example.com
?? Checking config 'GANT Sweden' with host: www.gant.se
? Found match: GANT Sweden
?? Applying brand config: GANT Sweden
?? Using brand configuration: GANT Sweden
?? Using brand-specific selector: .product-grid__item
Found 6 potential product elements
```

### 2. Verify Products Are Saved
After crawling completes, you should see:
```
? Found product: SKU=9980082-252, Name=Necess�r i l�der, Price=1 150 kr, HasImage=True
? Found product: SKU=9922047-5, Name=Totev�ska i vaxad bomull, Price=2 450 kr, HasImage=True
```

### 3. Check the Products Page
The products grid should now display:
- Article Number: 9980082-252, 9922047-5, etc.
- Description: "Necess�r i l�der - 1 150 kr"
- Product Images
- Created Date

## Testing Steps

1. **Ensure GANT Config is Loaded**
   - Go to Brand Configs page
   - Verify "GANT Sweden" exists
   - Click "? Load" if needed

2. **Start Crawling**
   - Go to Products page
   - Click "?? Start Crawler"
   - Watch the status message - should say "Using GANT Sweden configuration"

3. **Check Output Window**
   - View ? Output
   - Select "Debug" from dropdown
   - Look for the logging messages above

4. **Verify Products**
   - Products should appear in the grid
   - Should have Swedish names like "Necess�r i l�der"
   - Should have prices in SEK (kr)

## If It Still Doesn't Work

### Check 1: Is the Target URL Set?
```
?? Starting crawler for https://www.gant.se/herr/accessoarer/vaskor
```
If you see `https://example.com/products`, the GANT config wasn't loaded.

**Fix:** Go to Brand Configs ? Click "? Load" next to GANT Sweden

### Check 2: Are Brand Configs Loading?
```
?? Found 2 total brand configs
```
If this shows 0, check:
- Does `Data/brand-configs.json` exist?
- Is it valid JSON?
- Are there any exceptions in the logs?

### Check 3: Is the Matching Working?
```
?? Checking config 'GANT Sweden' with host: www.gant.se
? Found match: GANT Sweden
```
If no match is found, verify:
- The TargetUrl in CrawlerSettings contains "www.gant.se"
- The TargetUrl in the GANT BrandConfig contains "www.gant.se"

### Check 4: Are Selectors Correct?
```
Found 6 potential product elements
?? Extracted - SKU: 9980082-252, Name: Necess�r i l�der, Price: 1 150 kr
```
If SKU is "(none)", the selectors are wrong:
- Go to Brand Configs
- Edit GANT Sweden
- Verify selectors match the current GANT website HTML

### Check 5: Database Connection
```sql
SELECT * FROM Products
```
Run this query to verify products are actually being saved to the database.

## Common Issues

### "No brand config set, using generic selectors"
**Cause:** The matching logic didn't find a matching brand config  
**Fix:** Ensure you clicked "Load" on the GANT config before crawling

### "Found 6 potential product elements" but no products saved
**Cause:** The SKU selector isn't finding the `data-pid` attribute  
**Fix:** The GANT HTML structure may have changed - update selectors

### Products appear but no images
**Cause:** Image URLs are relative or behind authentication  
**Fix:** Check the ImageUrl in the database - may need to prepend base URL

## Next Steps

Once products are loading correctly:
1. ? Verify product data quality
2. ? Add more brands (H&M, Zara, etc.)
3. ? Set up scheduled crawling
4. ? Add pagination support for more than 6 products

## Quick Reference: Expected Log Flow

```
1. ?? Looking for brand config matching URL: https://www.gant.se/herr/accessoarer/vaskor
2. ?? Found 2 total brand configs
3. ?? Target host: www.gant.se
4. ?? Checking config 'GANT Sweden' with host: www.gant.se
5. ? Found match: GANT Sweden
6. ?? Applying brand config: GANT Sweden
7. ?? Starting crawler for https://www.gant.se/herr/accessoarer/vaskor
8. ?? Skipping authentication (no login required)
9. Page crawled: https://www.gant.se/herr/accessoarer/vaskor [OK]
10. ?? Using brand configuration: GANT Sweden
11. ?? Using brand-specific selector: .product-grid__item
12. Found 6 potential product elements
13. ? Found product: SKU=9980082-252, Name=Necess�r i l�der, Price=1 150 kr
14. [... more products ...]
15. ? Crawl completed successfully!
16. ?? Crawled 5 pages
17. ? Loaded 6 products
```

If your logs match this, everything is working correctly! ??
