# Crawler Issues Fixed

## Issue 1: Wrong Starting URL ❌ → ✅

### Problem
The crawler was starting from the homepage (`https://www.gant.se/`) instead of a category page.

**Result:** It crawled 20 random pages (cart, register, FAQ, etc.) with only 1 product page found.

### Solution
Updated `brand-configs.json` to use the category page:

```json
{
  "TargetUrl": "https://www.gant.se/herr/accessoarer/vaskor"
}
```

Now the crawler starts on a page with actual product links!

---

## Issue 2: JSON-LD Image Field Error ❌ → ✅

### Problem
```
System.InvalidOperationException: The requested operation requires an element of type 'String', but the target element has type 'Object'.
```

The code assumed `image` in JSON-LD is always a string, but it can be:
1. **String:** `"image": "https://example.com/image.jpg"`
2. **Array:** `"image": ["url1.jpg", "url2.jpg"]`
3. **Object:** `"image": { "@id": "url.jpg" }` or `{ "url": "url.jpg" }`

### Solution
Added proper handling for all three cases:

```csharp
if (root.TryGetProperty("image", out var imageProperty))
{
    if (imageProperty.ValueKind == JsonValueKind.String)
    {
        imageUrl = imageProperty.GetString();
    }
    else if (imageProperty.ValueKind == JsonValueKind.Array && imageProperty.GetArrayLength() > 0)
    {
        imageUrl = imageProperty[0].GetString();  // Take first image
    }
    else if (imageProperty.ValueKind == JsonValueKind.Object)
    {
        // Try @id or url property
        if (imageProperty.TryGetProperty("@id", out var idProp))
            imageUrl = idProp.GetString();
        else if (imageProperty.TryGetProperty("url", out var urlProp))
            imageUrl = urlProp.GetString();
    }
}
```

---

## Expected Behavior Now

### Before:
```
Page crawled: https://www.gant.se/ [OK]
Page crawled: https://www.gant.se/cart [OK]
Page crawled: https://www.gant.se/register [OK]
...
🎯 Detected product page: https://www.gant.se/p/gift-card/123 [CRASH]
```

### After:
```
Page crawled: https://www.gant.se/herr/accessoarer/vaskor [OK]
Page crawled: https://www.gant.se/p/necessaer-i-laeder/7325708333070 [OK]
🎯 Detected product page by URL pattern: /p/
✅ Product page data - SKU=7325708333070, Name=Necessär i läder, Price=1150.00 SEK
Added new product: 7325708333070
```

---

## Why This Matters

### URL Pattern Matching Works Like This:

1. **Crawler starts on category page** → Finds links to products
2. **Follows links** → Some contain `/p/` (product pages)
3. **Detects product pages** → Processes them automatically
4. **Other pages ignored** → Just logs and continues

### The Key:
The **category page must have links to product pages**. Starting from the homepage doesn't work because it doesn't link directly to products.

---

## Testing

Run the crawler now and you should see:

```
Product_Manager.Services.ProductCrawlerService: Information: 🎯 Detected product page by URL pattern: /p/
Product_Manager.Services.ProductCrawlerService: Information: ✅ Product page data - SKU=7325708333070
```

Multiple times for each product!

---

## Summary

✅ **TargetUrl** changed to category page  
✅ **Image parsing** now handles all JSON-LD formats  
✅ **Build** successful  
✅ **Ready** to crawl products  

The crawler will now:
1. Start on bags category
2. Find product links with `/p/`
3. Parse each product page
4. Extract complete data
5. Save to database

**No more crashes!** 🎉
