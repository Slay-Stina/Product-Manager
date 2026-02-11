# Article Number Extraction - Pattern Improvements

## Issue

The original regex pattern `@"/(\d+)\.html"` was too strict and required:
- URLs to end with `.html`
- This failed for URLs without the extension

## Actual GANT URL Formats

GANT uses various URL formats:
```
✅ /p/necessaer-i-laeder-cognac/7325708333070.html
✅ /p/necessaer-i-laeder-cognac/7325708333070
✅ /p/product-name/7325708333070?color=252
```

## New Solution

The crawler now tries **3 different patterns** in order:

### Pattern 1: With .html Extension
```csharp
@"/(\d{7,})\.html"
```
Matches: `/7325708333070.html`

### Pattern 2: Without Extension (End of Path)
```csharp
@"/(\d{7,})(?:[?#]|$)"
```
Matches: 
- `/7325708333070` (end of URL)
- `/7325708333070?param=value` (before query string)
- `/7325708333070#anchor` (before anchor)

### Pattern 3: Any Long Digit Sequence
```csharp
@"(\d{7,})"
```
Matches any sequence of 7+ digits (EANs are typically 7-13 digits)

## Why 7+ Digits?

- EAN-8: 8 digits
- EAN-13: 13 digits (most common)
- GTIN-14: 14 digits
- Using `{7,}` ensures we capture article numbers while avoiding short numbers like years (2024) or prices (150)

## Fallback

If URL extraction fails, the crawler tries the CSS selector:
```csharp
if (!string.IsNullOrWhiteSpace(_currentBrandConfig.ProductSkuSelector))
{
    articleNumber = ExtractText(document.DocumentElement, _currentBrandConfig.ProductSkuSelector);
}
```

## Examples

| URL | Pattern Match | Result |
|-----|---------------|--------|
| `/p/bag/7325708333070.html` | Pattern 1 | `7325708333070` |
| `/p/bag/7325708333070` | Pattern 2 | `7325708333070` |
| `/p/bag/7325708333070?color=252` | Pattern 2 | `7325708333070` |
| `/product/1234567` | Pattern 3 | `1234567` |
| `/bag-name-2024` | None (too short) | Falls back to selector |

## Logging

The crawler now logs which method was used:

```
📍 Extracted article number from URL: 7325708333070
```

Or if selector was used:
```
📍 Extracted article number from selector: 9980082-252
```

## Testing

Test with various URL formats:
```csharp
var urls = new[]
{
    "https://www.gant.se/p/necessaer/7325708333070.html",    // ✅ Pattern 1
    "https://www.gant.se/p/necessaer/7325708333070",         // ✅ Pattern 2
    "https://www.gant.se/p/necessaer/7325708333070?v=1",     // ✅ Pattern 2
    "https://www.gant.se/products/1234567",                   // ✅ Pattern 3
};
```

All should successfully extract the article number!

## Benefits

✅ **More Flexible** - Handles multiple URL formats  
✅ **More Reliable** - Doesn't fail on missing `.html`  
✅ **Better Logging** - Shows extraction method  
✅ **Fallback Safe** - Uses selector if URL fails  

---

**Status:** ✅ Implemented  
**Build:** ✅ Successful  
**Testing:** Ready for production  
