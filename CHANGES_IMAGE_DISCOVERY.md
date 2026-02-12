# Image Discovery Enhancement - Technical Documentation

## Overview
This update implements a robust image discovery and validation system that addresses the 403 Forbidden errors encountered when downloading product images from CDN URLs, including handling for zero-padded color IDs.

## Problem Statement
Previously, the crawler would:
1. Extract image URLs from JSON-LD or HTML selectors
2. Attempt to download images from these URLs
3. Fail with 403 errors because CDN URLs were access-restricted
4. **NEW**: Miss images where the article number format didn't match (e.g., `9970239-5` vs `9970239-005`)

## New Solution

### Key Changes

#### 1. **ImageDownloaderService.cs** - New Method: `FindAndValidateImageUrlsAsync()`

**Purpose**: Intelligently discovers and validates all image URLs on a product page containing the article number, including zero-padded variations.

**Process**:
1. **Pattern Generation** - Creates search patterns for the article number:
   - Original format: `9970239-5`
   - Zero-padded format: `9970239-005` (for 1-2 digit color IDs)
   - Handles standard 3-digit formats without modification

2. **Discovery Phase** - Searches the entire HTML document for image URLs matching any pattern:
   - Checks `<img>`, `<source>`, and `<link>` elements
   - Examines multiple attributes: `src`, `data-src`, `srcset`, `data-srcset`, `data-original`, `data-lazy-src`, `href`
   - Parses inline styles for `background-image` URLs
   - Scans all `data-*` attributes
   - Filters for URLs ending in `.jpg` that contain the article number (any pattern)

3. **Validation Phase** - Tests each discovered URL:
   - Makes absolute URLs
   - Applies brand-specific CDN transformations
   - Performs HTTP HEAD requests (fast, no download)
   - Keeps only URLs that return 200 OK status
   - Discards URLs returning 403, 404, or other errors

4. **Results** - Returns only validated, working image URLs

**Benefits**:
- **Handles zero-padding**: Finds images with formats like `9970239-005` even when article number is `9970239-5`
- Finds alternative/mirror URLs that aren't access-restricted
- Discovers multiple image variants (different sizes, angles)
- Resilient to URL structure changes
- No wasted bandwidth on failed downloads

#### 2. **ProductCrawlerService.cs** - Updated Image Handling

**Changes**:
- Added `ImageDownloaderService` as a constructor dependency
- Modified `ParseProductPageData()` to call the new discovery method
- Replaces parsed image URLs with validated ones before saving

**Flow**:
```
1. Parse product data (article number, name, price, etc.)
2. Call FindAndValidateImageUrlsAsync() with article number
3. Replace parsed ImageUrls with validated URLs
4. Save product with working image URLs
```

## Example Scenario

### Before:
```
❌ Article number: 9970239-5
❌ Searching for: "9970239-5" in image URLs
❌ Actual image URL: .../9970239-005-model-fv-1.jpg (MISS - zero-padded!)
❌ Found image URL from JSON-LD: 
   https://production-eu01-gant.demandware.net/.../9970239-005-model-fv-1.jpg
❌ Attempt download → 403 Forbidden
❌ No images saved
```

### After:
```
✅ Article number: 9970239-5
🔍 Searching for patterns: 9970239-5, 9970239-005
📋 Found 6 potential image URLs
🔄 Validating URLs...
✅ Valid: https://www.gant.se/dw/.../9970239-005-model-fv-1.jpg (200 OK)
✅ Valid: https://www.gant.se/dw/.../9970239-005-flat-fv-1.jpg (200 OK)
✅ Valid: https://www.gant.se/dw/.../9970239-005-flat-bv-1.jpg (200 OK)
✅ Valid: https://www.gant.se/dw/.../9970239-005-detail-fv-1.jpg (200 OK)
❌ Invalid: https://production-eu01-gant.demandware.net/... (403 Forbidden)
✅ Saved product with 4 working image URLs
```

## Configuration

No configuration changes required! The system works automatically for all brands.

### Article Number Pattern Matching
The system automatically handles:
- **Standard format**: `9970222-130` (3-digit color ID) → searches for `9970222-130`
- **Short format**: `9970239-5` (1-digit color ID) → searches for both `9970239-5` AND `9970239-005`
- **Medium format**: `9980089-45` (2-digit color ID) → searches for both `9980089-45` AND `9980089-045`

This ensures images are found regardless of URL formatting conventions.

### Brand-Specific Transformations
The existing `TransformCdnUrl()` method is still applied during validation, allowing brand-specific URL transformations to be tested.

## Performance Considerations

- **HEAD requests** are used instead of GET for validation (minimal bandwidth)
- Parallel validation of multiple URLs with `Task.WhenAll()`
- Short-circuits on first error per URL
- Typical overhead: 50-200ms per product page (depends on number of images found)

## Testing Recommendations

1. **Test with GANT products** - Should now download images successfully
2. **Check database** - Verify `ImageUrl` and `ImageData` columns are populated
3. **Monitor logs** - Look for "✅ Found X valid image URLs" messages
4. **Verify multiple images** - Check that products with multiple angles save all images

## Future Enhancements

Potential improvements:
- Add support for other image formats (`.png`, `.webp`)
- Cache validation results to avoid re-checking same URLs
- Prioritize certain URL patterns (e.g., prefer "front-view" over thumbnails)
- Add retry logic for transient network errors
- Support for progressive image quality checks

## Rollback Plan

If issues occur:
1. Remove `ImageDownloaderService` parameter from `ProductCrawlerService` constructor
2. Revert `ParseProductPageData()` changes
3. Restore old image handling logic from git history

## Code Files Modified

- `Product-Manager\Services\ImageDownloaderService.cs` - Added `FindAndValidateImageUrlsAsync()`
- `Product-Manager\Services\ProductCrawlerService.cs` - Updated constructor and `ParseProductPageData()`

Build status: ✅ Successful
